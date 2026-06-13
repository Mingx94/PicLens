<#
.SYNOPSIS
Watches PicLens source files and relaunches the WinUI app after saves.

.DESCRIPTION
This is not XAML Hot Reload. It provides a fast edit loop for non-Visual Studio
development by rebuilding after relevant file changes and relaunching the app
only when the build succeeds.

Build and launch output is written to logs/watch-run for later diagnosis.

.EXAMPLE
.\WatchAndRun.ps1 .\PicLens\PicLens.csproj

.EXAMPLE
.\WatchAndRun.ps1 .\PicLens\PicLens.csproj /p:Configuration=Debug /p:Platform=x64
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
$script:CurrentAppProcessId = $null
$script:LastExitCode = 0
$script:CycleNumber = 0

function Resolve-FullPath {
    param([string]$Path)

    $candidate = if ([System.IO.Path]::IsPathRooted($Path)) {
        $Path
    } else {
        Join-Path $script:RepoRoot $Path
    }

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
$script:LogDirectoryPath = if ([System.IO.Path]::IsPathRooted($LogDirectory)) {
    $LogDirectory
} else {
    Join-Path $script:RepoRoot $LogDirectory
}
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

function Get-RunSettings {
    $platform = if ($env:PROCESSOR_ARCHITECTURE -eq "ARM64") { "ARM64" } else { "x64" }
    $configuration = "Debug"

    foreach ($arg in $ExtraArgs) {
        if ($arg -match "^[/-]p:Platform=(.+)$") {
            $platform = $Matches[1]
        } elseif ($arg -match "^[/-]p:Configuration=(.+)$") {
            $configuration = $Matches[1]
        }
    }

    [pscustomobject]@{
        Platform = $platform
        Configuration = $configuration
        RuntimeIdentifier = "win-$($platform.ToLowerInvariant())"
    }
}

function Resolve-OutputDirectory {
    param([pscustomobject]$RunSettings)

    $projectDir = Split-Path -Parent $script:ProjectPath
    $binDir = Join-Path $projectDir ("bin\{0}\{1}" -f $RunSettings.Platform, $RunSettings.Configuration)
    if (-not (Test-Path -LiteralPath $binDir)) {
        throw "Build output folder not found: $binDir"
    }

    $tfmDir = Get-ChildItem -LiteralPath $binDir -Directory |
        Where-Object { $_.Name -match "^net\d" } |
        Sort-Object Name -Descending |
        Select-Object -First 1

    if (-not $tfmDir) {
        throw "No target framework output folder found in $binDir"
    }

    $ridOutputDir = Join-Path $tfmDir.FullName $RunSettings.RuntimeIdentifier
    if (Test-Path -LiteralPath $ridOutputDir) {
        return $ridOutputDir
    }

    return $tfmDir.FullName
}

function Update-AppxLayoutIfNeeded {
    param([string]$OutputDirectory)

    $expectedRecipeName = "$script:ProjectBaseName.build.appxrecipe"
    $staleRecipeFiles = Get-ChildItem -LiteralPath $OutputDirectory -Filter "*.build.appxrecipe" -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -ne $expectedRecipeName }
    $shouldRefreshAppxLayout = $false

    foreach ($recipeFile in $staleRecipeFiles) {
        Remove-Item -LiteralPath $recipeFile.FullName -Force -ErrorAction SilentlyContinue
        $shouldRefreshAppxLayout = $true
    }

    $sourceManifest = Join-Path $OutputDirectory "AppxManifest.xml"
    $layoutManifest = Join-Path $OutputDirectory "AppX\appxmanifest.xml"
    if ((Test-Path -LiteralPath $sourceManifest) -and (Test-Path -LiteralPath $layoutManifest)) {
        $sourceManifestContent = Get-Content -LiteralPath $sourceManifest -Raw
        $layoutManifestContent = Get-Content -LiteralPath $layoutManifest -Raw
        if ($sourceManifestContent -ne $layoutManifestContent) {
            $shouldRefreshAppxLayout = $true
        }
    }

    if ($shouldRefreshAppxLayout) {
        $appxLayoutDir = Join-Path $OutputDirectory "AppX"
        if (Test-Path -LiteralPath $appxLayoutDir) {
            Remove-Item -LiteralPath $appxLayoutDir -Recurse -Force
            Write-WatchLog -Level "INFO" -Message "Removed stale AppX layout." -Color DarkGray
        }
    }
}

function Invoke-WatchedBuild {
    $powerShellExe = Get-PowerShellExecutable
    $psArgs = @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        $script:BuildScript,
        $script:ProjectPath,
        "-SkipRun"
    ) + $ExtraArgs

    Write-WatchLog -Level "INFO" -Message "Building with BuildAndRun.ps1 -SkipRun" -Color Cyan
    $output = & $powerShellExe @psArgs 2>&1
    $exitCode = $LASTEXITCODE
    Write-CommandOutput -Output $output

    if ($exitCode -ne 0) {
        Write-WatchLog -Level "ERROR" -Message "Build failed with exit code $exitCode. Existing app instance was left running." -Color Red
        return $false
    }

    Write-WatchLog -Level "INFO" -Message "Build succeeded." -Color Green
    return $true
}

