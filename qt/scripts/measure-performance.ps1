[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$FolderPath,
    [string]$BuildDirectory = "",
    [int]$MaximumElapsedMilliseconds = 5000,
    [int64]$MaximumPeakWorkingSetBytes = 536870912
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$qtRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$repoRoot = [IO.Path]::GetFullPath((Join-Path $qtRoot ".."))
if ([string]::IsNullOrWhiteSpace($BuildDirectory)) {
    $BuildDirectory = Join-Path $qtRoot "build\release"
}
$executable = Join-Path ([IO.Path]::GetFullPath($BuildDirectory)) "bin\PicLens.exe"
$resolvedFolder = (Resolve-Path -LiteralPath $FolderPath).Path
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw "Release executable was not found: $executable"
}

$outputRoot = Join-Path $repoRoot "artifacts\performance"
$metricsPath = Join-Path $outputRoot "windows-release.json"
$dataRoot = Join-Path $outputRoot ".data"
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
Remove-Item -LiteralPath $metricsPath -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $dataRoot -Recurse -Force -ErrorAction SilentlyContinue

$processInfo = [Diagnostics.ProcessStartInfo]::new()
$processInfo.FileName = $executable
$processInfo.Arguments = '--smoke-ms 8000 --include-subfolders --data-root "{0}" --folder "{1}" --metrics "{2}"' -f `
    $dataRoot, $resolvedFolder, $metricsPath
$processInfo.UseShellExecute = $false
$processInfo.CreateNoWindow = $true
$processInfo.Environment["QT_QPA_PLATFORM"] = "offscreen"
$process = [Diagnostics.Process]::Start($processInfo)
if (-not $process.WaitForExit(30000)) {
    $process.Kill($true)
    throw "Performance smoke timed out"
}
if ($process.ExitCode -ne 0) {
    throw "Performance smoke failed with exit code $($process.ExitCode)"
}
if (-not (Test-Path -LiteralPath $metricsPath -PathType Leaf)) {
    throw "Performance metrics were not produced: $metricsPath"
}

$metrics = Get-Content -LiteralPath $metricsPath -Raw | ConvertFrom-Json
if ($metrics.rowCount -le 0 -or $metrics.imageCount -le 0) {
    throw "Performance fixture did not load any images"
}
if ($metrics.elapsedMilliseconds -gt $MaximumElapsedMilliseconds) {
    throw "Performance elapsed threshold exceeded: $($metrics.elapsedMilliseconds) ms"
}
if ($metrics.peakWorkingSetBytes -le 0 -or
    $metrics.peakWorkingSetBytes -gt $MaximumPeakWorkingSetBytes) {
    throw "Performance peak working-set threshold exceeded: $($metrics.peakWorkingSetBytes) bytes"
}

Remove-Item -LiteralPath $dataRoot -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "Windows Release performance smoke passed"
Write-Host "  Rows:    $($metrics.rowCount)"
Write-Host "  Images:  $($metrics.imageCount)"
Write-Host "  Elapsed: $($metrics.elapsedMilliseconds) ms"
Write-Host "  RSS:     $($metrics.workingSetBytes) bytes"
Write-Host "  Peak:    $($metrics.peakWorkingSetBytes) bytes"
Write-Host "  Metrics: $metricsPath"
