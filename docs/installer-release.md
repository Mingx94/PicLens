# Installer Release

PicLens Windows installer output is an Inno Setup `.exe` built from the existing portable release folder.

## Build

Install Inno Setup 6, then run from repository root:

```powershell
winget install --id JRSoftware.InnoSetup -e
```

```powershell
.\scripts\BuildInstaller.ps1
```

Fast local build:

```powershell
.\scripts\BuildInstaller.ps1 -SkipTests
```

Default output:

```text
artifacts/installer/PicLens-win-x64-Setup.exe
```

## Options

```powershell
.\scripts\BuildInstaller.ps1 -Version 1.0.1.0
.\scripts\BuildInstaller.ps1 -RuntimeIdentifier win-x86 -Platform x86
.\scripts\BuildInstaller.ps1 -InnoSetupCompiler "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
```

## Notes

The setup installs per-user under `%LOCALAPPDATA%\Programs\PicLens`, so it does not need Administrator rights.

This package is still framework-dependent. Target machines must have .NET Runtime 10 installed.

Installer builds strip debug symbol `.pdb` files from setup staging. Portable release folders keep them.
