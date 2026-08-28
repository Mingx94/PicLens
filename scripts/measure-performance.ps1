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
$profile = Join-Path $output "profile"
New-Item -ItemType Directory -Force -Path $output | Out-Null
Push-Location $repoRoot
try {
    cargo build --release --locked -p piclens-gpui
    if ($LASTEXITCODE -ne 0) { throw "Release build failed" }
    $executable = Join-Path $repoRoot "target\release\piclens-gpui.exe"
    foreach ($state in @("cold", "warm")) {
        if ($state -eq "cold") { Remove-Item -LiteralPath $profile -Recurse -Force -ErrorAction SilentlyContinue }
        $metrics = Join-Path $output "windows-release-$state.json"
        $process = Start-Process $executable -ArgumentList @(
            "--smoke-ms", "8000", "--performance-scroll", "--include-subfolders",
            "--data-root", $profile, "--folder", $fixture, "--search", "jpg",
            "--metrics", $metrics
        ) -PassThru -WindowStyle Hidden
        if (-not $process.WaitForExit(30000)) { $process.Kill($true); throw "$state metrics run timed out" }
        if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $metrics)) {
            throw "$state metrics run failed"
        }
    }
    $environment = [ordered]@{
        measuredAtUtc = [DateTime]::UtcNow.ToString("o")
        commit = (git rev-parse HEAD)
        gpuiRevision = "c7537bdf463a998e7ec636adff33b198891e69ed"
        os = [Environment]::OSVersion.VersionString
        cpu = (Get-CimInstance Win32_Processor | Select-Object -First 1 -ExpandProperty Name)
        gpu = @((Get-CimInstance Win32_VideoController).Name)
        storage = "Record fixture drive media and cache state with the result"
        fixture = $fixture
        window = "1280x800; also run the minimum 800x600 native smoke"
        displayScale = "Record from Windows display settings"
        thresholds = "No automatic gate until product thresholds are approved"
    }
    $environment | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $output "environment.json") -Encoding utf8
    Write-Host "Release metrics ready: $output"
}
finally { Pop-Location }
