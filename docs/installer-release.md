# Installer release

The package version comes from root `VERSION` unless explicitly overridden.

## Windows MSI

```powershell
pwsh -NoProfile -File scripts/build-msi.ps1
```

Useful diagnostic options:

```powershell
pwsh -NoProfile -File scripts/build-msi.ps1 -DryRun
pwsh -NoProfile -File scripts/build-msi.ps1 -NoRelease
pwsh -NoProfile -File scripts/build-msi.ps1 -NoClean
pwsh -NoProfile -File scripts/build-msi.ps1 -Version 2.0.0
```

Output: `artifacts/installer/PicLens-win-x64.msi`.

The script logs and times three independent stages: Qt portable payload, WiX build, and MSI database/payload audit. `-NoRelease` requires an existing portable bundle. WiX Toolset currently requires the .NET SDK, but no application runtime or test project uses .NET.

## Debian / Ubuntu DEB

```bash
cmake -S . -B build/release -G Ninja \
  -DCMAKE_BUILD_TYPE=Release -DPICLENS_SYSTEM_PACKAGE=ON
cmake --build build/release
cpack -G DEB --config build/release/CPackConfig.cmake
```

## Fedora / RHEL RPM

```bash
cmake -S . -B build/fedora -G Ninja \
  -DCMAKE_BUILD_TYPE=Release \
  -DPICLENS_SYSTEM_PACKAGE=ON \
  -DPICLENS_USE_SYSTEM_QT=ON
cmake --build build/fedora
cd build/fedora && cpack -G RPM
```

DEB/RPM are generated from the Qt CMake install graph. The old standalone Fedora builder and rollback package path have been removed. Signing remains a release-operations responsibility and requires the appropriate certificate/key outside this repository.
