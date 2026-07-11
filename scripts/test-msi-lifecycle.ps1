[CmdletBinding()]
param(
    [string]$MsiPath = "",
    [string]$PreviousMsiPath = "",
    [string]$ExpectedExecutable = "",
    [switch]$ConfirmSystemChanges,
    [switch]$AllowReplacingExistingInstallation
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not $IsWindows) {
    throw "MSI lifecycle testing requires Windows"
}
if (-not $ConfirmSystemChanges) {
    throw "This test installs and uninstalls PicLens. Re-run with -ConfirmSystemChanges after explicit approval."
}
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "MSI lifecycle testing requires an elevated PowerShell process"
}

$qtRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$repoRoot = $qtRoot
if ([string]::IsNullOrWhiteSpace($MsiPath)) {
    $MsiPath = Join-Path $repoRoot "artifacts\installer\PicLens-win-x64.msi"
}
if ([string]::IsNullOrWhiteSpace($ExpectedExecutable)) {
    $ExpectedExecutable = Join-Path $repoRoot "artifacts\qt-portable\PicLens-win-x64\PicLens.exe"
}
$resolvedMsi = (Resolve-Path -LiteralPath $MsiPath).Path
$resolvedExpectedExecutable = (Resolve-Path -LiteralPath $ExpectedExecutable).Path
$resolvedPreviousMsi = if ([string]::IsNullOrWhiteSpace($PreviousMsiPath)) {
    ""
} else {
    (Resolve-Path -LiteralPath $PreviousMsiPath).Path
}

$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot "artifacts\installer-lifecycle"))
$profileRoot = [IO.Path]::GetFullPath((Join-Path $artifactRoot "profile"))
$artifactPrefix = $artifactRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $profileRoot.StartsWith($artifactPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Lifecycle profile must stay below $artifactRoot"
}
$installedExecutable = Join-Path $env:ProgramFiles "PicLens\PicLens.exe"
$shortcutPath = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::CommonPrograms)) "PicLens\PicLens.lnk"
$registryPath = "HKLM:\Software\PicLens"
$existingProducts = @(
    Get-ItemProperty -Path @(
        "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*"
        "HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*"
    ) -ErrorAction SilentlyContinue |
        Where-Object {
            $_.PSObject.Properties["DisplayName"] -and $_.DisplayName -eq "PicLens"
        } |
        Select-Object DisplayName, DisplayVersion, PSChildName
)
if ($existingProducts.Count -gt 0 -and -not $AllowReplacingExistingInstallation) {
    $descriptions = $existingProducts | ForEach-Object {
        "PicLens $($_.DisplayVersion) [$($_.PSChildName)]"
    }
    throw "Existing PicLens installation detected. Lifecycle test refused to replace: $($descriptions -join ', '). Use -AllowReplacingExistingInstallation only with explicit approval."
}

function Write-MsiLogTail([string]$LogPath) {
    if (-not (Test-Path -LiteralPath $LogPath -PathType Leaf)) {
        Write-Host "MSI log was not created: $LogPath"
        return
    }
    Write-Host "--- MSI log tail: $LogPath ---"
    Get-Content -LiteralPath $LogPath -Tail 80 -ErrorAction SilentlyContinue |
        ForEach-Object { Write-Host $_ }
    Write-Host "--- end MSI log tail ---"
}

function Invoke-Msi(
    [string]$Mode,
    [string]$PackagePath,
    [string]$LogName,
    [switch]$Cleanup,
    [int]$TimeoutSeconds = 600
) {
    $logPath = Join-Path $artifactRoot $LogName
    $arguments = @(
        "/$Mode",
        ('"{0}"' -f $PackagePath),
        "/qn",
        "/norestart",
        "/l*v",
        ('"{0}"' -f $logPath)
    )
    $startedAt = Get-Date
    Write-Host "==> [$($startedAt.ToString('o'))] msiexec /$Mode start: $PackagePath"
    Write-Host "    Log: $logPath"
    $process = Start-Process -FilePath "msiexec.exe" -ArgumentList $arguments `
        -PassThru -WindowStyle Hidden
    Write-Host "    PID: $($process.Id); timeout: $TimeoutSeconds seconds"
    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    $nextHeartbeatSeconds = 15
    while (-not $process.WaitForExit(1000)) {
        if ($stopwatch.Elapsed.TotalSeconds -ge $nextHeartbeatSeconds) {
            Write-Host "    msiexec /$Mode PID $($process.Id) still running; elapsed $([Math]::Round($stopwatch.Elapsed.TotalSeconds)) seconds"
            $nextHeartbeatSeconds += 15
        }
        if ($stopwatch.Elapsed.TotalSeconds -ge $TimeoutSeconds) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
            $process.WaitForExit(10000) | Out-Null
            $stopwatch.Stop()
            Write-MsiLogTail $logPath
            throw "msiexec /$Mode timed out after $TimeoutSeconds seconds. Log: $logPath"
        }
    }
    $process.WaitForExit()
    $stopwatch.Stop()
    Write-Host "<== [$((Get-Date).ToString('o'))] msiexec /$Mode exit $($process.ExitCode); elapsed $([Math]::Round($stopwatch.Elapsed.TotalSeconds, 1)) seconds"
    $accepted = if ($Cleanup) { @(0, 1605, 1614, 3010) } else { @(0, 3010) }
    if ($process.ExitCode -notin $accepted) {
        Write-MsiLogTail $logPath
        throw "msiexec /$Mode failed with exit code $($process.ExitCode). Log: $logPath"
    }
}

function Invoke-InstalledSmoke([string]$Label) {
    if (-not (Test-Path -LiteralPath $installedExecutable -PathType Leaf)) {
        throw "$Label executable was not installed: $installedExecutable"
    }
    Write-Host "==> [$((Get-Date).ToString('o'))] $Label packaged launch start"
    $processInfo = [Diagnostics.ProcessStartInfo]::new()
    $processInfo.FileName = $installedExecutable
    $processInfo.Arguments = '--smoke-ms 1500 --data-root "{0}" --folder "{1}"' -f `
        $profileRoot, (Join-Path $repoRoot "assets")
    $processInfo.UseShellExecute = $false
    $processInfo.CreateNoWindow = $true
    $processInfo.Environment["QT_QPA_PLATFORM"] = "offscreen"
    $process = [Diagnostics.Process]::Start($processInfo)
    if (-not $process.WaitForExit(30000)) {
        $process.Kill($true)
        throw "$Label installed-app smoke timed out"
    }
    if ($process.ExitCode -ne 0) {
        throw "$Label installed-app smoke failed with exit code $($process.ExitCode)"
    }
    Write-Host "<== [$((Get-Date).ToString('o'))] $Label packaged launch passed"
}

