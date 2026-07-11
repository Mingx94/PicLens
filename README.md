# PicLens

PicLens 是 Windows / 主流 Linux 桌面圖片整理與檢視工具。它以本機資料夾為工作區，讓使用者快速瀏覽大量圖片、調整排序與顯示範圍、選取圖片、執行保守的檔案操作，並在主視窗內直接檢視單張圖片。

詳細文件從 [docs/README.md](docs/README.md) 開始。

## 功能重點

- 明確選取資料夾；啟動時可還原上次透過資料夾選擇器選取且仍可用的資料夾。
- 資料夾樹加縮圖圖庫，支援 `jpg`、`jpeg`、`png`、`bmp`、`webp`、`gif`。
- 支援排序、包含子資料夾、縮圖大小保存、資料夾歷史導覽與滑鼠側鍵導覽。
- 圖片選取後可重新命名、移至作業系統回收筒、轉換 JPG，批次操作會回報成功、略過與失敗結果。
- 內嵌圖片檢視器支援上一張/下一張、縮放、平移、鍵盤操作與 Escape 返回。
- 動畫 GIF / WebP 會被辨識，但目前不播放；檢視時會顯示不支援預覽的提示。

## 專案結構

```text
PicLens.slnx                         Visual Studio solution
PicLens/                             Avalonia desktop app、AXAML views、assets、window setup
qt/                                  正式 Qt 6 / C++20 migration production tree
src/PicLens.Core/                    Pure models、service contracts、domain rules
src/PicLens.Presentation/            UI-agnostic ViewModels、presentation contracts
src/PicLens.Infrastructure/          Settings、filesystem、thumbnail、trash、logging services
tests/PicLens.Core.Tests/            Core xUnit tests
tests/PicLens.Infrastructure.Tests/  Infrastructure xUnit tests
tests/PicLens.ViewModels.Tests/      ViewModel xUnit tests
tests/PicLens.Ui.Tests/              Avalonia Headless smoke tests
docs/                                Product、architecture、testing、release docs
Tasks.cs                             Repo-local build/test/release tasks
```

## 開發

需要可建置 `net10.0` 的 .NET SDK。

```shell
dotnet run --project PicLens/PicLens.csproj -p:Platform=x64
```

只建置、不啟動 app：

```shell
dotnet build PicLens/PicLens.csproj -p:Platform=x64
```

單元與 ViewModel 測試：

```shell
dotnet run Tasks.cs test
```

Avalonia Headless UI smoke tests：

```shell
dotnet run Tasks.cs ui-test
```

Qt migration production app 與 tests：

```shell
cmake --preset debug -S qt
cmake --build qt/build/debug
ctest --test-dir qt/build/debug --output-on-failure
qt/build/debug/bin/PicLens.exe
```

Qt production app 已完成 search/grid/list、gallery selection/file operations、inline viewer、drag/drop rename、Windows input parity、portable 與 MSI candidate；PicLens 採 MIT License，本機 MSI fresh/upgrade/uninstall lifecycle、授權後 real-profile 副本驗證與跨平台 workflow actionlint 均已通過。主 `release` 命令已切換為 Qt candidate，但遠端 Linux/clean-Windows evidence 與 destructive release cutover 尚未完成，因此尚不可宣告正式切換完成。

Visual Studio 開發時開啟 `PicLens.slnx`，solution platform 選 `x64`。

## Release

免安裝資料夾：

```shell
dotnet run Tasks.cs release
```

輸出：

```text
artifacts/qt-portable/PicLens-win-x64/PicLens.exe
artifacts/qt-portable/PicLens-linux-x64/PicLens
```

這是 self-contained Qt runtime bundle，不是 single-file executable；散佈時請保留完整資料夾。暫時需要 Avalonia rollback artifact 時使用 `dotnet run Tasks.cs legacy-release`。

安裝檔：

```shell
dotnet run Tasks.cs installer
```

Package 版號讀取 repository root 的 `VERSION`；一般發版請先更新該檔。

輸出：

```text
artifacts/installer/PicLens-win-x64.msi
artifacts/installer/PicLens-1.0.0-fedora-x86_64.rpm
```

Windows installer 使用 WiX Toolset 建置 MSI；Linux installer 目前已實作 Fedora RPM，但產品支援範圍是主流 Linux 桌面發行版，不以 Fedora/RPM 作為最終完整範圍。Release 與 installer tasks 不會自動跑測試，打包前請先執行 `dotnet run Tasks.cs test`。

## 文件

- [產品規格](docs/product-spec.md)
- [Architecture](docs/architecture.md)
- [Runtime contract](docs/runtime-contract.md)
- [Testing](docs/testing.md)
- [Portable release](docs/portable-release.md)
- [Installer release](docs/installer-release.md)
- [Qt migration](docs/qt-migration.md)
