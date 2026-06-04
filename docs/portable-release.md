# Portable Release

ImageViewerWin is not being prepared for Microsoft Store or MSIX distribution right now. The release target is a no-install folder that can be copied and launched with `ImageViewerWin.exe`.

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

This is not a single-file executable. WinUI self-contained unpackaged output must keep the adjacent DLL, PRI, WinUI, and runtime files beside `ImageViewerWin.exe`.

Do not distribute only `ImageViewerWin.exe`; distribute the full folder.

## What The Script Does

1. Restores the test project using repo-local `NuGet.Config`.
2. Runs Core tests unless `-SkipTests` is passed.
3. Restores the app for the selected Windows RID.
4. Publishes with:
   - `WindowsPackageType=None`
   - `WindowsAppSDKSelfContained=true`
   - `--self-contained true`
   - `PublishSingleFile=false`
5. Verifies that `ImageViewerWin.exe` exists.
6. Reports file count, total bytes, and the executable SHA256.
