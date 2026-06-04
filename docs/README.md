# ImageViewerWin Docs

ImageViewerWin is the native WinUI 3 / MVVM port of the Electron ImageViewer app in `E:\Developer\ImageViewer`.

## Documents

- [Architecture](architecture.md) describes the solution layout, responsibilities, and current native shell.
- [Native parity](native-parity.md) tracks which Electron behaviors the native app should preserve.
- [Testing](testing.md) lists the verification commands and current test coverage.
- [Portable release](portable-release.md) explains how to build the no-install output folder.

## Current Status

The current native milestone is an application scaffold plus core domain parity. The app has a WinUI shell with favorites, folder tree, command bar, image grid, and status bar. Filesystem services, real thumbnail loading, image windows, and full file-management behavior are still future lanes.
