<#
.SYNOPSIS
Builds and optionally runs the PicLens Avalonia desktop app.

.EXAMPLE
.\BuildAndRun.ps1 .\PicLens\PicLens.csproj
.\BuildAndRun.ps1 .\PicLens\PicLens.csproj -SkipRun
.\BuildAndRun.ps1 .\PicLens\PicLens.csproj /p:Configuration=Release
#>

param(
    [Parameter(Position = 0)]
    [string]$Project = ".\PicLens\PicLens.csproj",
    [switch]$SkipRun,
    [switch]$Detach,
    [Parameter(ValueFromRemainingArguments)]
    [string[]]$ExtraArgs
)

$ErrorActionPreference = "Stop"

if ($ExtraArgs -contains "--detach") {
    $Detach = $true
    $ExtraArgs = $ExtraArgs | Where-Object { $_ -ne "--detach" }
}

$projectPath = (Resolve-Path -LiteralPath $Project -ErrorAction Stop).Path
$projectDir = Split-Path -Parent $projectPath
$projectName = [System.IO.Path]::GetFileNameWithoutExtension($projectPath)
$platform = if ($env:PROCESSOR_ARCHITECTURE -eq "ARM64") { "ARM64" } else { "x64" }
$configuration = "Debug"

foreach ($arg in $ExtraArgs) {
    if ($arg -match "^[/-]p:Platform=(.+)$") {
        $platform = $Matches[1]
    } elseif ($arg -match "^[/-]p:Configuration=(.+)$") {
        $configuration = $Matches[1]
    }
}

$buildArgs = @(
    "build",
    $projectPath,
    "/restore",
    "-p:Platform=$platform",
    "-p:Configuration=$configuration"
) + $ExtraArgs

Write-Host "==> Building $projectName ($configuration|$platform)" -ForegroundColor Cyan
& dotnet @buildArgs
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "BUILD SUCCEEDED" -ForegroundColor Green

if ($SkipRun) {
    Write-Host "==> Skipping run (-SkipRun)" -ForegroundColor DarkGray
    exit 0
}

$binDir = Join-Path $projectDir "bin\$platform\$configuration"
$exePath = Get-ChildItem -LiteralPath $binDir -Filter "$projectName.exe" -Recurse -File |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1

if (-not $exePath) {
    throw "Build completed but $projectName.exe was not found under $binDir."
}

Write-Host "==> Launching $($exePath.FullName)" -ForegroundColor Cyan
if ($Detach) {
    Start-Process -FilePath $exePath.FullName -WorkingDirectory $exePath.DirectoryName
} else {
    & $exePath.FullName
}
