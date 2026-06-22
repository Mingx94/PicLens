# Native Behavior

這份文件追蹤 PicLens 目前支援與應保留的原生 app 行為。

## 支援格式

原生 app 支援下列 image extension gate：

- `jpg`
- `jpeg`
- `png`
- `bmp`
- `webp`
- `gif`

Animated GIF 與 animated WebP 會被偵測出來，並在目前 product scope 中視為不支援顯示。若未更新這份文件與 tests，不要加入 AVIF、HEIC、TIFF、SVG 或 animation playback。

## Main Window

目前目標：

- 預設 size：`1220x820`
- 沒有有效上一個資料夾時，瀏覽前會明確選取資料夾
- 上一個開啟的資料夾仍可用時，啟動時會還原
- 以目前資料夾為 root 的 folder tree
- 用於 history、sorting、recursive mode 與 file operations 的 toolbar；較少用的 file operations 由 48x48 icon-only 更多動作選單呈現，menu 由按鈕下方往下展開，避免觸發 `CommandBar.SecondaryCommands` 的整列 overflow chrome
- 用於 selected-image rename 與 recycle-bin trash 的 image context menu
- 包含 folders 與 supported image files 的 grid
- Status/error feedback area

目前的原生 app 以真實 filesystem-backed data、persisted settings、still images 的 disk-cached thumbnails、含 contextual actions 的 multi-selection、drag/drop rename、browser-style mouse side-button history navigation、繁體中文（台灣）runtime copy，以及 status feedback 實作這個 surface。

主視窗功能應維持下列互動：

- Back/forward buttons 與 mouse side buttons 都使用同一組 folder history。
- Sort menu 透過 ViewModel command 支援名稱與修改時間的 asc/desc 排序。
- Recursive mode 透過 ViewModel command 保存設定、重新載入 library，並清除目前 selection。
- Thumbnail size slider 會保存設定、取消既有 thumbnail requests，並重新排程 visible tiles。
- Library reload、folder navigation、sort、recursive mode 與 file operations 都不可留下 stale selected image paths。
- GridView 資料夾 tile 左鍵單擊會進入資料夾；雙擊只對圖片開啟 viewer，不對資料夾執行額外動作。
- 左鍵點選圖片只更新 `GridView` selection，不顯示底部 contextual action bar。
- 圖片右鍵選單比照 Explorer selection scope：右鍵已選圖片時作用於目前 selection，右鍵未選圖片時先改成只選該圖片；選單可用 command-backed「在檔案總管中顯示」開啟 Explorer 並選取該圖片；資料夾 tile 不提供圖片 rename/trash 選單。
- Rename 僅允許單張圖片；trash 可處理一張或多張 selected images。
- Clear selection 必須同時清除 visual `GridView.SelectedItems` 與 ViewModel selection state。
- Drag/drop rename 只支援 app 內圖片拖到另一張圖片；按下 tile 時不可立即 capture pointer，必須等移動超過拖曳門檻才 capture，避免阻擋 `GridView` 內建 selection checkbox 勾選。拖拉開始後應顯示跟隨 pointer 的 drag preview overlay，並 highlight 目前可放下的 target。Preview overlay positioning 使用 `LibraryGrid` local 座標；drop target hit-test 必須轉成 root/host 座標後再呼叫 WinUI host-coordinate API，避免側欄與 header offset 造成座標偏移。Pointer cancel/capture-lost 必須清掉 drag state，並在執行前顯示 rename preview confirmation。Pointer、selection、container recycling、loaded/unloaded 與 folder expanding handlers 是保留的 WinUI lifecycle glue，不應改成 ViewModel state。

## Inline Image Viewer

圖片預覽顯示在主視窗內，不另外開啟視窗。Inline viewer 使用 immutable image sequence snapshot，不會因主視窗 reload 直接改動已開啟 viewer 的 navigation list。

目前支援：

