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

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
if ([string]::IsNullOrWhiteSpace($BuildDirectory)) {
    $BuildDirectory = Join-Path $repoRoot "build\release"
}
$executable = Join-Path ([IO.Path]::GetFullPath($BuildDirectory)) "bin\PicLens.exe"
$resolvedFolder = (Resolve-Path -LiteralPath $FolderPath).Path
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw "Release executable was not found: $executable"
}

$outputRoot = Join-Path $repoRoot "artifacts\performance"
$metricsPath = Join-Path $outputRoot "windows-release.json"
$warmMetricsPath = Join-Path $outputRoot "windows-release-warm.json"
$dataRoot = Join-Path $outputRoot ".data"
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
Remove-Item -LiteralPath $metricsPath -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $warmMetricsPath -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $dataRoot -Recurse -Force -ErrorAction SilentlyContinue

function Invoke-Measurement([string]$OutputPath) {
    $processInfo = [Diagnostics.ProcessStartInfo]::new()
    $processInfo.FileName = $executable
    $processInfo.Arguments = '--smoke-ms 8000 --performance-scroll --include-subfolders --data-root "{0}" --folder "{1}" --metrics "{2}"' -f `
        $dataRoot, $resolvedFolder, $OutputPath
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
    if (-not (Test-Path -LiteralPath $OutputPath -PathType Leaf)) {
        throw "Performance metrics were not produced: $OutputPath"
    }
    return Get-Content -LiteralPath $OutputPath -Raw | ConvertFrom-Json
}

function Assert-Measurement($Metrics, [string]$CacheState) {
    if ($Metrics.rowCount -le 0 -or $Metrics.imageCount -le 0) {
        throw "$CacheState performance fixture did not load any images"
    }
    if ($Metrics.elapsedMilliseconds -gt $MaximumElapsedMilliseconds) {
        throw "$CacheState performance elapsed threshold exceeded: $($Metrics.elapsedMilliseconds) ms"
    }
    if ($Metrics.peakWorkingSetBytes -le 0 -or
        $Metrics.peakWorkingSetBytes -gt $MaximumPeakWorkingSetBytes) {
        throw "$CacheState performance peak working-set threshold exceeded: $($Metrics.peakWorkingSetBytes) bytes"
    }
    if ($Metrics.libraryReadyMilliseconds -le 0 -or
        $Metrics.firstThumbnailMilliseconds -le 0 -or
        $Metrics.completedThumbnailRequests -le 0 -or
        $Metrics.processCpuMilliseconds -le 0 -or
        $Metrics.logicalProcessorCount -le 0 -or
        $Metrics.maxConcurrentThumbnailRequests -le 0 -or
        $Metrics.renderFrameSampleCount -le 0 -or
        [string]::IsNullOrWhiteSpace([string]$Metrics.graphicsApi)) {
        throw "$CacheState performance diagnostics are incomplete"
    }
}

$metrics = Invoke-Measurement $metricsPath
$warmMetrics = Invoke-Measurement $warmMetricsPath
Assert-Measurement $metrics "Cold-cache"
Assert-Measurement $warmMetrics "Warm-cache"

Remove-Item -LiteralPath $dataRoot -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "Windows Release performance smoke passed"
Write-Host "  Rows:               $($metrics.rowCount)"
Write-Host "  Images:             $($metrics.imageCount)"
Write-Host "  Cold elapsed:       $($metrics.elapsedMilliseconds) ms"
Write-Host "  Warm elapsed:       $($warmMetrics.elapsedMilliseconds) ms"
Write-Host "  First thumbnail:    $($metrics.firstThumbnailMilliseconds) ms"
Write-Host "  Warm cache hits:    $($warmMetrics.thumbnailCacheHits)"
Write-Host "  Thumbnail workers:  $($metrics.maxConcurrentThumbnailRequests)"
Write-Host "  Thumbnail rate:     $([math]::Round($metrics.thumbnailThroughputPerSecond, 1))/s"
Write-Host "  Average CPU:        $([math]::Round($metrics.averageCpuUtilizationPercent, 1))%"
Write-Host "  Renderer:           $($metrics.graphicsApi)"
Write-Host "  Frame interval p95: $($metrics.renderFrameIntervalP95Milliseconds) ms"
Write-Host "  RSS:                $($metrics.workingSetBytes) bytes"
Write-Host "  Peak:               $($metrics.peakWorkingSetBytes) bytes"
Write-Host "  Cold metrics:       $metricsPath"
Write-Host "  Warm metrics:       $warmMetricsPath"
