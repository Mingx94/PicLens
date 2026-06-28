# Installer Release

PicLens Windows installer output is an MSIX package built from the existing portable release folder.

## Build

From repository root:

```powershell
.\scripts\BuildInstaller.ps1
```

Fast local build:

```powershell
.\scripts\BuildInstaller.ps1 -SkipTests
```

Default output:

```text
artifacts/installer/PicLens-win-x64.msix
artifacts/installer/PicLens-win-x64.cer
```

## Install A Dev-Signed Package

The default build uses a self-signed certificate. Trust it once from an Administrator PowerShell, then install the package:

```powershell
Import-Certificate -FilePath .\artifacts\installer\PicLens-win-x64.cer -CertStoreLocation Cert:\LocalMachine\Root
Add-AppxPackage .\artifacts\installer\PicLens-win-x64.msix
```

For upgrades, pass a higher MSIX version:

```powershell
.\scripts\BuildInstaller.ps1 -Version 1.0.1.0
```

## Options

```powershell
.\scripts\BuildInstaller.ps1 -RuntimeIdentifier win-arm64 -Platform ARM64
.\scripts\BuildInstaller.ps1 -RuntimeIdentifier win-x86 -Platform x86
.\scripts\BuildInstaller.ps1 -CertificateThumbprint <thumbprint>
```

## Notes

This package is still framework-dependent. Target machines must have .NET Runtime 10 installed.

Installer builds strip debug symbol `.pdb` files from MSIX staging. Portable release folders keep them.

The package is signed for sideloading. Store or production releases should use a real code-signing certificate whose subject matches the package publisher.
