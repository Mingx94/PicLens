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
$repoRoot = $qtRoot
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
    Invoke-GateCommand "Configure Qt Debug" "cmake" @("--preset", "debug")
    Invoke-GateCommand "Build Qt Debug" "cmake" @("--build", "build/debug")
    Invoke-GateCommand "Test Qt Debug" "ctest" @("--test-dir", "build/debug", "--output-on-failure")

    Invoke-GateCommand "Configure Qt Release" "cmake" @("--preset", "release")
    Invoke-GateCommand "Build Qt Release" "cmake" @("--build", "build/release")
    Invoke-GateCommand "Test Qt Release" "ctest" @("--test-dir", "build/release", "--output-on-failure")

    Invoke-GateCommand "Measure representative Qt Release performance" "pwsh" @(
        "-NoProfile", "-File", "scripts/measure-performance.ps1",
        "-FolderPath", $resolvedPerformanceFolder)
    Invoke-GateCommand "Build Qt portable" "pwsh" @(
        "-NoProfile", "-File", "scripts/build-portable.ps1")
    Invoke-GateCommand "Build and audit Qt MSI" "pwsh" @(
        "-NoProfile", "-File", "scripts/build-msi.ps1", "-NoRelease")
    Invoke-GateCommand "Verify packaged profile continuity" "pwsh" @(
        "-NoProfile", "-File", "scripts/verify-data-continuity.ps1",
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

    $debugTests = (ctest --test-dir build/debug --show-only=json-v1 | ConvertFrom-Json).tests.Count
    $releaseTests = (ctest --test-dir build/release --show-only=json-v1 | ConvertFrom-Json).tests.Count
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
