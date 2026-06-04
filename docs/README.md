# ImageViewerWin Docs

ImageViewerWin is the native WinUI 3 / MVVM port of the Electron ImageViewer app in `E:\Developer\ImageViewer`.

## Documents

- [Architecture](architecture.md) describes the solution layout, responsibilities, and current native shell.
- [Native parity](native-parity.md) tracks which Electron behaviors the native app should preserve.
- [Testing](testing.md) lists the verification commands and current test coverage.
- [Portable release](portable-release.md) explains how to build the no-install output folder.

## Current Status

The current native milestone is an integrated WinUI 3 app with MVVM view models, service-backed favorites, folder scanning, real thumbnail loading, a secondary image viewer window, and conservative file operations. Core, Application, and Infrastructure behavior are covered by focused xUnit projects.
