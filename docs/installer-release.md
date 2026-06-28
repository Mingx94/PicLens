# Installer Release

PicLens installer outputs are built from the existing portable release folder.

## Build

Run the platform-detecting installer wrapper from repository root:

```bash
dotnet run --file scripts/Installer.cs --
```

The wrapper builds the installer for the current host:

- Windows: `artifacts/installer/PicLens-win-x64-Setup.exe`
- Fedora Linux: `artifacts/installer/PicLens-1.0.0-fedora-x86_64.rpm`

If a required packaging tool is missing, the wrapper prints the install command and exits.

Installer builds do not run tests. Run `dotnet run --file scripts/Tasks.cs -- test` separately before packaging.

## Options

```bash
dotnet run --file scripts/Installer.cs -- --version 1.0.1
dotnet run --file scripts/Installer.cs -- --dry-run
dotnet run --file scripts/Installer.cs -- --no-clean
dotnet run --file scripts/Installer.cs -- --no-release
```

## Tooling

Windows setup builds require Inno Setup 6:

```powershell
winget install --id JRSoftware.InnoSetup -e
```

Windows default output:

```text
artifacts/installer/PicLens-win-x64-Setup.exe
```

Fedora RPM builds require `rpm-build`:

```bash
sudo dnf install rpm-build
```

Fedora default output:

```text
artifacts/installer/PicLens-1.0.0-fedora-x86_64.rpm
```

## Windows options

```powershell
dotnet run --file scripts/Installer.cs -- --version 1.0.1.0
dotnet run --file scripts/Installer.cs -- --inno-setup-compiler "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
```

## Notes

The setup installs per-user under `%LOCALAPPDATA%\Programs\PicLens`, so it does not need Administrator rights.

This package is still framework-dependent. Target machines must have .NET Runtime 10 installed.

Installer builds strip debug symbol `.pdb` files from setup staging. Portable release folders keep them.
