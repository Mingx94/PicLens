# Installer Release

PicLens installer outputs are built from the existing portable release folder.

## Build

Install Inno Setup 6, then run from repository root:

```powershell
winget install --id JRSoftware.InnoSetup -e
```

```powershell
.\build\windows-x64.ps1
```

Fast local build:

```powershell
.\build\windows-x64.ps1 -SkipTests
```

Default output:

```text
artifacts/installer/PicLens-win-x64-Setup.exe
```

Fedora RPM:

```bash
bash ./build/fedora-x64.sh
```

Default output:

```text
artifacts/installer/PicLens-1.0.0-fedora-x86_64.rpm
```

## Windows options

```powershell
.\build\windows-x64.ps1 -Version 1.0.1.0
.\build\windows-x64.ps1 -InnoSetupCompiler "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
```

## Notes

The setup installs per-user under `%LOCALAPPDATA%\Programs\PicLens`, so it does not need Administrator rights.

This package is still framework-dependent. Target machines must have .NET Runtime 10 installed.

Installer builds strip debug symbol `.pdb` files from setup staging. Portable release folders keep them.
