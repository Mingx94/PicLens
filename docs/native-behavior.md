# Native Behavior

這份文件追蹤 ImageViewerWin 目前支援與應保留的原生 app 行為。

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
- 用於 history、sorting、recursive mode 與 file operations 的 toolbar
- 用於 selected-image rename、recycle-bin trash 與 selection clearing 的 contextual action bar
- 包含 folders 與 supported image files 的 grid
- Status/error feedback area

目前的原生 app 以真實 filesystem-backed data、persisted settings、still images 的 disk-cached thumbnails、含 contextual actions 的 multi-selection、drag/drop rename、browser-style mouse side-button history navigation、繁體中文（台灣）runtime copy，以及 status feedback 實作這個 surface。

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
- Secondary viewer 中的 full-image loading
- Secondary image window 與 viewer controls
- Selection behavior，包含 selected-image summaries、single-image rename/trash command gating，以及 library reload 時的 stale-selection clearing
- Conservative file operations
- Recursive scanner canonical-directory de-duplication，處理 symlink/junction aliases
- Drop-target batch rename sequencing，會跳過既有 target paths
- Image viewer `1120x760` size，以及 Escape-to-main-window focus behavior

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
