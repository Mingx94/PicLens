[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [ValidateSet("win-x64", "win-arm64", "win-x86")]
    [string]$RuntimeIdentifier = "win-x64",

    [ValidateSet("x64", "ARM64", "x86")]
    [string]$Platform = "x64",

    [string]$Version = "1.0.0.0",
    [string]$PackageName = "PicLens",
    [string]$Publisher = "CN=PicLens",
    [string]$CertificateThumbprint,

    [switch]$SkipTests,
    [switch]$NoClean
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($PSVersionTable.ContainsKey("Platform") -and $PSVersionTable.Platform -ne "Win32NT") {
    throw "scripts/BuildInstaller.ps1 is Windows-only."
}

$root = Split-Path -Parent $PSScriptRoot
$releaseScript = Join-Path $PSScriptRoot "Release.ps1"
$assetSource = Join-Path (Join-Path $root "PicLens") "Assets"
$portableDir = Join-Path (Join-Path (Join-Path $root "artifacts") "portable") "PicLens-$RuntimeIdentifier"
$installerRoot = Join-Path (Join-Path $root "artifacts") "installer"
$stageRoot = Join-Path $installerRoot "msix-stage"
$stageDir = Join-Path $stageRoot "PicLens-$RuntimeIdentifier"
$msixPath = Join-Path $installerRoot "PicLens-$RuntimeIdentifier.msix"
$certPath = Join-Path $installerRoot "PicLens-$RuntimeIdentifier.cer"

function Assert-UnderRoot {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    $resolvedRoot = [System.IO.Path]::GetFullPath($root)
    $resolvedPath = [System.IO.Path]::GetFullPath($Path)

    if (-not $resolvedPath.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to operate outside workspace root: $resolvedPath"
    }
}

function Invoke-Native {
    param(
        [Parameter(Mandatory)]
        [string]$FilePath,

        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath failed with exit code $LASTEXITCODE."
    }
}

function Find-WindowsSdkTool {
    param(
        [Parameter(Mandatory)]
        [string]$Name
    )

    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $programFilesX86 = ${env:ProgramFiles(x86)}
    if (-not $programFilesX86) {
        throw "$Name was not found on PATH and ProgramFiles(x86) is not set."
    }

    $kitBin = Join-Path $programFilesX86 "Windows Kits\10\bin"
    if (-not (Test-Path -LiteralPath $kitBin)) {
        throw "$Name was not found. Install the Windows SDK."
    }

    $tools = Get-ChildItem -LiteralPath $kitBin -Recurse -Filter $Name -ErrorAction SilentlyContinue
    $tool = $tools |
        Where-Object { $_.FullName -like "*\x64\$Name" } |
        Sort-Object FullName -Descending |
        Select-Object -First 1

    if (-not $tool) {
        $tool = $tools | Sort-Object FullName -Descending | Select-Object -First 1
    }

    if (-not $tool) {
        throw "$Name was not found. Install the Windows SDK."
    }

    return $tool.FullName
}

function Test-MsixVersion {
    param(
        [Parameter(Mandatory)]
        [string]$Value
    )

    if ($Value -notmatch '^\d+\.\d+\.\d+\.\d+$') {
        return $false
    }

    foreach ($part in $Value.Split(".")) {
        if ([int]$part -gt 65535) {
            return $false
        }
    }

    return $true
}

function Escape-Xml {
    param(
        [Parameter(Mandatory)]
        [string]$Value
    )

    return [System.Security.SecurityElement]::Escape($Value)
}

function Find-SigningCertificate {
    param(
        [string]$Thumbprint,
        [Parameter(Mandatory)]
        [string]$Subject
    )

    if ($Thumbprint) {
        $normalizedThumbprint = $Thumbprint -replace '\s', ''
        foreach ($store in @("Cert:\CurrentUser\My", "Cert:\LocalMachine\My")) {
            $cert = Get-ChildItem -LiteralPath $store -ErrorAction SilentlyContinue |
                Where-Object { $_.Thumbprint -eq $normalizedThumbprint } |
                Select-Object -First 1
            if ($cert) {
                if ($cert.Subject -ne $Subject) {
                    throw "Package publisher '$Subject' must match signing certificate subject '$($cert.Subject)'."
                }

                return $cert
            }
        }

        throw "Signing certificate was not found: $Thumbprint"
    }

    $existing = Get-ChildItem -LiteralPath "Cert:\CurrentUser\My" -ErrorAction SilentlyContinue |
        Where-Object { $_.Subject -eq $Subject -and $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date).AddDays(1) } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1

    if ($existing) {
        return $existing
    }

    return New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $Subject `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -KeyUsage DigitalSignature `
        -FriendlyName "PicLens MSIX Signing"
}

if (-not (Test-MsixVersion $Version)) {
    throw "MSIX version must be four dot-separated numbers from 0 to 65535: $Version"
}

if ($PackageName -notmatch '^[A-Za-z0-9][A-Za-z0-9.-]{2,49}$') {
    throw "MSIX package name must be 3-50 characters and use letters, numbers, dots, or hyphens: $PackageName"
}

Assert-UnderRoot -Path $installerRoot
Assert-UnderRoot -Path $stageRoot
Assert-UnderRoot -Path $stageDir
Assert-UnderRoot -Path $msixPath
Assert-UnderRoot -Path $certPath

