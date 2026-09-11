[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$MsiPath,
    [string]$PreviousMsiPath,
    [switch]$ConfirmSystemChanges
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $ConfirmSystemChanges) { throw '安裝測試會變更系統；明確授權後才可加上 -ConfirmSystemChanges。' }
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$candidate = (Resolve-Path -LiteralPath $MsiPath).Path
$installedExe = Join-Path $env:ProgramFiles 'PicLens/PicLens.exe'
if (Test-Path -LiteralPath $installedExe) { throw '此測試需要沒有既有 PicLens 安裝的乾淨環境。' }
$evidence = Join-Path $repo ('artifacts/wpf-msi-lifecycle-' + [guid]::NewGuid().ToString('N'))
$profile = Join-Path $evidence 'profile'
$q = [char]34
New-Item -ItemType Directory -Path $profile -Force | Out-Null
$settings = Join-Path $profile 'piclens-settings.json'
$initial = @{ lastFolderPath = (Join-Path $repo 'assets'); sort = @{ key = 1; direction = 1 }; includeSubfolders = $false; thumbnailSize = 200; sidebarCollapsed = $true }
$initial | ConvertTo-Json | Set-Content -LiteralPath $settings -Encoding utf8
function Msi([string]$file, [string]$verb, [string]$label) {
    $log = Join-Path $evidence "$label.log"
    $process = Start-Process msiexec.exe -ArgumentList @("/$verb", "$q$file$q", '/qn', '/norestart', '/l*v', "$q$log$q") -PassThru -WindowStyle Hidden
    if (-not $process.WaitForExit(600000)) { throw "$label MSI 逾時；請檢查 $log" }
    if ($process.ExitCode -notin 0,3010) { throw "$label MSI exit=$($process.ExitCode)" }
}
$activePackage = $null
try {
    if ($PreviousMsiPath) {
        $previous = (Resolve-Path -LiteralPath $PreviousMsiPath).Path
        Msi $previous 'i' 'previous-install'; $activePackage = $previous
    }
    Msi $candidate 'i' 'candidate-install'; $activePackage = $candidate
    if (-not (Test-Path -LiteralPath $installedExe)) { throw '安裝後找不到 PicLens.exe。' }
    $metric = Join-Path $evidence 'smoke.json'
    $process = Start-Process $installedExe -ArgumentList @('--data-root', "$q$profile$q", '--smoke-ms', '1500', '--metrics', "$q$metric$q") -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(30000)) { Stop-Process -Id $process.Id -Force; throw '已安裝程式未正常結束。' }
    if ($process.ExitCode -ne 0) { throw '已安裝程式啟動失敗。' }
    $loaded = Get-Content -Raw -LiteralPath $settings | ConvertFrom-Json
    if ($loaded.thumbnailSize -ne 200 -or $loaded.sort.key -ne 1 -or $loaded.sort.direction -ne 1 -or -not $loaded.sidebarCollapsed) { throw '舊設定未保留。' }
    Msi $candidate 'fa' 'repair'
    $before = (Get-FileHash -LiteralPath $settings).Hash
    Msi $candidate 'x' 'uninstall'; $activePackage = $null
    if (Test-Path -LiteralPath $installedExe) { throw '解除安裝後程式仍存在。' }
    if ((Get-FileHash -LiteralPath $settings).Hash -ne $before) { throw '解除安裝改動設定。' }
    @{ install = 'pass'; launch = 'pass'; repair = 'pass'; uninstall = 'pass'; profile = 'pass'; upgrade = $(if ($PreviousMsiPath) { 'pass' } else { 'not-tested' }) } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $evidence 'result.json')
    Write-Output "MSI lifecycle passed: $evidence"
} finally {
    if ($activePackage) { Msi $activePackage 'x' 'failure-cleanup' }
}
