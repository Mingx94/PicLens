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
$nugetConfig = Join-Path $root "NuGet.Config"
$uiTestProject = Join-Path $root "tests\PicLens.Ui.Tests\PicLens.Ui.Tests.csproj"

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

foreach ($path in @($nugetConfig, $uiTestProject)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required file not found: $path"
    }
}

Write-Host "==> Restoring Avalonia headless UI test project" -ForegroundColor Cyan
Invoke-Native "dotnet" @(
    "restore",
    $uiTestProject,
    "--configfile",
    $nugetConfig
)

Write-Host "==> Running Avalonia headless UI smoke tests" -ForegroundColor Cyan
Invoke-Native "dotnet" @(
    "test",
    $uiTestProject,
    "--no-restore",
    "-c",
    $Configuration
)
