# PicLens

PicLens 是使用 Qt 6、C++20 與 Qt Quick 建置的跨平台圖片整理與檢視應用程式，支援 Windows 與主流 Linux。

## Repository layout

```text
src/                production C++ libraries and application composition
qml/                Qt Quick shell and reusable controls
tests/              Qt Test and Qt Quick Test suites
scripts/            release, lifecycle and performance automation
packaging/          Linux desktop integration
assets/             framework-neutral icons、logos 與 embedded fonts
installer/          Windows WiX MSI definition
docs/               product、architecture、testing、release 與 migration evidence
LICENSE             MIT license
VERSION             package version authority
```

文件入口：[docs/README.md](docs/README.md)。

## Build and test

需要 CMake 3.21+、Ninja、C++20 compiler 與 Qt 6.5+（Core、Gui、Qml、Quick、QuickControls2、Concurrent、Test、QuickTest）。

```powershell
cd qt
cmake --preset debug
cmake --build --preset debug
ctest --preset debug --output-on-failure
```

Release：

```powershell
cd qt
cmake --preset release
cmake --build --preset release
ctest --preset release --output-on-failure
```

## Run

Windows：`build\debug\bin\PicLens.exe`

Linux：`./build/debug/bin/PicLens`

可用 `--folder <path>` 直接開啟圖片資料夾；測試或診斷時可用 `PICLENS_DATA_ROOT` 隔離 profile。

## Release

```powershell
# Windows portable
pwsh -NoProfile -File scripts/build-portable.ps1

# Windows MSI（.NET SDK 僅供 WiX Toolset）
pwsh -NoProfile -File scripts/build-msi.ps1
```

Linux installers：

```bash
bash scripts/build-deb.sh
bash scripts/build-rpm.sh
```

```bash
# Linux portable
bash scripts/build-linux-portable.sh
```

DEB/RPM 使用 CPack，詳細命令與選項見 [installer release](docs/installer-release.md)。Windows、Ubuntu 與 Fedora clean-runner gates 位於 `.github/workflows/release.yml`。

## Status

Production runtime 已完成 Qt cutover；舊 UI/runtime projects、tests、rollback commands 與 legacy packaging builders 已移除。MIT license、portable bundles、MSI/DEB/RPM lifecycle、profile-copy continuity 與 Windows 10,000-image performance gate 均已有驗證證據。
