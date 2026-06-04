# ImageViewerWin

Native WinUI 3 / MVVM version of the Electron ImageViewer app in `E:\Developer\ImageViewer`.

## Current Milestone

- Official WinUI 3 MVVM scaffold.
- Native main-window shell backed by real favorites, folder scanning, thumbnails, selection, and conservative file operations.
- Secondary image viewer window with previous/next, zoom, pan, fullscreen, keyboard navigation, and animated-image unsupported feedback.
- Shared core domain project for Electron parity rules.
- Application and Infrastructure service projects for settings persistence, favorites, scanning, image loading, JPG conversion, recycle-bin trash, and rename planning.
- xUnit coverage for Core, Application, and Infrastructure behavior.

## Solution

```text
ImageViewerWin.slnx
ImageViewerWin/                         WinUI 3 app
src/ImageViewerWin.Application/         Service contracts and deterministic planning
src/ImageViewerWin.Core/                Pure models and domain rules
src/ImageViewerWin.Infrastructure/      JSON, filesystem, image, and recycle-bin services
tests/ImageViewerWin.Application.Tests/ xUnit application tests
tests/ImageViewerWin.Core.Tests/        xUnit domain tests
tests/ImageViewerWin.Infrastructure.Tests/ xUnit infrastructure tests
```

## Build And Test

```powershell
dotnet restore .\tests\ImageViewerWin.Core.Tests\ImageViewerWin.Core.Tests.csproj --configfile .\NuGet.Config
dotnet restore .\tests\ImageViewerWin.Application.Tests\ImageViewerWin.Application.Tests.csproj --configfile .\NuGet.Config
dotnet restore .\tests\ImageViewerWin.Infrastructure.Tests\ImageViewerWin.Infrastructure.Tests.csproj --configfile .\NuGet.Config
dotnet restore .\ImageViewerWin\ImageViewerWin.csproj --configfile .\NuGet.Config -r win-x64 /p:Platform=x64
dotnet test .\tests\ImageViewerWin.Core.Tests\ImageViewerWin.Core.Tests.csproj --no-restore
dotnet test .\tests\ImageViewerWin.Application.Tests\ImageViewerWin.Application.Tests.csproj --no-restore
dotnet test .\tests\ImageViewerWin.Infrastructure.Tests\ImageViewerWin.Infrastructure.Tests.csproj --no-restore
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
