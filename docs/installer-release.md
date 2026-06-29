# Installer Release

PicLens installer outputs are built from the existing portable release folder.

This page documents the installer outputs currently implemented by `Tasks.cs`. Product support still targets mainstream Linux desktop distributions, not Fedora/RPM only.

## Build

Run the platform-detecting installer task from repository root:

```shell
dotnet run Tasks.cs installer
```

The wrapper builds the implemented installer for the current host:

- Windows: `artifacts/installer/PicLens-win-x64-Setup.exe`
- Fedora Linux: `artifacts/installer/PicLens-1.0.0-fedora-x86_64.rpm`

If a required packaging tool is missing, the wrapper prints the install command and exits.

Installer builds do not run tests. Run `dotnet run Tasks.cs test` separately before packaging.

## Linux coverage

The current Linux installer implementation emits a Fedora RPM. This is not complete Linux installer coverage by itself: mainstream Linux support must account for Debian/Ubuntu-compatible systems as well as Fedora/RPM systems. Until additional package formats are implemented, the generic `linux-x64` portable release is the cross-distro path for machines with .NET Runtime 10 installed.

## Options

```shell
dotnet run Tasks.cs installer --version 1.0.1
dotnet run Tasks.cs installer --dry-run
dotnet run Tasks.cs installer --no-clean
dotnet run Tasks.cs installer --no-release
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

```shell
sudo dnf install rpm-build
```

Fedora default output:

```text
artifacts/installer/PicLens-1.0.0-fedora-x86_64.rpm
```

## Windows options

```powershell
dotnet run Tasks.cs -- installer --version 1.0.1.0
dotnet run Tasks.cs -- installer --inno-setup-compiler "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
```

## Notes

The setup installs per-user under `%LOCALAPPDATA%\Programs\PicLens`, so it does not need Administrator rights.

This package is still framework-dependent. Target machines must have .NET Runtime 10 installed.

Installer builds strip debug symbol `.pdb` files from setup staging. Portable release folders keep them.
