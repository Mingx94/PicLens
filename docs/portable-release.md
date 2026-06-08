# Portable Release

ImageViewerWin is not being prepared for Microsoft Store or MSIX distribution right now. The release target is a framework-dependent no-install folder that can be copied and launched with `ImageViewerWin.exe` on machines that already have the required runtimes installed.

## Build

Run from the repository root:

```powershell
.\Release.ps1
```

Default output:

```text
artifacts/portable/ImageViewerWin-win-x64/
```

The executable is:

```text
artifacts/portable/ImageViewerWin-win-x64/ImageViewerWin.exe
```

## Options

```powershell
.\Release.ps1 -SkipTests
.\Release.ps1 -RuntimeIdentifier win-arm64 -Platform ARM64
.\Release.ps1 -RuntimeIdentifier win-x86 -Platform x86
```

## Notes

This is not a single-file executable. WinUI unpackaged output must keep the adjacent DLL, PRI, WinUI, and runtime files beside `ImageViewerWin.exe`.

Do not distribute only `ImageViewerWin.exe`; distribute the full folder.

The default output is framework-dependent. Target machines must already have:

- Windows App Runtime 1.8
- .NET Runtime 10
- .NET Windows Desktop Runtime 10

## What The Script Does

1. Restores the Core, Application, Infrastructure, and ViewModels test projects using repo-local `NuGet.Config`.
2. Runs Core, Application, Infrastructure, and ViewModels tests unless `-SkipTests` is passed.
3. Restores the app for the selected Windows RID.
4. Publishes with:
   - `WindowsPackageType=None`
   - `WindowsAppSDKSelfContained=false`
   - `--self-contained false`
   - `PublishSelfContained=false`
   - `PublishSingleFile=false`
   - `SelfContained=false`
   - `DebugType=None`
   - `DebugSymbols=false`
5. Verifies that `ImageViewerWin.exe` exists.
6. Reports file count, total bytes, and the executable SHA256.