function Get-ProcessFromLaunchOutput {
    param([object[]]$Output)

    foreach ($item in $Output) {
        $line = $item.ToString()
        if ($line -match '(?i)"(?:pid|processId|process_id)"\s*:\s*(\d+)') {
            return [int]$Matches[1]
        }

        if ($line -match "(?i)\bPID\b\s*:?\s*(\d+)") {
            return [int]$Matches[1]
        }
    }

    return $null
}

function Stop-ExistingApp {
    if ($KeepExistingApp) {
        Write-WatchLog -Level "INFO" -Message "Keeping existing app instance because -KeepExistingApp was specified." -Color DarkGray
        return
    }

    $processes = @()
    if ($script:CurrentAppProcessId) {
        $process = Get-Process -Id $script:CurrentAppProcessId -ErrorAction SilentlyContinue
        if ($process) { $processes += $process }
    }

    if ($processes.Count -eq 0) {
        $processes = @(Get-Process -Name $script:ProjectBaseName -ErrorAction SilentlyContinue)
    }

    if ($processes.Count -eq 0) {
        return
    }

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

function Find-LatestAppProcessId {
    try {
        $process = Get-Process -Name $script:ProjectBaseName -ErrorAction SilentlyContinue |
            Sort-Object StartTime -Descending |
            Select-Object -First 1

        if ($process) { return [int]$process.Id }
    } catch {
    }

    return $null
}

function Start-WatchedApp {
    param([string]$OutputDirectory)

    if ($SkipLaunch) {
        Write-WatchLog -Level "INFO" -Message "Skipping launch because -SkipLaunch was specified. Output: $OutputDirectory" -Color DarkGray
        return $true
    }

    $winapp = Get-Command winapp -ErrorAction SilentlyContinue
    if (-not $winapp) {
        Write-WatchLog -Level "ERROR" -Message "winapp CLI not found in PATH. Build succeeded, but the app was not relaunched." -Color Red
        return $false
    }

    Stop-ExistingApp

    Write-WatchLog -Level "INFO" -Message "Launching app with winapp run --detach: $OutputDirectory" -Color Cyan
    $output = & $winapp.Source run $OutputDirectory --detach --json 2>&1
    $exitCode = $LASTEXITCODE
    Write-CommandOutput -Output $output

    if ($exitCode -ne 0) {
        Write-WatchLog -Level "ERROR" -Message "Launch failed with exit code $exitCode." -Color Red
        return $false
    }

    $pidFromOutput = Get-ProcessFromLaunchOutput -Output $output
    if (-not $pidFromOutput) {
        Start-Sleep -Milliseconds 500
        $pidFromOutput = Find-LatestAppProcessId
    }

    $script:CurrentAppProcessId = $pidFromOutput
    if ($script:CurrentAppProcessId) {
        Write-WatchLog -Level "INFO" -Message "App relaunched. PID: $script:CurrentAppProcessId" -Color Green
    } else {
        Write-WatchLog -Level "WARN" -Message "App relaunched, but no PID was detected." -Color Yellow
    }

    return $true
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

    if (-not (Invoke-WatchedBuild)) {
        $script:LastExitCode = 1
        return
    }

    try {
        $runSettings = Get-RunSettings
        $outputDirectory = Resolve-OutputDirectory -RunSettings $runSettings
        Update-AppxLayoutIfNeeded -OutputDirectory $outputDirectory
        if (Start-WatchedApp -OutputDirectory $outputDirectory) {
            $script:LastExitCode = 0
        } else {
            $script:LastExitCode = 1
        }
    } catch {
        Write-WatchLog -Level "ERROR" -Message $_.Exception.Message -Color Red
        $script:LastExitCode = 1
    }
}

function Test-RelevantChange {
    param([string]$RelativePath)

    if ([string]::IsNullOrWhiteSpace($RelativePath)) {
        return $true
    }

    $normalized = $RelativePath.Replace("/", "\")
    $segments = $normalized.Split("\", [System.StringSplitOptions]::RemoveEmptyEntries)
    $excludedSegments = @(
        ".git",
        ".vs",
        ".nuget",
        "artifacts",
        "bin",
        "obj",
        "logs",
        "AppPackages",
        "BundleArtifacts",
        "publish",
        "TestResults"
    )

    foreach ($segment in $segments) {
        if ($excludedSegments -icontains $segment) {
            return $false
        }
    }

    $fileName = [System.IO.Path]::GetFileName($normalized)
    $extension = [System.IO.Path]::GetExtension($normalized)
    $includedNames = @(
        "NuGet.Config",
        "Package.appxmanifest",
        "app.manifest",
        "PicLens.slnx"
    )
    $includedExtensions = @(
        ".cs",
        ".xaml",
        ".csproj",
        ".props",
        ".targets",
        ".resw",
        ".json",
        ".appxmanifest",
        ".manifest",
        ".ico",
        ".png",
        ".jpg",
        ".jpeg",
        ".gif",
        ".bmp",
        ".webp",
        ".svg"
    )

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
            if ($uniqueChanges.Count -eq 0) {
                continue
            }

            Invoke-RunCycle -Reason "file change" -Changes $uniqueChanges
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
