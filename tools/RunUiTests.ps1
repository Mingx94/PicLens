[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [ValidateSet("win-x64")]
    [string]$RuntimeIdentifier = "win-x64",

    [ValidateSet("x64")]
    [string]$Platform = "x64"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$releaseScript = Join-Path $root "Release.ps1"
$nugetConfig = Join-Path $root "NuGet.Config"
$uiTestProject = Join-Path $root "tests\PicLens.Ui.Tests\PicLens.Ui.Tests.csproj"
$appPath = Join-Path $root "artifacts\portable\PicLens-$RuntimeIdentifier\PicLens.exe"

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

foreach ($path in @($releaseScript, $nugetConfig, $uiTestProject)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required file not found: $path"
    }
}

Write-Host "==> Publishing PicLens for UI smoke tests" -ForegroundColor Cyan
& $releaseScript `
    -Configuration $Configuration `
    -RuntimeIdentifier $RuntimeIdentifier `
    -Platform $Platform `
    -SkipTests
if ($LASTEXITCODE -ne 0) {
    throw "Release.ps1 failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path -LiteralPath $appPath)) {
    throw "Published app not found: $appPath"
}

Write-Host "==> Restoring FlaUI test project" -ForegroundColor Cyan
Invoke-Native "dotnet" @(
    "restore",
    $uiTestProject,
    "--configfile",
    $nugetConfig
)

$previousAppPath = $env:PICLENS_UI_APP_PATH
try {
    $env:PICLENS_UI_APP_PATH = $appPath

    Write-Host "==> Running FlaUI UI smoke tests" -ForegroundColor Cyan
    Invoke-Native "dotnet" @(
        "test",
        $uiTestProject,
        "--no-restore",
        "-p:Platform=$Platform"
    )
}
finally {
    $env:PICLENS_UI_APP_PATH = $previousAppPath
}
