[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($PSVersionTable.ContainsKey("Platform") -and $PSVersionTable.Platform -ne "Win32NT") {
    throw "scripts/Test.ps1 is Windows-only. Use bash ./scripts/Test.sh on Linux."
}

$root = Split-Path -Parent $PSScriptRoot
$nugetConfig = Join-Path $root "NuGet.Config"
$testProjects = @(
    @{
        Path = Join-Path (Join-Path $root "tests/PicLens.Core.Tests") "PicLens.Core.Tests.csproj"
        Properties = @()
    },
    @{
        Path = Join-Path (Join-Path $root "tests/PicLens.Infrastructure.Tests") "PicLens.Infrastructure.Tests.csproj"
        Properties = @()
    },
    @{
        Path = Join-Path (Join-Path $root "tests/PicLens.ViewModels.Tests") "PicLens.ViewModels.Tests.csproj"
        Properties = @()
    }
)

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

if (-not (Test-Path -LiteralPath $nugetConfig)) {
    throw "NuGet.Config not found: $nugetConfig"
}

foreach ($testProject in $testProjects) {
    if (-not (Test-Path -LiteralPath $testProject.Path)) {
        throw "Test project file not found: $($testProject.Path)"
    }
}

foreach ($testProject in $testProjects) {
    Write-Host "==> Restoring test project: $($testProject.Path)" -ForegroundColor Cyan
    $restoreArgs = @(
        "restore",
        $testProject.Path,
        "--configfile",
        $nugetConfig
    ) + $testProject.Properties
    Invoke-Native "dotnet" $restoreArgs
}

foreach ($testProject in $testProjects) {
    Write-Host "==> Running tests: $($testProject.Path)" -ForegroundColor Cyan
    $testArgs = @(
        "test",
        $testProject.Path,
        "--no-restore"
    ) + $testProject.Properties
    Invoke-Native "dotnet" $testArgs
}
