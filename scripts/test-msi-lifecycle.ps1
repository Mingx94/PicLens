[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$MsiPath,
    [switch]$ConfirmSystemChanges
)
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
if (-not $IsWindows) { throw "MSI lifecycle testing requires Windows" }
if (-not $ConfirmSystemChanges) { throw "Pass -ConfirmSystemChanges after explicit approval" }
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$resolvedMsi = (Resolve-Path -LiteralPath $MsiPath).Path
$artifactRoot = Join-Path $repoRoot "artifacts\msi-lifecycle"
$profileRoot = Join-Path $artifactRoot "profile"
$installedExecutable = Join-Path $env:ProgramFiles "PicLens\PicLens.exe"
New-Item -ItemType Directory -Force -Path $profileRoot | Out-Null
$sentinel = Join-Path $profileRoot "profile-preservation.txt"
[IO.File]::WriteAllText($sentinel, "preserve")

function Invoke-Msi([string]$Mode, [string]$Label) {
    $log = Join-Path $artifactRoot "$Label.log"
    $process = Start-Process msiexec.exe -ArgumentList @("/$Mode", "`"$resolvedMsi`"", "/qn", "/norestart", "/l*v", "`"$log`"") -PassThru -WindowStyle Hidden
    if (-not $process.WaitForExit(600000)) { Stop-Process -Id $process.Id -Force; throw "$Label timed out" }
    if ($process.ExitCode -notin @(0, 3010)) { throw "$Label failed with exit code $($process.ExitCode)" }
}

$installed = $false
try {
    Invoke-Msi i install
    $installed = $true
    if (-not (Test-Path -LiteralPath $installedExecutable -PathType Leaf)) { throw "PicLens.exe was not installed" }
    $smoke = Start-Process $installedExecutable -ArgumentList @("--smoke-ms", "1500", "--data-root", $profileRoot, "--folder", (Join-Path $repoRoot "assets")) -PassThru -WindowStyle Hidden
    if (-not $smoke.WaitForExit(30000) -or $smoke.ExitCode -ne 0) { throw "Installed app launch failed" }
    Invoke-Msi i replace
    Invoke-Msi x uninstall
    $installed = false
    if (Test-Path -LiteralPath $installedExecutable) { throw "PicLens.exe remained after uninstall" }
    if (-not (Test-Path -LiteralPath $sentinel)) { throw "Uninstall removed the isolated user profile" }
    Write-Host "MSI install, launch, replace, uninstall, and profile preservation passed"
}
finally {
    if ($installed) {
        Start-Process msiexec.exe -ArgumentList @("/x", "`"$resolvedMsi`"", "/qn", "/norestart") -Wait -WindowStyle Hidden
    }
}
