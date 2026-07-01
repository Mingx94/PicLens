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
dotnet run Tasks.cs run
```

只建置、不啟動 app：

```shell
dotnet run Tasks.cs run --skip-run
```

單元與 ViewModel 測試：

```shell
dotnet run Tasks.cs test
```

Avalonia Headless UI smoke tests：

```shell
dotnet run Tasks.cs ui-test
```

Visual Studio 開發時開啟 `PicLens.slnx`，solution platform 選 `x64`。

## Release

免安裝資料夾：

```shell
dotnet run Tasks.cs release
```

輸出：

```text
artifacts/portable/PicLens-win-x64/PicLens.exe
artifacts/portable/PicLens-linux-x64/PicLens
```

這是 framework-dependent portable output，不是 single-file executable；散佈時請保留完整資料夾。

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
