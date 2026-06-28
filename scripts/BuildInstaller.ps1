[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [ValidateSet("win-x64", "win-arm64", "win-x86")]
    [string]$RuntimeIdentifier = "win-x64",

    [ValidateSet("x64", "ARM64", "x86")]
    [string]$Platform = "x64",

    [string]$Version = "1.0.0.0",
    [string]$InnoSetupCompiler,

    [switch]$SkipTests,
    [switch]$NoClean
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($PSVersionTable.ContainsKey("Platform") -and $PSVersionTable.Platform -ne "Win32NT") {
    throw "scripts/BuildInstaller.ps1 is Windows-only."
}

$root = Split-Path -Parent $PSScriptRoot
$releaseScript = Join-Path $PSScriptRoot "Release.ps1"
$installerScript = Join-Path (Join-Path $root "installer") "PicLens.iss"
$portableDir = Join-Path (Join-Path (Join-Path $root "artifacts") "portable") "PicLens-$RuntimeIdentifier"
$installerRoot = Join-Path (Join-Path $root "artifacts") "installer"
$stageRoot = Join-Path $installerRoot "setup-stage"
$stageDir = Join-Path $stageRoot "PicLens-$RuntimeIdentifier"
$outputBaseName = "PicLens-$RuntimeIdentifier-Setup"
$setupPath = Join-Path $installerRoot "$outputBaseName.exe"

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

function Find-InnoSetupCompiler {
    param(
        [string]$Path
    )

    if ($Path) {
        if (-not (Test-Path -LiteralPath $Path)) {
            throw "Inno Setup compiler was not found: $Path"
        }

        return (Resolve-Path -LiteralPath $Path).Path
    }

    $command = Get-Command "iscc.exe" -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    foreach ($candidate in @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )) {
        if ($candidate -and (Test-Path -LiteralPath $candidate)) {
            return $candidate
        }
    }

    throw "Inno Setup compiler not found. Install Inno Setup 6 (winget install --id JRSoftware.InnoSetup -e), then rerun this script."
}

function Test-SetupVersion {
    param(
        [Parameter(Mandatory)]
        [string]$Value
    )

    return $Value -match '^\d+\.\d+\.\d+(\.\d+)?$'
}

if (-not (Test-SetupVersion $Version)) {
    throw "Installer version must be three or four dot-separated numbers: $Version"
}

Assert-UnderRoot -Path $installerRoot
Assert-UnderRoot -Path $stageRoot
Assert-UnderRoot -Path $stageDir
Assert-UnderRoot -Path $setupPath

foreach ($path in @($releaseScript, $installerScript)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required file not found: $path"
    }
}

$releaseArgs = @{
    Configuration = $Configuration
    RuntimeIdentifier = $RuntimeIdentifier
    Platform = $Platform
}
if ($SkipTests) {
    $releaseArgs.SkipTests = $true
}
if ($NoClean) {
    $releaseArgs.NoClean = $true
}

$iscc = Find-InnoSetupCompiler -Path $InnoSetupCompiler

Write-Host "==> Building portable release" -ForegroundColor Cyan
& $releaseScript @releaseArgs
if ($LASTEXITCODE -ne 0) {
    throw "$releaseScript failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path -LiteralPath (Join-Path $portableDir "PicLens.exe"))) {
    throw "Portable release was not found: $portableDir"
}

New-Item -ItemType Directory -Path $installerRoot -Force | Out-Null

if (-not $NoClean) {
    Remove-Item -LiteralPath $stageRoot -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $setupPath -Force -ErrorAction SilentlyContinue
}

New-Item -ItemType Directory -Path $stageDir -Force | Out-Null
Copy-Item -Path (Join-Path $portableDir "*") -Destination $stageDir -Recurse -Force
Get-ChildItem -LiteralPath $stageDir -Recurse -Filter "*.pdb" -File | Remove-Item -Force

Write-Host "==> Building Inno Setup installer" -ForegroundColor Cyan
Invoke-Native $iscc @(
    "/DAppVersion=$Version",
    "/DRootDir=$root",
    "/DPayloadDir=$stageDir",
    "/DOutputDir=$installerRoot",
    "/DOutputBaseFilename=$outputBaseName",
    $installerScript
)

if (-not (Test-Path -LiteralPath $setupPath)) {
    throw "Installer build completed but setup file was not found: $setupPath"
}

$sha256 = (Get-FileHash -LiteralPath $setupPath -Algorithm SHA256).Hash
$bytes = (Get-Item -LiteralPath $setupPath).Length

Write-Host ""
Write-Host "Installer output ready:" -ForegroundColor Green
Write-Host "  Setup:  $setupPath"
Write-Host "  Bytes:  $bytes"
Write-Host "  SHA256: $sha256"
