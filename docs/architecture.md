# Architecture

PicLens production runtime 使用 Qt 6、C++20 與 Qt Quick。Repository root 同時是標準 CMake/Qt project root，可直接由 Qt Creator 開啟。

```text
src/core/                 framework-light product rules and value models
src/infrastructure/       filesystem, settings, logging, thumbnails, OS adapters
src/presentation/         typed models and controllers exposed to QML
src/app/                  composition root, QML registration and executable
qml/PicLens/              Qt Quick shell and reusable controls
tests/                    C++ Qt Test and QML Quick Test suites
scripts/                  portable, installer, lifecycle and performance gates
assets/                   icons, logos and embedded fonts
installer/                WiX definition for the Qt Windows payload
```

## Dependency direction

`app -> presentation -> core` and `app -> infrastructure -> core`. Core does not depend on QML, filesystem implementations, platform commands, or image codecs. Presentation owns UI-facing state and orchestration; QML owns visual layout and direct interaction wiring.

## Runtime composition

`src/app/src/main.cpp` creates the Qt application, registers C++ types, loads embedded fonts and QML, and provides diagnostic launch switches. `AppController` composes settings, logging, scanner, thumbnail, file-operation, viewer and folder-tree services. `Main.qml` is the production shell.

The gallery and folder tree use lazy/virtualized models. The library model indexes path identity for constant-time thumbnail delivery, preserves thumbnail state across search/sort projections and limits role notifications to affected rows. Thumbnail work is bounded and coordinated asynchronously; stale requests are discarded when search, folder or navigation generation changes. Cache capacity is maintained incrementally and pruned only after exceeding its bound. A bounded decoded-image cache and forced-asynchronous Qt Quick image provider deliver generated thumbnails without a cold-path PNG reload. The viewer requests viewport-sized decode tiers instead of unconditional original-size textures. Settings writes are normalized and atomic. OS-specific trash and reveal behavior are isolated behind `PlatformFileManager`.

## Data and diagnostics

Without `PICLENS_DATA_ROOT`, platform local application data under `PicLens` remains the authority for settings, cache and logs. Tests, smoke runs and installers set an isolated root so they never modify the user's profile. See [data continuity](data-migration.md).

## Packaging

Windows portable deployment uses `windeployqt`; WiX packages that exact audited payload. Linux portable deployment copies Qt runtime dependencies and plugins; system DEB/RPM packages are generated from the same CMake install graph through CPack. No legacy runtime or packaging builder remains.
