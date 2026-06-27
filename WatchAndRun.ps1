<#
.SYNOPSIS
Watches PicLens source files and reruns BuildAndRun.ps1 after saves.

.DESCRIPTION
This is not XAML Hot Reload. It rebuilds after relevant file changes and lets
BuildAndRun.ps1 handle launch details.

Build and launch output is written to logs/watch-run for later diagnosis.
#>

param(
    [Parameter(Position = 0)]
    [string]$Project = ".\PicLens\PicLens.csproj",
    [int]$DebounceMilliseconds = 900,
    [string]$LogDirectory = ".\logs\watch-run",
    [switch]$NoInitialRun,
    [switch]$RunOnce,
    [switch]$SkipLaunch,
    [switch]$KeepExistingApp,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$ExtraArgs
)

$ErrorActionPreference = "Stop"

$script:RepoRoot = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).ProviderPath }
$script:BuildScript = Join-Path $script:RepoRoot "BuildAndRun.ps1"
$script:LastExitCode = 0
$script:CycleNumber = 0

function Resolve-FullPath {
    param([string]$Path)

    $candidate = if ([System.IO.Path]::IsPathRooted($Path)) { $Path } else { Join-Path $script:RepoRoot $Path }
    return (Resolve-Path -LiteralPath $candidate -ErrorAction Stop).Path
}

try {
    $script:ProjectPath = Resolve-FullPath $Project
} catch {
    Write-Host "ERROR: Project file not found: $Project" -ForegroundColor Red
    exit 2
}

if (-not (Test-Path -LiteralPath $script:BuildScript)) {
    Write-Host "ERROR: BuildAndRun.ps1 was not found at $script:BuildScript" -ForegroundColor Red
    exit 2
}

$script:ProjectBaseName = [System.IO.Path]::GetFileNameWithoutExtension($script:ProjectPath)
$script:LogDirectoryPath = if ([System.IO.Path]::IsPathRooted($LogDirectory)) { $LogDirectory } else { Join-Path $script:RepoRoot $LogDirectory }
New-Item -ItemType Directory -Path $script:LogDirectoryPath -Force | Out-Null
$script:LogFile = Join-Path $script:LogDirectoryPath ("watch-run-{0}.log" -f (Get-Date -Format "yyyyMMdd-HHmmss"))

function Write-WatchLog {
    param(
        [ValidateSet("INFO", "WARN", "ERROR")]
        [string]$Level,
        [string]$Message,
        [ConsoleColor]$Color = [ConsoleColor]::Gray
    )

    $line = "[{0}] [{1}] {2}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss.fff"), $Level, $Message
    Add-Content -LiteralPath $script:LogFile -Value $line
    Write-Host $line -ForegroundColor $Color
}

function Write-CommandOutput {
    param([object[]]$Output)

    foreach ($item in $Output) {
        $line = $item.ToString()
        Add-Content -LiteralPath $script:LogFile -Value $line
        Write-Host $line
    }
}

function Get-PowerShellExecutable {
    try {
        $currentProcess = Get-Process -Id $PID -ErrorAction Stop
        if ($currentProcess.Path -and (Test-Path -LiteralPath $currentProcess.Path)) {
            return $currentProcess.Path
        }
    } catch {
    }

    $pwsh = Get-Command pwsh -ErrorAction SilentlyContinue
    if ($pwsh) { return $pwsh.Source }

    $powershell = Get-Command powershell -ErrorAction SilentlyContinue
    if ($powershell) { return $powershell.Source }

    throw "PowerShell executable not found."
}

function Stop-ExistingApp {
    if ($KeepExistingApp -or $SkipLaunch) {
        return
    }

    $processes = @(Get-Process -Name $script:ProjectBaseName -ErrorAction SilentlyContinue)
    foreach ($process in ($processes | Sort-Object Id -Unique)) {
        try {
            Write-WatchLog -Level "INFO" -Message "Stopping $($process.ProcessName) (PID $($process.Id)) before relaunch." -Color DarkGray
            Stop-Process -Id $process.Id -Force -ErrorAction Stop
            Wait-Process -Id $process.Id -Timeout 5 -ErrorAction SilentlyContinue
        } catch {
            Write-WatchLog -Level "ERROR" -Message "Failed to stop PID $($process.Id): $($_.Exception.Message)" -Color Red
        }
    }
}

