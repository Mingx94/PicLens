# ImageViewerWin

Native WinUI 3 / MVVM version of the Electron ImageViewer app in `E:\Developer\ImageViewer`.

## Current Milestone

- Official WinUI 3 MVVM scaffold.
- Native main-window shell with favorites, folder tree, command bar, image grid, and status bar.
- Shared core domain project for Electron parity rules.
- xUnit coverage for supported image formats, animated GIF/WebP detection, sorting, settings merge, startup folder selection, image sequence snapshots, and zoom math.

## Solution

```text
ImageViewerWin.slnx
ImageViewerWin/                 WinUI 3 app
src/ImageViewerWin.Core/        Pure models and domain rules
tests/ImageViewerWin.Core.Tests xUnit domain tests
```

## Build And Test

```powershell
dotnet restore .\tests\ImageViewerWin.Core.Tests\ImageViewerWin.Core.Tests.csproj --configfile .\NuGet.Config
dotnet restore .\ImageViewerWin\ImageViewerWin.csproj --configfile .\NuGet.Config -r win-x64 /p:Platform=x64
dotnet test .\tests\ImageViewerWin.Core.Tests\ImageViewerWin.Core.Tests.csproj --no-restore
dotnet build .\ImageViewerWin\ImageViewerWin.csproj --no-restore /p:Platform=x64
.\BuildAndRun.ps1 .\ImageViewerWin\ImageViewerWin.csproj
```

Use `.\BuildAndRun.ps1 .\ImageViewerWin\ImageViewerWin.csproj -SkipRun` for build-only verification.

## Portable Release

Build the no-install app folder:

```powershell
.\Release.ps1
```

Output:

```text
artifacts/portable/ImageViewerWin-win-x64/ImageViewerWin.exe
```

Keep the full folder together; this is a WinUI self-contained unpackaged layout, not a single-file exe.

## Docs

Start at [docs/README.md](docs/README.md).

## Parallel Lanes Used

- Product map: read the Electron app and summarized native parity scope.
- WinUI architecture: proposed solution ownership, controls, test strategy, and staged worker lanes.
- Main implementation: scaffold, core TDD, WinUI shell, build/run verification.
