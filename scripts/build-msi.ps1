[CmdletBinding()]
param(
    [string]$Version,
    [switch]$NoRelease,
    [switch]$NoClean,
    [switch]$Sign,
    [string]$CertificateThumbprint = "",
    [string]$TimestampUrl = "http://timestamp.digicert.com",
    [switch]$RunLifecycleTest,
    [string]$PreviousMsiPath = "",
    [switch]$ConfirmSystemChanges,
    [switch]$AllowReplacingExistingInstallation,
    [switch]$SkipSmoke,
    [switch]$SkipAudit,
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = (Get-Content -Raw (Join-Path $repoRoot "VERSION")).Trim()
}
if ($Version -notmatch '^(?<year>\d{2})\.(?<month>\d{2})(?<day>\d{2})\.(?<hour>\d{2})(?<minute>\d{2})$') {
    throw "Installer version must use the UTC timestamp format YY.MMDD.HHmm: $Version"
}
$year = [int]$Matches['year']
$month = [int]$Matches['month']
$day = [int]$Matches['day']
$hour = [int]$Matches['hour']
$minute = [int]$Matches['minute']
try {
    [void][DateTime]::new(2000 + $year, $month, $day, $hour, $minute, 0)
}
catch {
    throw "Installer version contains an invalid UTC timestamp: $Version"
}
$msiBuild = (($day - 1) * 1440) + ($hour * 60) + $minute
$msiVersion = "$year.$month.$msiBuild"

$portableDirectory = Join-Path $repoRoot "artifacts\qt-portable\PicLens-win-x64"
$installerDirectory = Join-Path $repoRoot "artifacts\installer"
$outputName = "PicLens-$Version-win-x64"
$msiPath = Join-Path $installerDirectory "$outputName.msi"
$projectPath = Join-Path $repoRoot "installer\PicLens.wixproj"
$portableScript = Join-Path $PSScriptRoot "build-portable.ps1"
$auditScript = Join-Path $PSScriptRoot "audit-msi.ps1"
$lifecycleScript = Join-Path $PSScriptRoot "test-msi-lifecycle.ps1"

function Get-SignTool {
    $command = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) { return $command.Source }
    $kitsBin = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
    $candidate = Get-ChildItem -LiteralPath $kitsBin -Filter signtool.exe -File -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' } |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if ($null -eq $candidate) { throw "signtool.exe was not found" }
    return $candidate.FullName
}

function Invoke-AuthenticodeSign([string]$Path, [string]$SignTool) {
    & $SignTool sign /sha1 $CertificateThumbprint /fd SHA256 /tr $TimestampUrl /td SHA256 $Path
    if ($LASTEXITCODE -ne 0) { throw "Authenticode signing failed for $Path" }
    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne [Management.Automation.SignatureStatus]::Valid) {
        throw "Authenticode verification failed for $Path ($($signature.Status))"
    }
}

function Invoke-Stage([string]$Name, [scriptblock]$Action) {
    $started = Get-Date
    Write-Host "::group::$Name"
    try {
        & $Action
        $elapsed = [math]::Round(((Get-Date) - $started).TotalSeconds, 1)
        Write-Host "Completed in $elapsed seconds"
    }
    finally {
        Write-Host "::endgroup::"
    }
}

Write-Host "Windows release version: $Version"
Write-Host "Windows MSI ProductVersion: $msiVersion"
Write-Host "Portable payload: $portableDirectory"

