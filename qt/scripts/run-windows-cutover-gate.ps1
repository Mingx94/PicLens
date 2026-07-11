[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PerformanceFolder
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not $IsWindows) {
    throw "The Windows cutover gate requires Windows"
}

$qtRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$repoRoot = [IO.Path]::GetFullPath((Join-Path $qtRoot ".."))
$resolvedPerformanceFolder = (Resolve-Path -LiteralPath $PerformanceFolder).Path
$artifactRoot = Join-Path $repoRoot "artifacts\cutover"
$evidencePath = Join-Path $artifactRoot "windows-local-gate.json"
$portableRoot = Join-Path $repoRoot "artifacts\qt-portable\PicLens-win-x64"
$portableExecutable = Join-Path $portableRoot "PicLens.exe"
$msiPath = Join-Path $repoRoot "artifacts\installer\PicLens-win-x64.msi"
$performancePath = Join-Path $repoRoot "artifacts\performance\windows-release.json"
$continuityPath = Join-Path $repoRoot "artifacts\data-migration\profile-continuity.json"

function Invoke-GateCommand([string]$Label, [string]$FileName, [string[]]$Arguments) {
    Write-Host "==> $Label"
    & $FileName @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Label failed with exit code $LASTEXITCODE"
    }
}

Push-Location $repoRoot
try {
    Invoke-GateCommand "Configure Qt Debug" "cmake" @("--preset", "debug", "-S", "qt")
    Invoke-GateCommand "Build Qt Debug" "cmake" @("--build", "qt/build/debug")
    Invoke-GateCommand "Test Qt Debug" "ctest" @("--test-dir", "qt/build/debug", "--output-on-failure")

    Invoke-GateCommand "Configure Qt Release" "cmake" @("--preset", "release", "-S", "qt")
    Invoke-GateCommand "Build Qt Release" "cmake" @("--build", "qt/build/release")
    Invoke-GateCommand "Test Qt Release" "ctest" @("--test-dir", "qt/build/release", "--output-on-failure")

    Invoke-GateCommand "Test legacy rollback baseline" "dotnet" @("run", "Tasks.cs", "--", "test")
    Invoke-GateCommand "Test legacy UI rollback baseline" "dotnet" @("run", "Tasks.cs", "--", "ui-test")
    Invoke-GateCommand "Measure representative Qt Release performance" "pwsh" @(
        "-NoProfile", "-File", "qt/scripts/measure-performance.ps1",
        "-FolderPath", $resolvedPerformanceFolder)
    Invoke-GateCommand "Build Qt portable" "dotnet" @("run", "Tasks.cs", "--", "release")
    Invoke-GateCommand "Build and audit Qt MSI" "dotnet" @(
        "run", "Tasks.cs", "--", "installer", "--no-release")
    Invoke-GateCommand "Verify packaged profile continuity" "pwsh" @(
        "-NoProfile", "-File", "qt/scripts/verify-data-continuity.ps1",
        "-Executable", $portableExecutable)

    foreach ($requiredArtifact in @(
        $portableExecutable,
        $msiPath,
        $performancePath,
        $continuityPath
    )) {
        if (-not (Test-Path -LiteralPath $requiredArtifact -PathType Leaf)) {
            throw "Required cutover evidence is missing: $requiredArtifact"
        }
    }

    $debugTests = (ctest --test-dir qt/build/debug --show-only=json-v1 | ConvertFrom-Json).tests.Count
    $releaseTests = (ctest --test-dir qt/build/release --show-only=json-v1 | ConvertFrom-Json).tests.Count
    $performance = Get-Content -LiteralPath $performancePath -Raw | ConvertFrom-Json
    $continuity = Get-Content -LiteralPath $continuityPath -Raw | ConvertFrom-Json
    $portableFiles = @(Get-ChildItem -LiteralPath $portableRoot -Recurse -File)
    [int64]$portableBytes = ($portableFiles | Measure-Object -Property Length -Sum).Sum
    $msiFile = Get-Item -LiteralPath $msiPath
    $evidence = [ordered]@{
        schemaVersion = 1
        generatedAtUtc = [DateTime]::UtcNow.ToString("o")
        platform = [Environment]::OSVersion.VersionString
        qt = [ordered]@{
            debugTestsPassed = $debugTests
            releaseTestsPassed = $releaseTests
        }
        legacyRollback = [ordered]@{
            unitTestsPassed = $true
            uiTestsPassed = $true
        }
        performance = $performance
        profileContinuity = $continuity
        portable = [ordered]@{
            path = $portableRoot
            files = $portableFiles.Count
            bytes = $portableBytes
            executableSha256 = (Get-FileHash -LiteralPath $portableExecutable -Algorithm SHA256).Hash
        }
        msi = [ordered]@{
            path = $msiPath
            bytes = $msiFile.Length
            sha256 = (Get-FileHash -LiteralPath $msiPath -Algorithm SHA256).Hash
            databaseAuditPassed = $true
        }
        exclusions = @(
            "MSI install/upgrade/uninstall lifecycle is a separate elevated gate",
            "Windows UIA tree requires explicit Computer Use approval",
            "Hosted Windows/Linux evidence is produced by GitHub Actions"
        )
    }
    New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
    $evidence | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $evidencePath -Encoding utf8NoBOM
    Write-Host "Windows local cutover gate passed"
    Write-Host "  Qt tests: Debug $debugTests / Release $releaseTests"
    Write-Host "  Evidence: $evidencePath"
}
finally {
    Pop-Location
}
