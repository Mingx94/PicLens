[CmdletBinding()]
param(
    [string]$Version,
    [switch]$NoRelease,
    [switch]$NoClean,
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = (Get-Content -Raw (Join-Path $repoRoot "VERSION")).Trim()
}
if ($Version -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') {
    throw "Installer version must contain three or four dot-separated numbers: $Version"
}

$portableDirectory = Join-Path $repoRoot "artifacts\qt-portable\PicLens-win-x64"
$installerDirectory = Join-Path $repoRoot "artifacts\installer"
$msiPath = Join-Path $installerDirectory "PicLens-win-x64.msi"
$projectPath = Join-Path $repoRoot "installer\PicLens.wixproj"
$portableScript = Join-Path $PSScriptRoot "build-portable.ps1"
$auditScript = Join-Path $PSScriptRoot "audit-msi.ps1"

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

Write-Host "Windows MSI version: $Version"
Write-Host "Portable payload: $portableDirectory"

if ($DryRun) {
    if (-not $NoRelease) {
        Write-Host "pwsh -NoProfile -File `"$portableScript`""
    }
    Write-Host "dotnet build `"$projectPath`" --no-incremental --configuration Release /p:AppVersion=$Version /p:PayloadDir=`"$portableDirectory`" /p:SuppressValidation=true /p:OutputPath=`"$installerDirectory\`" /p:OutputName=PicLens-win-x64"
    Write-Host "pwsh -NoProfile -File `"$auditScript`" -MsiPath `"$msiPath`" -PayloadDirectory `"$portableDirectory`" -ExpectedVersion $Version"
    exit 0
}

if (-not $NoRelease) {
    Invoke-Stage "Build Qt portable payload" {
        & $portableScript
        if ($LASTEXITCODE -ne 0) { throw "Portable build failed with exit code $LASTEXITCODE" }
    }
}

if (-not (Test-Path -LiteralPath (Join-Path $portableDirectory "PicLens.exe"))) {
    throw "Portable release was not found: $portableDirectory"
}

New-Item -ItemType Directory -Path $installerDirectory -Force | Out-Null
if (-not $NoClean) {
    Remove-Item -LiteralPath $msiPath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath (Join-Path $installerDirectory "cab1.cab") -Force -ErrorAction SilentlyContinue
}

Invoke-Stage "Build WiX MSI" {
    & dotnet build $projectPath --no-incremental --configuration Release `
        "/p:AppVersion=$Version" `
        "/p:PayloadDir=$portableDirectory" `
        "/p:SuppressValidation=true" `
        "/p:OutputPath=$installerDirectory\" `
        "/p:OutputName=PicLens-win-x64"
    if ($LASTEXITCODE -ne 0) { throw "WiX build failed with exit code $LASTEXITCODE" }
}

if (-not (Test-Path -LiteralPath $msiPath)) {
    throw "MSI build completed but output was not found: $msiPath"
}

Invoke-Stage "Audit MSI database and Qt payload" {
    & $auditScript -MsiPath $msiPath -PayloadDirectory $portableDirectory -ExpectedVersion $Version
    if ($LASTEXITCODE -ne 0) { throw "MSI audit failed with exit code $LASTEXITCODE" }
}

$hash = (Get-FileHash -LiteralPath $msiPath -Algorithm SHA256).Hash
Write-Host "MSI ready: $msiPath"
Write-Host "Bytes: $((Get-Item -LiteralPath $msiPath).Length)"
Write-Host "SHA256: $hash"