function Invoke-RunCycle {
    param(
        [string]$Reason,
        [string[]]$Changes = @()
    )

    $script:CycleNumber++
    Write-WatchLog -Level "INFO" -Message "Cycle #$script:CycleNumber started: $Reason" -Color White
    if ($Changes.Count -gt 0) {
        $preview = ($Changes | Select-Object -First 8) -join ", "
        if ($Changes.Count -gt 8) { $preview += ", ..." }
        Write-WatchLog -Level "INFO" -Message "Changed files: $preview" -Color DarkGray
    }

    Stop-ExistingApp

    $powerShellExe = Get-PowerShellExecutable
    $buildArgs = @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        $script:BuildScript,
        $script:ProjectPath
    )
    if ($SkipLaunch) {
        $buildArgs += "-SkipRun"
    } else {
        $buildArgs += "-Detach"
    }
    $buildArgs += $ExtraArgs

    Write-WatchLog -Level "INFO" -Message "Running BuildAndRun.ps1" -Color Cyan
    $output = & $powerShellExe @buildArgs 2>&1
    $script:LastExitCode = $LASTEXITCODE
    Write-CommandOutput -Output $output

    if ($script:LastExitCode -eq 0) {
        Write-WatchLog -Level "INFO" -Message "Cycle completed." -Color Green
    } else {
        Write-WatchLog -Level "ERROR" -Message "Cycle failed with exit code $script:LastExitCode." -Color Red
    }
}

function Test-RelevantChange {
    param([string]$RelativePath)

    if ([string]::IsNullOrWhiteSpace($RelativePath)) {
        return $true
    }

    $normalized = $RelativePath.Replace("/", "\")
    $segments = $normalized.Split("\", [System.StringSplitOptions]::RemoveEmptyEntries)
    $excludedSegments = @(".git", ".vs", ".nuget", "artifacts", "bin", "obj", "logs", "AppPackages", "BundleArtifacts", "publish", "TestResults")
    foreach ($segment in $segments) {
        if ($excludedSegments -icontains $segment) {
            return $false
        }
    }

    $fileName = [System.IO.Path]::GetFileName($normalized)
    $extension = [System.IO.Path]::GetExtension($normalized)
    $includedNames = @("NuGet.Config", "app.manifest", "PicLens.slnx")
    $includedExtensions = @(".cs", ".axaml", ".csproj", ".props", ".targets", ".json", ".manifest", ".ico", ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".svg")

    return ($includedNames -icontains $fileName) -or ($includedExtensions -icontains $extension)
}

function Start-WatchLoop {
    $watcher = New-Object System.IO.FileSystemWatcher
    $watcher.Path = $script:RepoRoot
    $watcher.Filter = "*.*"
    $watcher.IncludeSubdirectories = $true
    $watcher.NotifyFilter = [System.IO.NotifyFilters]"FileName, DirectoryName, LastWrite, Size, CreationTime"

    Write-WatchLog -Level "INFO" -Message "Watching $script:RepoRoot" -Color Cyan
    Write-WatchLog -Level "INFO" -Message "Log file: $script:LogFile" -Color DarkGray
    Write-WatchLog -Level "INFO" -Message "Press Ctrl+C to stop." -Color DarkGray

    try {
        while ($true) {
            $change = $watcher.WaitForChanged([System.IO.WatcherChangeTypes]::All, 500)
            if ($change.TimedOut) {
                continue
            }

            $changes = New-Object System.Collections.Generic.List[string]
            if (Test-RelevantChange -RelativePath $change.Name) {
                [void]$changes.Add($change.Name)
            }

            $lastEventAt = Get-Date
            while (((Get-Date) - $lastEventAt).TotalMilliseconds -lt $DebounceMilliseconds) {
                $remainingDelay = [Math]::Max(50, $DebounceMilliseconds - [int]((Get-Date) - $lastEventAt).TotalMilliseconds)
                $nextChange = $watcher.WaitForChanged([System.IO.WatcherChangeTypes]::All, $remainingDelay)
                if (-not $nextChange.TimedOut) {
                    $lastEventAt = Get-Date
                    if (Test-RelevantChange -RelativePath $nextChange.Name) {
                        [void]$changes.Add($nextChange.Name)
                    }
                }
            }

            $uniqueChanges = @($changes | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)
            if ($uniqueChanges.Count -gt 0) {
                Invoke-RunCycle -Reason "file change" -Changes $uniqueChanges
            }
        }
    } finally {
        $watcher.Dispose()
    }
}

Write-WatchLog -Level "INFO" -Message "PicLens watch runner started." -Color Cyan
Write-WatchLog -Level "INFO" -Message "Project: $script:ProjectPath" -Color DarkGray

if ($RunOnce -and $NoInitialRun) {
    Write-WatchLog -Level "ERROR" -Message "-RunOnce cannot be combined with -NoInitialRun." -Color Red
    exit 2
}

if (-not $NoInitialRun) {
    Invoke-RunCycle -Reason "initial run"
    if ($RunOnce) {
        exit $script:LastExitCode
    }
}

Start-WatchLoop