function Get-ProfileManifest {
    return @(
        Get-ChildItem -LiteralPath $profileRoot -Recurse -File | Sort-Object FullName | ForEach-Object {
            [pscustomobject]@{
                Path = $_.FullName.Substring($profileRoot.Length).TrimStart([IO.Path]::DirectorySeparatorChar)
                Length = $_.Length
                Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
            }
        }
    )
}

New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
$resultPath = Join-Path $artifactRoot "lifecycle-result.txt"
Remove-Item -LiteralPath $resultPath -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $profileRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path (Join-Path $profileRoot "Thumbnails") -Force | Out-Null
$sentinelPath = Join-Path $profileRoot "Thumbnails\installer-profile-sentinel.bin"
[IO.File]::WriteAllText($sentinelPath, "PicLens installer must preserve this profile")

$currentInstalled = $false
$previousInstalled = $false
try {
    if (-not [string]::IsNullOrWhiteSpace($resolvedPreviousMsi)) {
        Invoke-Msi "i" $resolvedPreviousMsi "previous-install.log"
        $previousInstalled = $true
        Invoke-InstalledSmoke "Previous MSI"
    }

    Invoke-Msi "i" $resolvedMsi "current-install.log"
    $currentInstalled = $true
    Invoke-InstalledSmoke "Current MSI"

    $expectedHash = (Get-FileHash -LiteralPath $resolvedExpectedExecutable -Algorithm SHA256).Hash
    $installedHash = (Get-FileHash -LiteralPath $installedExecutable -Algorithm SHA256).Hash
    if ($installedHash -ne $expectedHash) {
        throw "Installed executable does not match the audited Qt portable payload"
    }
    if (-not (Test-Path -LiteralPath $shortcutPath -PathType Leaf)) {
        throw "Per-machine Start Menu shortcut was not installed: $shortcutPath"
    }
    if ((Get-ItemPropertyValue -LiteralPath $registryPath -Name installed) -ne 1) {
        throw "Per-machine PicLens registration was not installed"
    }
    $profileBeforeUninstall = ConvertTo-Json -InputObject @(Get-ProfileManifest) -Compress

    Invoke-Msi "x" $resolvedMsi "current-uninstall.log"
    $currentInstalled = $false
    $previousInstalled = $false

    if (Test-Path -LiteralPath $installedExecutable -PathType Leaf) {
        throw "PicLens.exe remained after uninstall"
    }
    if (Test-Path -LiteralPath $shortcutPath -PathType Leaf) {
        throw "PicLens Start Menu shortcut remained after uninstall"
    }
    if (Test-Path -LiteralPath $registryPath) {
        throw "PicLens machine registration remained after uninstall"
    }
    $profileAfterUninstall = ConvertTo-Json -InputObject @(Get-ProfileManifest) -Compress
    if ($profileAfterUninstall -cne $profileBeforeUninstall) {
        throw "MSI uninstall changed the isolated PicLens user profile"
    }
    if (-not (Test-Path -LiteralPath $sentinelPath -PathType Leaf)) {
        throw "MSI lifecycle removed the user-profile sentinel"
    }

    Write-Host "MSI lifecycle smoke passed"
    Write-Host "  Install/launch/uninstall: passed"
    Write-Host "  Upgrade: $(if ($resolvedPreviousMsi) { 'passed' } else { 'not requested' })"
    Write-Host "  User profile preservation: passed"
    Write-Host "  Logs: $artifactRoot"
    [IO.File]::WriteAllText($resultPath, "PASS`n")
}
catch {
    $details = $_ | Out-String
    [IO.File]::WriteAllText($resultPath, "FAIL`n$details")
    throw
}
finally {
    if ($currentInstalled) {
        Invoke-Msi "x" $resolvedMsi "cleanup-current.log" -Cleanup
    }
    if ($previousInstalled) {
        Invoke-Msi "x" $resolvedPreviousMsi "cleanup-previous.log" -Cleanup
    }
}
