# 架構

## 目標

PicLens 是 Windows / Linux 的 Avalonia 圖片整理與檢視 app。UI shell 使用 Avalonia Desktop；可測試的 product rules、ViewModels 與 infrastructure behavior 分開放在 Core、Presentation 與 Infrastructure projects。

## Solution Layout

```text
PicLens.slnx
PicLens/                         Avalonia desktop app、AXAML views、assets、window setup
src/PicLens.Core/                Pure models、service contracts 與 deterministic domain rules
src/PicLens.Presentation/        UI-agnostic ViewModels 與 presentation service contracts
src/PicLens.Infrastructure/      JSON settings、filesystem、thumbnail、OS trash 與 logging services
tests/PicLens.Core.Tests/        pure domain behavior 的 xUnit tests
tests/PicLens.Infrastructure.Tests/ infrastructure behavior 的 xUnit tests
tests/PicLens.ViewModels.Tests/  ViewModel behavior 的 xUnit tests
tests/PicLens.Ui.Tests/          Avalonia Headless smoke tests
docs/                            專案文件
Tasks.cs                         repo-local dotnet file-based tasks
artifacts/portable/              產生的免安裝 release outputs
```

`PicLens.slnx` 是 Visual Studio 開發入口，包含 x86/x64/ARM64 solution platforms。`PicLens.ViewModels.Tests` 與 `PicLens.Ui.Tests` 載入在 solution 中但不參與預設 solution build；請透過 `dotnet run Tasks.cs test` 與 `dotnet run Tasks.cs ui-test` 做完整驗證。

## Avalonia App

`PicLens` 是 Avalonia Desktop app project，target `net10.0`。Windows manifest 只套用在 Windows build/publish。

App shell 使用：

- `Program.cs` 啟動 Avalonia classic desktop lifetime。
- `App.axaml` 設定 Fluent theme 與 Inter font。
- `MainWindow.axaml` 承載 `Views/MainView.axaml`。
- `MainView` code-behind 作為 composition root，手動建立 `MainPageViewModel` 與 Core/Infrastructure services，不引入 DI container。
- Avalonia storage provider、modal dialogs、pointer events、TreeView/ItemsRepeater tile selection、context menu、drag preview overlay 與 inline viewer input 都留在 view 層。

`MainView` 是目前的原生 shell：

- 沒有有效上一個資料夾時，瀏覽前會明確選取資料夾。
- 啟動時還原上一個開啟的資料夾。
- 以目前資料夾為 root 的 folder tree。
- 用於 history、sort key/direction、recursive mode 與 file operations 的 library toolbar。
- 用於 rename、OS trash 與 reveal-in-file-manager 的 image context menu。
- 混合資料夾/圖片的 thumbnail grid，still images 使用 asynchronous disk-cached thumbnails。
- Browser-style mouse side-button folder history navigation。
- File-operation status bar。
- 主視窗內嵌 image viewer。

Selection ownership 沿著 Avalonia 邊界切分：`LibraryTileItem.IsSelected` 是 visual tile selection 的來源，`MainPageViewModel` 負責 selected image paths、command availability 與繁體中文 selection summary。Clearing selection 必須同步 visual selection 與 view-model selection state，避免 reload 或 folder load 失敗後留下 stale selected paths。

## Presentation

`PicLens.Presentation` 負責 UI-agnostic ViewModels 與 presentation contracts：

- `MainPageViewModel` 協調 service-backed browsing、settings persistence、selection-derived state、file operations、drop-target rename preview planning、visible-tile thumbnail requests、toolbar command state，以及建立 image sequence snapshot。
- `ImageViewerWindowViewModel` 保留 viewer navigation、zoom、pan 與 unsupported animated-image feedback。
- `FolderTreeItem`、`LibraryTileItem` 與 navigation history 是 Avalonia shell 可直接 binding 的 presentation models。
- `IDialogService`、drop rename preview DTOs 與 `NullAppLogger` 放在 Presentation，方便 ViewModel tests 不依賴 app project。
- `DragInteractionRules` 保存 drag coordinate/autoscroll math，讓相關測試不需要引用 Avalonia view code。

## Core Domain

`PicLens.Core` 負責不需 UI framework 也應可測試的 product rules：

- Supported image extension detection。
- Animated GIF 與 WebP detection。
- List sorting，包含 folder-first behavior 與 Windows Explorer logical name ordering。
- Settings defaults 與 patch merge。
- Settings、scanning、thumbnails、file operations 與 app logger contracts。
- Deterministic rename planning 與 validation。
- Immutable image sequence snapshot creation。
- Shared path comparison helpers，Windows case-insensitive、Linux case-sensitive。
- Zoom clamping 與 pointer-anchored wheel zoom。

Filesystem、Avalonia controls、thumbnail codecs 與 OS trash behavior 應留在 Core 之外。

## Infrastructure

`PicLens.Infrastructure` 負責：

- JSON settings persistence。
- Direct 與 recursive folder scanning，包含 canonical directory de-duplication。
- Local app data 下的 disk thumbnail cache generation。
- OS trash operations：Windows recycle bin，Linux `gio trash`。
- 以不含副檔名的 sequence basename 找出最小可用序號的 drop-target batch rename execution。
- File-backed app logging。

Drop-target rename 的 deterministic plan 由 `PicLens.Core` 建立，`MainPageViewModel` 先把 plan 轉成 preview 並交給 view 顯示確認對話；使用者確認後才由 infrastructure 逐筆 `File.Move`。Sequence occupancy 以目標資料夾中不含副檔名的 basename 判斷，例如 `AAA-01.jpg` 會讓 `AAA-01.png` 視為已占用，且既有 `AAA-03.jpg` 在前面缺號時會被規劃到最小可用序號。Batch result 回到 ViewModel 統一更新 status，且每個 failed item 都要透過 app logger 寫入 ERROR context，方便後續排查 source path、target path 與 reason。

Main-grid thumbnails 會透過 `IThumbnailService` / `ThumbnailService` 以 SkiaSharp 產生為小型 PNG files。Tile loaded/materialized 會啟動 requests，tile unloaded 會取消 requests，而 ViewModel 會限制 concurrent thumbnail work，避免快速捲動時為 off-screen items 解碼大型 source images。每個 thumbnail request 都有 timeout，避免有問題的 decoder operation 永久佔用 background slot。Cache 位於 local app data，並會修剪到 bounded size。Full-size source files 仍只由主視窗內嵌 viewer 直接載入。
