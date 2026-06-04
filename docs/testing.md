# Testing

## Core Tests

Run:

```powershell
dotnet restore .\tests\ImageViewerWin.Core.Tests\ImageViewerWin.Core.Tests.csproj --configfile .\NuGet.Config
dotnet test .\tests\ImageViewerWin.Core.Tests\ImageViewerWin.Core.Tests.csproj --no-restore
```

Current coverage is focused on pure product rules in `ImageViewerWin.Core`.

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

This restores packages, runs tests, publishes a self-contained unpackaged output folder, and verifies that `ImageViewerWin.exe` exists.

Manual smoke check:

```powershell
.\artifacts\portable\ImageViewerWin-win-x64\ImageViewerWin.exe
```

The app should launch directly without installing an MSIX package.
