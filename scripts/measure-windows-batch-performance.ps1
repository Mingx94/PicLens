[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$SourcePng,
    [ValidateRange(1, 49)][int]$Copies = 49,
    [string]$OutputDirectory = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$source = (Resolve-Path -LiteralPath $SourcePng).Path
if ([IO.Path]::GetExtension($source) -ine ".png") {
    throw "SourcePng must identify a PNG file"
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $stamp = [DateTime]::UtcNow.ToString("yyyyMMdd-HHmmss")
    $OutputDirectory = Join-Path $repoRoot "artifacts\windows-batch-performance-$stamp"
}
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) {
    throw "OutputDirectory already exists: $output"
}

$profileRoot = Join-Path $output "profile"
$fixture = Join-Path $profileRoot "fixture"
$metricsPath = Join-Path $output "metrics.json"
$screenshotPath = Join-Path $output "screenshot.png"
$environmentPath = Join-Path $output "environment.json"
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
for ($index = 1; $index -le $Copies; $index++) {
    Copy-Item -LiteralPath $source -Destination (Join-Path $fixture ("image-{0:d3}.png" -f $index))
}

Push-Location $repoRoot
try {
    cargo build --release --locked -p piclens-desktop
    if ($LASTEXITCODE -ne 0) { throw "Release build failed" }

    $executable = Join-Path $repoRoot "target\release\piclens-desktop.exe"
    $logPath = Join-Path $profileRoot "Logs\PicLens.log"
    $process = Start-Process $executable -ArgumentList @(
        "--folder", ('"{0}"' -f $fixture),
        "--data-root", ('"{0}"' -f $profileRoot),
        "--performance-batch-jpg",
        "--metrics", ('"{0}"' -f $metricsPath),
        "--screenshot", ('"{0}"' -f $screenshotPath),
        "--smoke-ms", "12000"
    ) -PassThru

    $startDeadline = [DateTime]::UtcNow.AddSeconds(10)
    while ([DateTime]::UtcNow -lt $startDeadline -and
        -not $process.HasExited -and
        (-not (Test-Path -LiteralPath $logPath) -or
            -not (Select-String -LiteralPath $logPath -Pattern 'performance JPG batch started' -Quiet))) {
        Start-Sleep -Milliseconds 50
        $process.Refresh()
    }
    if ($process.HasExited -or
        -not (Test-Path -LiteralPath $logPath) -or
        -not (Select-String -LiteralPath $logPath -Pattern 'performance JPG batch started' -Quiet)) {
        if (-not $process.HasExited) { $process.Kill($true) }
        throw "Batch performance workload did not start"
    }

    $process.Refresh()
    $batchCpuStarted = $process.TotalProcessorTime
    $batchWindowStarted = [DateTime]::UtcNow
    $gpuCounterError = $null
    $gpuCounterSamples = @()
    $batchDeadline = [DateTime]::UtcNow.AddSeconds(30)
    while ([DateTime]::UtcNow -lt $batchDeadline -and
        -not $process.HasExited -and
        -not (Select-String -LiteralPath $logPath -Pattern 'performance JPG batch completed' -Quiet)) {
        if ($null -eq $gpuCounterError) {
            try {
                $counterSet = Get-Counter '\GPU Engine(*)\Utilization Percentage' -MaxSamples 1
                $gpuCounterSamples += $counterSet.CounterSamples
            }
            catch {
                $gpuCounterError = $_.Exception.Message
            }
        }
        Start-Sleep -Milliseconds 100
        $process.Refresh()
    }
    $batchWindowEnded = [DateTime]::UtcNow
    $process.Refresh()
    $batchCpuEnded = $process.TotalProcessorTime
    if ($process.HasExited -or
        -not (Select-String -LiteralPath $logPath -Pattern 'performance JPG batch completed' -Quiet)) {
        if (-not $process.HasExited) { $process.Kill($true) }
        throw "Batch performance workload did not complete"
    }

    if (-not $process.WaitForExit(30000)) {
        $process.Kill($true)
        throw "Batch performance run timed out"
    }
    $process.Refresh()
    if ($process.ExitCode -ne 0) { throw "Batch performance run failed with exit code $($process.ExitCode)" }
    if (-not (Test-Path -LiteralPath $metricsPath) -or -not (Test-Path -LiteralPath $screenshotPath)) {
        throw "Batch performance run did not write metrics and screenshot evidence"
    }

    $metrics = Get-Content -LiteralPath $metricsPath -Raw | ConvertFrom-Json
    if ($metrics.schemaVersion -ne 5 -or
        $null -eq $metrics.batchOperationMilliseconds -or
        $metrics.batchTotal -ne $Copies -or
        $metrics.batchSucceeded -ne $Copies -or
        $metrics.batchSkipped -ne 0 -or
        $metrics.batchCanceled -ne 0 -or
        $metrics.batchFailed -ne 0) {
        throw "Batch metrics are incomplete or report an unsuccessful operation"
    }

    $pngCount = @(Get-ChildItem -LiteralPath $fixture -File -Filter '*.png').Count
    $jpgCount = @(Get-ChildItem -LiteralPath $fixture -File -Filter '*.jpg').Count
    if ($pngCount -ne $Copies -or $jpgCount -ne $Copies) {
        throw "The conversion fixture does not contain the expected preserved PNG and generated JPG files"
    }

    if (-not (Test-Path -LiteralPath $logPath)) { throw "The isolated app log is missing" }
    $badLogLines = @(Select-String -LiteralPath $logPath -Pattern '\[(WARN|ERROR)\]|panic')
    if ($badLogLines.Count -ne 0) { throw "The isolated app log contains warnings, errors, or a panic" }

    $processCounterToken = "pid_$($process.Id)_"
    $processGpuValues = @(
        $gpuCounterSamples |
            Where-Object { $_.Path.ToLowerInvariant().Contains($processCounterToken) } |
            ForEach-Object { [double]$_.CookedValue }
    )
    $gpuNonZero = @($processGpuValues | Where-Object { $_ -gt 0.0 })
    $gpuMaximum = if ($processGpuValues.Count -eq 0) { $null } else {
        ($processGpuValues | Measure-Object -Maximum).Maximum
    }
    $batchWindowSeconds = ($batchWindowEnded - $batchWindowStarted).TotalSeconds
    $externalCpuPercent = if ($batchWindowSeconds -le 0) { 0.0 } else {
        ($batchCpuEnded - $batchCpuStarted).TotalSeconds / $batchWindowSeconds * 100.0
    }

    $environment = [ordered]@{
        measuredAtUtc = [DateTime]::UtcNow.ToString("o")
        commit = (git rev-parse HEAD)
        workingTreeChanged = [bool](git status --short)
        frontend = "eframe-egui-wgpu"
        os = [Environment]::OSVersion.VersionString
        cpu = (Get-CimInstance Win32_Processor | Select-Object -First 1 -ExpandProperty Name)
        gpu = @(Get-CimInstance Win32_VideoController | Select-Object Name, DriverVersion, AdapterRAM)
        sourcePng = $source
        sourceSha256 = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
        copies = $Copies
        fixture = $fixture
        externalCpuWindowMilliseconds = [Math]::Round($batchWindowSeconds * 1000.0)
        externalCpuWindowPercent = [Math]::Round($externalCpuPercent, 2)
        gpuCounterError = $gpuCounterError
        gpuProcessSampleCount = $processGpuValues.Count
        gpuProcessNonZeroSampleCount = $gpuNonZero.Count
        gpuProcessMaximumPercent = if ($null -eq $gpuMaximum) { $null } else { [Math]::Round($gpuMaximum, 2) }
        metrics = $metrics
        thresholds = "No automatic gate until product thresholds are approved"
    }
    $environment | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $environmentPath -Encoding utf8
    Write-Host "Windows batch performance evidence ready: $output"
}
finally {
    Pop-Location
}
