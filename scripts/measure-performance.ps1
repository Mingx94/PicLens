[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$FolderPath,
    [string]$OutputDirectory = ""
)
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$fixture = (Resolve-Path -LiteralPath $FolderPath).Path
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts\performance"
}
$output = [IO.Path]::GetFullPath($OutputDirectory)
$galleryProfile = Join-Path $output "gallery-profile"
$viewerProfile = Join-Path $output "viewer-profile"
$viewerImage = Get-ChildItem -LiteralPath $fixture -File -Recurse |
    Where-Object Extension -in @(".jpg", ".jpeg", ".png", ".bmp", ".webp", ".gif") |
    Select-Object -First 1
if (-not $viewerImage) { throw "The fixture does not contain a supported image" }
New-Item -ItemType Directory -Force -Path $output | Out-Null
Push-Location $repoRoot
try {
    cargo build --release --locked -p piclens-desktop
    if ($LASTEXITCODE -ne 0) { throw "Release build failed" }
    $executable = Join-Path $repoRoot "target\release\piclens-desktop.exe"
    foreach ($state in @("cold", "warm")) {
        if ($state -eq "cold") {
            Remove-Item -LiteralPath $galleryProfile, $viewerProfile -Recurse -Force -ErrorAction SilentlyContinue
        }
        $galleryMetrics = Join-Path $output "windows-release-gallery-$state.json"
        $galleryScreenshot = Join-Path $output "windows-release-gallery-$state.png"
        $galleryProcess = Start-Process $executable -ArgumentList @(
            "--smoke-ms", "8000", "--performance-scroll", "--include-subfolders",
            "--data-root", $galleryProfile, "--folder", $fixture, "--search", "jpg",
            "--metrics", $galleryMetrics, "--screenshot", $galleryScreenshot
        ) -PassThru
        if (-not $galleryProcess.WaitForExit(30000)) { $galleryProcess.Kill($true); throw "$state gallery run timed out" }
        if ($galleryProcess.ExitCode -ne 0 -or
            -not (Test-Path -LiteralPath $galleryMetrics) -or
            -not (Test-Path -LiteralPath $galleryScreenshot)) {
            throw "$state gallery run failed"
        }
        $galleryData = Get-Content -LiteralPath $galleryMetrics -Raw | ConvertFrom-Json
        if ($galleryData.schemaVersion -ne 2 -or
            $null -eq $galleryData.searchMilliseconds -or
            $null -eq $galleryData.continuousScrollMilliseconds) {
            throw "$state gallery metrics are incomplete"
        }

        $viewerMetrics = Join-Path $output "windows-release-viewer-$state.json"
        $viewerScreenshot = Join-Path $output "windows-release-viewer-$state.png"
        $viewerProcess = Start-Process $executable -ArgumentList @(
            "--smoke-ms", "8000", "--include-subfolders",
            "--data-root", $viewerProfile, "--folder", $fixture, "--viewer", $viewerImage.FullName,
            "--metrics", $viewerMetrics, "--screenshot", $viewerScreenshot
        ) -PassThru
        if (-not $viewerProcess.WaitForExit(30000)) { $viewerProcess.Kill($true); throw "$state viewer run timed out" }
        if ($viewerProcess.ExitCode -ne 0 -or
            -not (Test-Path -LiteralPath $viewerMetrics) -or
            -not (Test-Path -LiteralPath $viewerScreenshot)) {
            throw "$state viewer run failed"
        }
        $viewerData = Get-Content -LiteralPath $viewerMetrics -Raw | ConvertFrom-Json
        if ($viewerData.schemaVersion -ne 2 -or
            $null -eq $viewerData.viewerOpenMilliseconds -or
            $null -eq $viewerData.viewerSharpPaintMilliseconds) {
            throw "$state viewer metrics are incomplete"
        }
    }
    $fixtureDrive = [IO.Path]::GetPathRoot($fixture).TrimEnd("\")
    $logicalDisk = Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='$fixtureDrive'"
    $partition = Get-CimAssociatedInstance -InputObject $logicalDisk -Association Win32_LogicalDiskToPartition |
        Select-Object -First 1
    $disk = Get-CimAssociatedInstance -InputObject $partition -Association Win32_DiskDriveToDiskPartition |
        Select-Object -First 1
    $galleryData = Get-Content -LiteralPath (Join-Path $output "windows-release-gallery-cold.json") -Raw |
        ConvertFrom-Json
    $environment = [ordered]@{
        measuredAtUtc = [DateTime]::UtcNow.ToString("o")
        commit = (git rev-parse HEAD)
        frontend = "eframe-egui-wgpu"
        os = [Environment]::OSVersion.VersionString
        cpu = (Get-CimInstance Win32_Processor | Select-Object -First 1 -ExpandProperty Name)
        gpu = @(Get-CimInstance Win32_VideoController | Select-Object Name, DriverVersion, AdapterRAM)
        storage = [ordered]@{
            drive = $fixtureDrive
            fileSystem = $logicalDisk.FileSystem
            volumeName = $logicalDisk.VolumeName
            model = $disk.Model
            mediaType = $disk.MediaType
            interfaceType = $disk.InterfaceType
        }
        fixture = $fixture
        fixtureImage = $viewerImage.FullName
        window = $galleryData.windowSize
        displayScale = $galleryData.displayScale
        thresholds = "No automatic gate until product thresholds are approved"
    }
    $environment | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $output "environment.json") -Encoding utf8
    Write-Host "Release metrics ready: $output"
}
finally { Pop-Location }
