# 架構

## 目標

PicLens 是 Windows / 主流 Linux 的 Avalonia 圖片整理與檢視 app。UI shell 使用 Avalonia Desktop；可測試的 product rules、ViewModels 與 infrastructure behavior 分開放在 Core、Presentation 與 Infrastructure projects。

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

## Qt Migration Coexistence

完整產品遷移期間，Avalonia/.NET 與 Qt production code 會並存：

```text
qt/                             正式 Qt 6 / C++20 production tree
qt/src/core/                    Qt Core-based deterministic domain library
qt/src/infrastructure/          App-data、settings、logger、scanner 與 file operations
qt/src/presentation/            Library/folder-tree models、history 與 async state
qt/src/app/                     Startup、settings 與 service composition
qt/qml/PicLens/                 Production Qt Quick shell、theme 與 reusable controls
qt/tests/core/                  Qt Test / CTest parity coverage
qt/tests/infrastructure/        Isolated persistence、scanner 與 file-operation tests
qt/tests/presentation/          Presentation state、race 與 large-list tests
qt/tests/app/                   Startup restore、persistence 與 root-context tests
qt/tests/qml/                   Qt Quick Test component/runtime coverage
poc/qtquick/                    Qt Quick API 與效能實驗，不作為 production architecture
```

目前 `qt/` 已具備 CMake presets、`PicLens::Core`、`PicLens::Infrastructure`、`PicLens::Presentation`、`PicLens::App` static libraries，以及可直接啟動的 `PicLens` Qt Quick executable。Production QML shell 包含嵌入字型/圖示、light-theme tokens、native folder picker、command/status bars、lazy virtualized folder tree、reusable thumbnail gallery，以及 empty/loading/error states。主 portable release 命令已指向 Qt candidate；Avalonia app 與 `legacy-release` 在完整互動、OS integration、packaging 與跨平台 gates 通過前保留作 rollback baseline。每個 legacy component 只會在對應 Qt behavior、tests、diagnostics 與 platform consumers 都通過 removal gate 後移除。

Qt `LibraryController` 是目前 folder navigation、sort、recursive mode、busy/error/status 與 selection invalidation 的單一 presentation owner。Scanner 在容量為 2 的專用 worker pool 執行，每個請求攜帶 generation 與 cooperative stop token；結果只在 owning thread 套用到 `LibraryItemModel`。10,000-item replacement 使用一次 model reset。Sort/include-subfolders/last-folder persistence 以 request signals 交給 `AppController` 的單一 worker queue 寫入 `JsonSettingsStore`，不在 GUI thread 做同步磁碟 I/O。

Gallery selection 同樣由 `LibraryController` 單一持有：一般點擊、Ctrl toggle、Shift range、selection order 與右鍵 scope 都先更新 model 的 `selected` role，QML delegate 不保存第二份 selection set。`FileOperationController` 依 selection/visible-image snapshot 暴露 rename、trash、convert、same-basename cleanup 與 cancel command state，將注入的 infrastructure functions 放到容量為 1 的 worker pool 執行，逐項 failure 透過 `AppController` 寫入 file log；完成後清除 selection，且只在原 folder context 仍有效時 refresh。

Drag/drop rename 的 pointer threshold、capture lifecycle、preview position、target hit testing 與 33ms edge autoscroll 留在 QML view；可測試的 autoscroll math 位於 Core。`FileOperationController` 從 `LibraryController` 擷取 immutable ordered sources，建立 typed deterministic preview，只有收到明確確認後才在 worker 執行 batch rename；取消、capture loss、完成與 failure 都清除 drag/preview state，逐項失敗沿用 file-operation diagnostics。

Window-level `HistoryMouseHandler` 只接受 `Qt.BackButton` / `Qt.ForwardButton`，將 browser-style mouse side buttons 路由到與 toolbar 相同的 `AppController` history commands；一般 left/right/middle input 不會被攔截，viewer 開啟時停用，避免在 overlay 下改動 folder history。