foreach ($path in @($releaseScript, $assetSource)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required path not found: $path"
    }
}

$processorArchitecture = switch ($RuntimeIdentifier) {
    "win-x64" { "x64" }
    "win-arm64" { "arm64" }
    "win-x86" { "x86" }
}

$releaseArgs = @{
    Configuration = $Configuration
    RuntimeIdentifier = $RuntimeIdentifier
    Platform = $Platform
}
if ($SkipTests) {
    $releaseArgs.SkipTests = $true
}
if ($NoClean) {
    $releaseArgs.NoClean = $true
}

Write-Host "==> Building portable release" -ForegroundColor Cyan
& $releaseScript @releaseArgs
if ($LASTEXITCODE -ne 0) {
    throw "$releaseScript failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path -LiteralPath (Join-Path $portableDir "PicLens.exe"))) {
    throw "Portable release was not found: $portableDir"
}

$makeAppx = Find-WindowsSdkTool "makeappx.exe"
$signTool = Find-WindowsSdkTool "signtool.exe"
$cert = Find-SigningCertificate -Thumbprint $CertificateThumbprint -Subject $Publisher

New-Item -ItemType Directory -Path $installerRoot -Force | Out-Null

if (-not $NoClean) {
    Remove-Item -LiteralPath $stageRoot -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $msixPath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $certPath -Force -ErrorAction SilentlyContinue
}

New-Item -ItemType Directory -Path $stageDir -Force | Out-Null
Copy-Item -Path (Join-Path $portableDir "*") -Destination $stageDir -Recurse -Force
Get-ChildItem -LiteralPath $stageDir -Recurse -Filter "*.pdb" -File | Remove-Item -Force

$stageAssets = Join-Path $stageDir "Assets"
New-Item -ItemType Directory -Path $stageAssets -Force | Out-Null

foreach ($asset in @(
    "StoreLogo.png",
    "Square44x44Logo.scale-200.png",
    "Square150x150Logo.scale-200.png",
    "Wide310x150Logo.scale-200.png"
)) {
    $source = Join-Path $assetSource $asset
    if (-not (Test-Path -LiteralPath $source)) {
        throw "Required MSIX asset not found: $source"
    }

    Copy-Item -LiteralPath $source -Destination (Join-Path $stageAssets $asset) -Force
}

$packageNameXml = Escape-Xml $PackageName
$publisherXml = Escape-Xml $Publisher
$versionXml = Escape-Xml $Version
$architectureXml = Escape-Xml $processorArchitecture

@"
<?xml version="1.0" encoding="utf-8"?>
<Package
  xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
  xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
  xmlns:uap10="http://schemas.microsoft.com/appx/manifest/uap/windows10/10"
  xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
  IgnorableNamespaces="uap uap10 rescap">
  <Identity Name="$packageNameXml" Publisher="$publisherXml" Version="$versionXml" ProcessorArchitecture="$architectureXml" />
  <Properties>
    <DisplayName>PicLens</DisplayName>
    <PublisherDisplayName>PicLens</PublisherDisplayName>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>
  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.17763.0" MaxVersionTested="10.0.26100.0" />
  </Dependencies>
  <Applications>
    <Application Id="PicLens" Executable="PicLens.exe" EntryPoint="Windows.FullTrustApplication" uap10:RuntimeBehavior="packagedClassicApp" uap10:TrustLevel="mediumIL">
      <uap:VisualElements DisplayName="PicLens" Description="PicLens" BackgroundColor="transparent" Square150x150Logo="Assets\Square150x150Logo.scale-200.png" Square44x44Logo="Assets\Square44x44Logo.scale-200.png">
        <uap:DefaultTile Wide310x150Logo="Assets\Wide310x150Logo.scale-200.png" />
      </uap:VisualElements>
    </Application>
  </Applications>
  <Capabilities>
    <rescap:Capability Name="runFullTrust" />
  </Capabilities>
</Package>
"@ | Set-Content -LiteralPath (Join-Path $stageDir "AppxManifest.xml") -Encoding UTF8

Write-Host "==> Packing MSIX" -ForegroundColor Cyan
Invoke-Native $makeAppx @("pack", "/d", $stageDir, "/p", $msixPath, "/o")

Write-Host "==> Signing MSIX" -ForegroundColor Cyan
Invoke-Native $signTool @("sign", "/fd", "SHA256", "/sha1", $cert.Thumbprint, $msixPath)
Export-Certificate -Cert $cert -FilePath $certPath -Force | Out-Null

$sha256 = (Get-FileHash -LiteralPath $msixPath -Algorithm SHA256).Hash

Write-Host ""
Write-Host "Installer output ready:" -ForegroundColor Green
Write-Host "  Package: $msixPath"
Write-Host "  Cert:    $certPath"
Write-Host "  SHA256:  $sha256"
Write-Host ""
Write-Host "For self-signed builds, trust the certificate once from an Administrator PowerShell before installing:" -ForegroundColor Yellow
Write-Host "  Import-Certificate -FilePath `"$certPath`" -CertStoreLocation Cert:\LocalMachine\Root"
Write-Host "  Add-AppxPackage `"$msixPath`""