- Viewer 開啟時，主視窗 app bar 會顯示 `PicLens - 目前圖片名稱`；Viewer overlay 只包含上方 navigation/zoom command strip 與主要圖片畫布，不保留底部狀態列。
- Previous/next image navigation。
- Wheel zoom、toolbar zoom in/out/reset，以及 pointer-anchored zoom math。
- Drag pan 與 keyboard pan；未 zoom in 時，left/right keyboard navigation 可切換圖片。
- Escape 或 viewer close button 關閉預覽，並回到 main gallery focus。
- App bar 顯示目前圖片名稱；Viewer command strip 只保留 icon commands，不顯示位置或 zoom percentage 文字。
- Animated GIF/WebP 顯示 unsupported feedback，不嘗試播放。

## Thumbnail Behavior

Thumbnail loading 應維持下列規則：

- 只針對 visible/materialized still image tiles 排程 thumbnail request。
- GridView container recycling 或 tile unload 時取消對應 request。
- Thumbnail work 有 concurrency limit 與 per-request timeout，避免 stalled decoder 阻塞後續 visible tiles。
- UI-bound thumbnail path update 必須回到 dispatcher/UI thread。
- Cache 位於 `%LOCALAPPDATA%\PicLens\Thumbnails`，以 generated PNG files 保存並做 bounded pruning。

## 已涵蓋的 Core Behaviors

Core test suite 涵蓋：

- Image extension support
- Animated GIF/WebP checks
- Folder-first sorting，包含 Windows Explorer logical name ordering
- Settings merge
- Last-folder startup selection
- Image sequence snapshot immutability
- Zoom clamp 與 pointer-anchor math

## 目前支援與後續工作

目前原生 milestone 已實作：

- Settings persistence
- Direct 與 recursive folder scanning
- 透過 GridView container events 與 local app data 下 bounded disk cache 執行 visible-tile thumbnail loading，並用 per-request timeouts 讓 decoder stalled 時後續 visible tiles 仍能繼續載入
- Inline viewer 中的 full-image loading
- Main-window inline viewer controls
- Selection behavior，包含 image context menu、single-image rename gating、multi-image trash，以及 library reload 時的 stale-selection clearing
- Conservative file operations
- Recursive scanner canonical-directory de-duplication，處理 symlink/junction aliases
- Drop-target batch rename sequencing，會用不含副檔名的 sequence basename 避開既有命名、補最小可用序號，並在使用者確認 preview 後才執行
- Image viewer 的 Escape-to-main-gallery focus behavior

剩餘 follow-up work 應聚焦在更深入的 GUI automation 與 polish，而不是缺少 service wiring：

- Rectangle drag selection
- File operation dialogs 的 automated WinUI UI tests
- 跨磁碟 real Windows recycle-bin behavior 的 manual smoke coverage
- Thumbnail cache behavior 的 larger-library performance profiling

## 需保留的 File Management Rules

原生 file-management behavior 應保持保守：

- Trash-like operations 送到 OS recycle bin。
- JPG conversion 會保留 original files。
- Target collisions 會略過，不覆寫。
- Batch operations 會回報 per-file results。
- Failures 會逐項繼續處理。
- Single rename 只驗證 basename，same-name skips 回報為 `same_name`，並將 existing targets 視為 invalid request。
- Drop-target batch rename 以不含副檔名的 sequence basename 判斷占用；例如 `AAA-01.jpg` 會讓 `AAA-01.png` 視為已占用，`AAA-03.jpg` 在前面缺號時會被重新規劃到 `AAA-01.jpg`。
- Drop-target batch rename 失敗項目必須逐筆寫入 ERROR log，包含 source path、target path 與 reason。

## Diagnostics

開發時要保留 ERROR LOG，讓 runtime crash 或使用者回報可以追溯：

- App log path：`%LOCALAPPDATA%\PicLens\Logs\PicLens.log`。
- App startup、main navigation、thumbnail failures、folder tree child-load failures、file-operation failures、drop-target batch rename per-item failures 與 viewer lifecycle 都應寫入足夠 context。
- WinUI/native crash 診斷不能只看 build；應補短時間 runtime launch 與 app log tail。
