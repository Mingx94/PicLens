[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [ValidateSet("win-x64", "win-arm64", "win-x86")]
    [string]$RuntimeIdentifier = "win-x64",

    [ValidateSet("x64", "ARM64", "x86")]
    [string]$Platform = "x64",

    [switch]$SkipTests,
    [switch]$NoClean
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$project = Join-Path $root "PicLens\PicLens.csproj"
$nugetConfig = Join-Path $root "NuGet.Config"
$testScript = Join-Path $root "Test.ps1"
$outputRoot = Join-Path $root "artifacts\portable"
$outputName = "PicLens-$RuntimeIdentifier"
$outputDir = Join-Path $outputRoot $outputName

function Assert-UnderRoot {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    $resolvedRoot = [System.IO.Path]::GetFullPath($root)
    $resolvedPath = [System.IO.Path]::GetFullPath($Path)

    if (-not $resolvedPath.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to operate outside workspace root: $resolvedPath"
    }
}

function Invoke-Native {
    param(
        [Parameter(Mandatory)]
        [string]$FilePath,

        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath failed with exit code $LASTEXITCODE."
    }
}

Assert-UnderRoot -Path $outputRoot
Assert-UnderRoot -Path $outputDir

if ($RuntimeIdentifier -eq "win-x64" -and $Platform -ne "x64") {
    throw "RuntimeIdentifier win-x64 requires -Platform x64."
}
if ($RuntimeIdentifier -eq "win-arm64" -and $Platform -ne "ARM64") {
    throw "RuntimeIdentifier win-arm64 requires -Platform ARM64."
}
if ($RuntimeIdentifier -eq "win-x86" -and $Platform -ne "x86") {
    throw "RuntimeIdentifier win-x86 requires -Platform x86."
}

foreach ($path in @($project, $nugetConfig, $testScript)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required file not found: $path"
    }
}

New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null

if (-not $NoClean) {
    if (Test-Path -LiteralPath $outputDir) {
        Remove-Item -LiteralPath $outputDir -Recurse -Force
    }
}

if (-not $SkipTests) {
    Write-Host "==> Running unit tests" -ForegroundColor Cyan
    & $testScript
    if ($LASTEXITCODE -ne 0) {
        throw "$testScript failed with exit code $LASTEXITCODE."
    }
}

Write-Host "==> Restoring app for $RuntimeIdentifier" -ForegroundColor Cyan
Invoke-Native "dotnet" @(
    "restore",
    $project,
    "--configfile",
    $nugetConfig,
    "-r",
    $RuntimeIdentifier,
    "/p:Platform=$Platform",
    "/p:WindowsPackageType=None",
    "/p:WindowsAppSDKSelfContained=false",
    "/p:SelfContained=false"
)

Write-Host "==> Publishing framework-dependent output" -ForegroundColor Cyan
Invoke-Native "dotnet" @(
    "publish",
    $project,
    "--no-restore",
    "-c",
    $Configuration,
    "-r",
    $RuntimeIdentifier,
    "--self-contained",
    "false",
    "/p:Platform=$Platform",
    "/p:WindowsPackageType=None",
    "/p:WindowsAppSDKSelfContained=false",
    "/p:PublishSelfContained=false",
    "/p:PublishSingleFile=false",
    "/p:PublishReadyToRun=false",
    "/p:PublishTrimmed=false",
    "/p:SelfContained=false",
    "/p:DebugType=None",
    "/p:DebugSymbols=false",
    "-o",
    $outputDir
)

$exePath = Join-Path $outputDir "PicLens.exe"
if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Publish completed but PicLens.exe was not found at: $exePath"
}

$fileCount = (Get-ChildItem -LiteralPath $outputDir -Recurse -File | Measure-Object).Count
$totalBytes = (Get-ChildItem -LiteralPath $outputDir -Recurse -File | Measure-Object -Property Length -Sum).Sum
$sha256 = (Get-FileHash -LiteralPath $exePath -Algorithm SHA256).Hash

Write-Host ""
Write-Host "Release output ready:" -ForegroundColor Green
Write-Host "  Folder: $outputDir"
Write-Host "  Exe:    $exePath"
Write-Host "  Files:  $fileCount"
Write-Host "  Bytes:  $totalBytes"
Write-Host "  SHA256: $sha256"