`FolderTreeModel` 使用 hierarchical `QAbstractItemModel`，root/path reconstruction 與 lazy children scans 都有 generation/cancellation guard。`AppController` 組合 settings store、logger、scanner、library controller 與 folder-tree model；settings load/update 使用單一 worker queue，只有 picker selection 更新 startup folder，tree/history navigation 不會覆寫它。History entry 同時保存 folder path 與 tree-root context，Back/Forward 可跨 picker roots 還原正確樹狀範圍。

Qt thumbnails 由 `ThumbnailService` 在 worker 中以 source path、mtime、file size 與 requested size 建立 SHA-256 cache key，透過 `QImageReader` scaled decode 與 auto transform 產生 atomic PNG cache，並做 bounded pruning。`ThumbnailCoordinator` 只接收 materialized tile requests，使用 logical concurrency 4 與較大的 physical isolation pool；timeout/cancel 會立即釋放 logical slot，而 late decoder result 由 generation/request identity 丟棄。成功路徑只在 owning thread 更新 `LibraryItemModel.thumbnailPath` / `thumbnailUrl` roles，size/navigation changes 會清除舊 thumbnails。`GridView.reuseItems` delegate 只在 materialized still-image tile request thumbnail，pool/destruction 時取消請求，避免快速捲動把 off-screen work 留在 decoder queue。

QML-facing C++ types 透過 declarative foreign-type registration 暴露為不可由 QML 建立的 typed properties；QML 只呼叫 `AppController` command surface，不直接做 filesystem、settings 或 thread work。Production process 提供隔離 `--data-root` 與 bounded `--smoke-ms` diagnostics options，讓 runtime CTest 不會污染使用者 app data。

`ViewerController` 在開啟時接收由 `LibraryController` 建立的 immutable `ImageSequenceSnapshot`，單一持有 current index、zoom 與 pan state；library reload 不會改動已開啟序列。QML `ViewerOverlay` 只呈現 full-image URL、animated feedback 與輸入，pointer-anchor zoom 使用 Core `zoom_math`，navigation 會重設 transform。Viewer open/close/load failure 透過 `AppController` 寫入 lifecycle diagnostics。

Qt target dependency direction：

```text
QML views -> presentation models/controllers -> core contracts/rules
                                          -> infrastructure adapters
```

Core 可使用 Qt Core value types，但不可依賴 Qt Quick、QML、GUI controls 或平台視窗。

Release installation follows Qt's CMake deployment model: the app target has standard GNU install destinations, Linux-relative RPATH, desktop entry, icon and license install rules. Portable/Ubuntu builds use the generated QML deployment script；Fedora RPM 使用 `PICLENS_USE_SYSTEM_QT` 和 RPM dependencies，不複製 distro Qt libraries。System packages 保留 app/runtime 在 `/opt/piclens`，但 desktop entry/icon 進入標準 `/usr/share`。Windows keeps a reproducible `windeployqt` wrapper because the local MSYS2 toolchain needs an additional PE dependency closure.

## Avalonia App

`PicLens` 是 Avalonia Desktop app project，target `net10.0`。Windows manifest 只套用在 Windows build/publish。

App shell 使用：

- `Program.cs` 啟動 Avalonia classic desktop lifetime。
- `App.axaml` 設定 Fluent theme 與 embedded Noto Sans CJK TC font。
- `MainWindow.axaml` 承載 `Views/MainView.axaml`。
- `MainView` code-behind 作為 composition root，手動建立 `MainPageViewModel` 與 Core/Infrastructure services，不引入 DI container。
- Avalonia storage provider、modal dialogs、pointer events、TreeView/ItemsRepeater tile selection、context menu、drag preview overlay 與 inline viewer input 都留在 view 層。

`MainView` 是目前的原生 shell：

- 沒有有效的資料夾選擇器選取資料夾時，瀏覽前會明確選取資料夾。
- 啟動時還原上次透過資料夾選擇器選取且仍可用的資料夾。
- 資料夾樹、資料夾縮圖、上一頁或下一頁導覽不會覆蓋啟動還原資料夾。
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
