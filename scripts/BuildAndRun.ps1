<#
.SYNOPSIS
Builds and optionally runs the PicLens Avalonia desktop app.

.EXAMPLE
.\scripts\BuildAndRun.ps1 .\PicLens\PicLens.csproj
.\scripts\BuildAndRun.ps1 .\PicLens\PicLens.csproj -SkipRun
.\scripts\BuildAndRun.ps1 .\PicLens\PicLens.csproj /p:Configuration=Release
#>

param(
    [Parameter(Position = 0)]
    [string]$Project = "PicLens/PicLens.csproj",
    [switch]$SkipRun,
    [switch]$Detach,
    [Parameter(ValueFromRemainingArguments)]
    [string[]]$ExtraArgs
)

$ErrorActionPreference = "Stop"

if ($PSVersionTable.ContainsKey("Platform") -and $PSVersionTable.Platform -ne "Win32NT") {
    throw "scripts/BuildAndRun.ps1 is Windows-only. Use bash ./scripts/BuildAndRun.sh on Linux."
}

$repoRoot = Split-Path -Parent $PSScriptRoot

if ($ExtraArgs -contains "--detach") {
    $Detach = $true
    $ExtraArgs = $ExtraArgs | Where-Object { $_ -ne "--detach" }
}

$projectCandidate = if ([System.IO.Path]::IsPathRooted($Project)) { $Project } else { Join-Path $repoRoot $Project }
$projectPath = (Resolve-Path -LiteralPath $projectCandidate -ErrorAction Stop).Path
$projectDir = Split-Path -Parent $projectPath
$projectName = [System.IO.Path]::GetFileNameWithoutExtension($projectPath)
$platform = if ([System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -eq [System.Runtime.InteropServices.Architecture]::Arm64) { "ARM64" } else { "x64" }
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

$binDir = Join-Path (Join-Path (Join-Path $projectDir "bin") $platform) $configuration
$exePath = Get-ChildItem -LiteralPath $binDir -Recurse -File |
    Where-Object { $_.Name -eq $projectName -or $_.Name -eq "$projectName.exe" } |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1

if (-not $exePath) {
    throw "Build completed but $projectName executable was not found under $binDir."
}

Write-Host "==> Launching $($exePath.FullName)" -ForegroundColor Cyan
if ($Detach) {
    Start-Process -FilePath $exePath.FullName -WorkingDirectory $exePath.DirectoryName
} else {
    & $exePath.FullName
}
