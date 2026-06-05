# Testing

## Unit Tests

Run:

```powershell
dotnet restore .\tests\ImageViewerWin.Core.Tests\ImageViewerWin.Core.Tests.csproj --configfile .\NuGet.Config
dotnet restore .\tests\ImageViewerWin.Application.Tests\ImageViewerWin.Application.Tests.csproj --configfile .\NuGet.Config
dotnet restore .\tests\ImageViewerWin.Infrastructure.Tests\ImageViewerWin.Infrastructure.Tests.csproj --configfile .\NuGet.Config
dotnet restore .\tests\ImageViewerWin.ViewModels.Tests\ImageViewerWin.ViewModels.Tests.csproj --configfile .\NuGet.Config
dotnet test .\tests\ImageViewerWin.Core.Tests\ImageViewerWin.Core.Tests.csproj --no-restore
dotnet test .\tests\ImageViewerWin.Application.Tests\ImageViewerWin.Application.Tests.csproj --no-restore
dotnet test .\tests\ImageViewerWin.Infrastructure.Tests\ImageViewerWin.Infrastructure.Tests.csproj --no-restore
dotnet test .\tests\ImageViewerWin.ViewModels.Tests\ImageViewerWin.ViewModels.Tests.csproj --no-restore
```

Current coverage spans:

- `ImageViewerWin.Core`: pure product rules.
- `ImageViewerWin.Application`: deterministic rename planning, including drop-target sequence advancement past existing targets.
- `ImageViewerWin.Infrastructure`: JSON settings, direct and recursive scanning, canonical directory de-duplication, image data helpers, conversion, trash, and rename operations.
- `ImageViewerWin.ViewModels`: startup folder selection flow, sort-without-rescan behavior, and Traditional Chinese runtime copy.

## WinUI Build

Run:

```powershell
dotnet restore .\ImageViewerWin\ImageViewerWin.csproj --configfile .\NuGet.Config -r win-x64 /p:Platform=x64
dotnet build .\ImageViewerWin\ImageViewerWin.csproj --no-restore /p:Platform=x64
```

For the plugin workflow:

```powershell
.\BuildAndRun.ps1 .\ImageViewerWin\ImageViewerWin.csproj -SkipRun
```

`BuildAndRun.ps1` uses Visual Studio MSBuild and may need to run outside restricted sandboxes when NuGet config access is blocked.

## Portable Release Verification

Run:

```powershell
.\Release.ps1
```

This restores packages, runs Core, Application, Infrastructure, and ViewModel tests, publishes a self-contained unpackaged output folder, and verifies that `ImageViewerWin.exe` exists.

Manual smoke check:

```powershell
.\artifacts\portable\ImageViewerWin-win-x64\ImageViewerWin.exe
```

The app should launch directly without installing an MSIX package.