if ($Sign -and [string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    throw "-Sign requires -CertificateThumbprint"
}
if ($RunLifecycleTest -and -not $ConfirmSystemChanges) {
    throw "-RunLifecycleTest requires -ConfirmSystemChanges because it installs and uninstalls PicLens"
}

if ($DryRun) {
    if (-not $NoRelease) {
        Write-Host "cmake --preset release"
        Write-Host "cmake --build --preset release"
        Write-Host "pwsh -NoProfile -File `"$portableScript`" -SkipSmoke:$SkipSmoke"
    }
    Write-Host "dotnet build `"$projectPath`" --no-incremental --configuration Release /p:AppVersion=$msiVersion /p:PayloadDir=`"$portableDirectory`" /p:SuppressValidation=true /p:OutputPath=`"$installerDirectory\`" /p:OutputName=$outputName"
    if ($Sign) { Write-Host "Sign PicLens.exe and MSI with certificate $CertificateThumbprint and RFC 3161 timestamp $TimestampUrl" }
    if (-not $SkipAudit) {
        Write-Host "pwsh -NoProfile -File `"$auditScript`" -MsiPath `"$msiPath`" -PayloadDirectory `"$portableDirectory`" -ExpectedVersion $msiVersion -RequireSigned:$Sign"
    }
    if ($RunLifecycleTest) { Write-Host "pwsh -NoProfile -File `"$lifecycleScript`" -MsiPath `"$msiPath`" -ConfirmSystemChanges" }
    exit 0
}

if (-not $NoRelease) {
    Invoke-Stage "Configure and build Qt Release" {
        Push-Location $repoRoot
        try {
            & cmake --preset release
            if ($LASTEXITCODE -ne 0) {
                throw "Release configure failed with exit code $LASTEXITCODE"
            }
            & cmake --build --preset release
            if ($LASTEXITCODE -ne 0) {
                throw "Release build failed with exit code $LASTEXITCODE"
            }
        }
        finally {
            Pop-Location
        }
    }
    Invoke-Stage "Build Qt portable payload" {
        & $portableScript -SkipSmoke:$SkipSmoke
        if ($LASTEXITCODE -ne 0) { throw "Portable build failed with exit code $LASTEXITCODE" }
    }
}

if (-not (Test-Path -LiteralPath (Join-Path $portableDirectory "PicLens.exe"))) {
    throw "Portable release was not found: $portableDirectory"
}

$signTool = $null
if ($Sign) {
    $signTool = Get-SignTool
    Invoke-Stage "Sign application executable" {
        Invoke-AuthenticodeSign (Join-Path $portableDirectory "PicLens.exe") $signTool
    }
}

New-Item -ItemType Directory -Path $installerDirectory -Force | Out-Null
if (-not $NoClean) {
    Remove-Item -LiteralPath $msiPath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath (Join-Path $installerDirectory "cab1.cab") -Force -ErrorAction SilentlyContinue
}

Invoke-Stage "Build WiX MSI" {
    & dotnet build $projectPath --no-incremental --configuration Release `
        "/p:AppVersion=$msiVersion" `
        "/p:PayloadDir=$portableDirectory" `
        "/p:SuppressValidation=true" `
        "/p:OutputPath=$installerDirectory\" `
        "/p:OutputName=$outputName"
    if ($LASTEXITCODE -ne 0) { throw "WiX build failed with exit code $LASTEXITCODE" }
}

if (-not (Test-Path -LiteralPath $msiPath)) {
    throw "MSI build completed but output was not found: $msiPath"
}


if ($Sign) {
    Invoke-Stage "Sign MSI" {
        Invoke-AuthenticodeSign $msiPath $signTool
    }
}

if (-not $SkipAudit) {
    Invoke-Stage "Audit MSI database and Qt payload" {
        & $auditScript -MsiPath $msiPath -PayloadDirectory $portableDirectory -ExpectedVersion $msiVersion -RequireSigned:$Sign
        if ($LASTEXITCODE -ne 0) { throw "MSI audit failed with exit code $LASTEXITCODE" }
    }
}


if ($RunLifecycleTest) {
    Invoke-Stage "Install, launch, upgrade, and uninstall MSI" {
        $lifecycleArguments = @(
            "-MsiPath", $msiPath,
            "-ExpectedExecutable", (Join-Path $portableDirectory "PicLens.exe"),
            "-ConfirmSystemChanges"
        )
        if (-not [string]::IsNullOrWhiteSpace($PreviousMsiPath)) {
            $lifecycleArguments += @("-PreviousMsiPath", $PreviousMsiPath)
        }
        if ($AllowReplacingExistingInstallation) {
            $lifecycleArguments += "-AllowReplacingExistingInstallation"
        }
        & $lifecycleScript @lifecycleArguments
        if ($LASTEXITCODE -ne 0) { throw "MSI lifecycle test failed with exit code $LASTEXITCODE" }
    }
}

$hash = (Get-FileHash -LiteralPath $msiPath -Algorithm SHA256).Hash
Write-Host "MSI ready: $msiPath"
Write-Host "Bytes: $((Get-Item -LiteralPath $msiPath).Length)"
Write-Host "SHA256: $hash"
