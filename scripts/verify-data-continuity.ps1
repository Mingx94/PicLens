[CmdletBinding()]
param(
    [string]$SourceProfile = "",
    [string]$ImageFolder = "",
    [string]$Executable = "",
    [switch]$KeepWorkingCopy
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$qtRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$repoRoot = $qtRoot
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot "artifacts\data-migration"))
$workingRoot = [IO.Path]::GetFullPath((Join-Path $artifactRoot "profile-copy"))
$artifactPrefix = $artifactRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $workingRoot.StartsWith($artifactPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Working profile must stay below $artifactRoot"
}
if ([string]::IsNullOrWhiteSpace($Executable)) {
    $Executable = Join-Path $qtRoot "build\release\bin\PicLens.exe"
}
if ([string]::IsNullOrWhiteSpace($ImageFolder)) {
    $ImageFolder = Join-Path $repoRoot "assets"
}
$resolvedExecutable = (Resolve-Path -LiteralPath $Executable).Path
$resolvedImageFolder = (Resolve-Path -LiteralPath $ImageFolder).Path

function Get-TreeManifest([string]$Root) {
    if (-not (Test-Path -LiteralPath $Root -PathType Container)) {
        return @()
    }
    $resolvedRoot = [IO.Path]::GetFullPath($Root).TrimEnd([IO.Path]::DirectorySeparatorChar)
    return @(
        Get-ChildItem -LiteralPath $resolvedRoot -Recurse -File | Sort-Object FullName | ForEach-Object {
            [pscustomobject]@{
                Path = $_.FullName.Substring($resolvedRoot.Length).TrimStart([IO.Path]::DirectorySeparatorChar)
                Length = $_.Length
                Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
            }
        }
    )
}

function Assert-ManifestsEqual($Before, $After, [string]$Description) {
    $beforeJson = ConvertTo-Json -InputObject @($Before) -Compress
    $afterJson = ConvertTo-Json -InputObject @($After) -Compress
    if ($beforeJson -cne $afterJson) {
        throw "$Description changed during the Qt continuity smoke"
    }
}

New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
Remove-Item -LiteralPath $workingRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $workingRoot -Force | Out-Null

$sourceBefore = @()
$resolvedSourceProfile = ""
if (-not [string]::IsNullOrWhiteSpace($SourceProfile)) {
    $resolvedSourceProfile = (Resolve-Path -LiteralPath $SourceProfile).Path
    if ([IO.Path]::GetFullPath($resolvedSourceProfile).TrimEnd([IO.Path]::DirectorySeparatorChar) -eq
        $workingRoot.TrimEnd([IO.Path]::DirectorySeparatorChar)) {
        throw "SourceProfile cannot be the disposable working profile"
    }
    $sourceBefore = Get-TreeManifest $resolvedSourceProfile
    Get-ChildItem -LiteralPath $resolvedSourceProfile -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $workingRoot -Recurse -Force
    }
}

$settingsPath = Join-Path $workingRoot "piclens-settings.json"
$logsRoot = Join-Path $workingRoot "Logs"
$thumbnailRoot = Join-Path $workingRoot "Thumbnails"
New-Item -ItemType Directory -Path $logsRoot,$thumbnailRoot -Force | Out-Null
$seedSettings = [ordered]@{
    lastFolderPath = $resolvedImageFolder
    sort = [ordered]@{ key = 1; direction = 1 }
    includeSubfolders = $true
    thumbnailSize = 240
}
$seedSettings | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $settingsPath -Encoding utf8NoBOM
$sentinelPath = Join-Path $thumbnailRoot "pre-cutover-sentinel.png"
[IO.File]::WriteAllBytes($sentinelPath, [byte[]](0x50,0x69,0x63,0x4c,0x65,0x6e,0x73))

$imageBefore = Get-TreeManifest $resolvedImageFolder
$metricsPath = Join-Path $artifactRoot "profile-continuity.json"
Remove-Item -LiteralPath $metricsPath -Force -ErrorAction SilentlyContinue
$processInfo = [Diagnostics.ProcessStartInfo]::new()
$processInfo.FileName = $resolvedExecutable
$processInfo.Arguments = '--smoke-ms 8000 --data-root "{0}" --metrics "{1}"' -f $workingRoot, $metricsPath
$processInfo.UseShellExecute = $false
$processInfo.CreateNoWindow = $true
$processInfo.Environment["QT_QPA_PLATFORM"] = "offscreen"
$process = [Diagnostics.Process]::Start($processInfo)
if (-not $process.WaitForExit(30000)) {
    $process.Kill($true)
    throw "Qt profile continuity smoke timed out"
}
if ($process.ExitCode -ne 0) {
    throw "Qt profile continuity smoke failed with exit code $($process.ExitCode)"
}
if (-not (Test-Path -LiteralPath $metricsPath -PathType Leaf)) {
    throw "Qt did not write profile continuity metrics"
}

$metrics = Get-Content -LiteralPath $metricsPath -Raw | ConvertFrom-Json
if (-not $metrics.includeSubfolders -or $metrics.sortKey -ne 1 -or
    $metrics.sortDirection -ne 1 -or $metrics.thumbnailSize -ne 240) {
    throw "Qt did not restore the Avalonia settings schema from the copied profile"
}
if ($metrics.rowCount -le 0 -or $metrics.imageCount -le 0) {
    throw "Qt did not restore and scan the copied profile folder"
}
$persisted = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
if (-not $persisted.includeSubfolders -or $persisted.sort.key -ne 1 -or
    $persisted.sort.direction -ne 1 -or $persisted.thumbnailSize -ne 240) {
    throw "Qt rewrote the shared settings contract incompatibly"
}
if (-not (Test-Path -LiteralPath $sentinelPath -PathType Leaf)) {
    throw "Qt removed the pre-cutover thumbnail-cache sentinel"
}
Assert-ManifestsEqual $imageBefore (Get-TreeManifest $resolvedImageFolder) "Source image tree"
if (-not [string]::IsNullOrWhiteSpace($resolvedSourceProfile)) {
    Assert-ManifestsEqual $sourceBefore (Get-TreeManifest $resolvedSourceProfile) "Original profile"
}

Write-Host "Qt profile continuity smoke passed"
Write-Host "  Profile copy: $workingRoot"
Write-Host "  Images:       $($metrics.imageCount)"
Write-Host "  Settings:     modified-time descending, recursive, 240 px"
Write-Host "  Metrics:      $metricsPath"
if (-not $KeepWorkingCopy) {
    Remove-Item -LiteralPath $workingRoot -Recurse -Force
}
