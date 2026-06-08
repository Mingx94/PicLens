# 架構

## 目標

ImageViewerWin 是原生 Windows 圖片整理與檢視 app。它以 WinUI 3 patterns 建構主視窗、資料夾瀏覽、縮圖載入、選取操作、檔案操作與次要圖片檢視視窗，並讓可測試的 domain behavior 留在 UI 之外。

## Solution Layout

```text
ImageViewerWin.slnx
ImageViewerWin/                  WinUI 3 app、XAML views、app assets、window setup
src/ImageViewerWin.Application/  Service contracts 與 deterministic operation planning
src/ImageViewerWin.Core/         Pure models 與 domain rules
src/ImageViewerWin.Infrastructure/ JSON settings、filesystem、image 與 recycle-bin services
tests/ImageViewerWin.Application.Tests/ application behavior 的 xUnit tests
tests/ImageViewerWin.Core.Tests/ pure domain behavior 的 xUnit tests
tests/ImageViewerWin.Infrastructure.Tests/ infrastructure behavior 的 xUnit tests
tests/ImageViewerWin.ViewModels.Tests/ ViewModel 與 localization behavior 的 xUnit tests
docs/                            專案文件
tools/                           Repo-local 維護 scripts
artifacts/portable/              產生的免安裝 release outputs
```

## WinUI App

WinUI app 使用搭配 CommunityToolkit.Mvvm 的官方 MVVM template。Root window 是 `MainWindow`，它在 frame 中承載 `MainPage`、使用 Mica、設定原生 app icon，並將主視窗大小設為 `1220x820` logical pixels。

`MainPage` 是目前的原生 shell：

- 沒有有效上一個資料夾時，瀏覽前會明確選取資料夾
- 啟動時還原上一個開啟的資料夾
- 以目前資料夾為 root 的 folder tree
- 用於 history、sort key/direction、recursive mode 與 file operations 的 library command bar
- 用於 rename、recycle-bin trash 與 clearing selection 的 contextual selected-image action bar
- 混合資料夾/圖片的 grid，still images 使用 asynchronous disk-cached thumbnails
- Browser-style mouse side-button folder history navigation
- File-operation status bar

`MainPageViewModel` 協調 service-backed browsing、settings persistence、selection-derived state、保守的 file operations、visible-tile thumbnail requests，以及開啟次要 viewer window。XAML code-behind 限定處理 WinUI-only work，例如 pickers、dialogs、drag/drop events、GridView selection synchronization、GridView container preparation/recycling notifications、tile loaded/unloaded notifications，以及 launching windows。

Selection ownership 沿著 WinUI 邊界切分：`GridView.SelectedItems` 仍是 visual selection 的來源，`MainPageViewModel` 則負責 selected image paths、command availability，以及顯示在 contextual action bar 的繁體中文 selection summary。Clearing selection 必須先清除 visual `GridView` selection，再重設 view-model selection state，避免 reload 或 folder load 失敗後留下 stale selected paths。

`ImageViewerWindow` 顯示 `ImageSequenceSnapshot`，並提供 previous/next navigation、zoom in/out/reset、pointer wheel zoom、drag pan、fullscreen toggle、keyboard shortcuts、Escape close/focus behavior，以及 unsupported animated-image feedback。

## Assets

Windows 11 style app icon source 由 `tools/generate_app_icon.py` 產生。它會寫入原生視窗使用的 multi-size `Assets\AppIcon.ico`，以及 app manifest 使用的 WinUI logo PNGs。

## Core Domain

`ImageViewerWin.Core` 負責不需 WinUI 也應可測試的 product rules：

- Supported image extension detection
- Animated GIF 與 WebP detection
- List sorting，包含 folder-first behavior 與 Windows Explorer logical name ordering
- Settings defaults 與 patch merge
- Last-folder startup selection
- Immutable image sequence snapshot creation
- Zoom clamping 與 pointer-anchored wheel zoom

Filesystem、Windows UI、thumbnail codecs 與 recycle-bin behavior 應留在 Core 之外。

## Application And Infrastructure

`ImageViewerWin.Application` 負責：

- Settings、scanning、image data、thumbnails 與 file operations 的 service contracts
- Deterministic rename planning 與 validation

`ImageViewerWin.Infrastructure` 負責：

- JSON settings persistence
- Direct 與 recursive folder scanning，包含 canonical directory de-duplication
- Image file loading helpers
- Local app data 下的 disk thumbnail cache generation
- 保留 originals 並略過 collisions 的 JPG conversion
- Recycle-bin trash operations
- 會跳過既有 sequence targets 的 drop-target batch rename execution

Main-grid thumbnails 會透過 `IThumbnailService` / `ThumbnailService` 產生為小型 PNG files。GridView container preparation 與 tile materialization 會啟動 requests，recycling 或 tile unmaterialization 會取消 requests，而 view model 會限制 concurrent thumbnail work，避免快速捲動時為 off-screen items 解碼大型 source images。每個 thumbnail request 都有 timeout，避免有問題的 decoder operation 永久佔用 background slot，導致後續 visible tiles 無法載入。Cache 位於 local app data，並會修剪到 bounded size，保留最新 thumbnails、刪除較舊的 generated PNGs。Full-size source files 仍只由次要 image viewer 直接載入。

這樣的切分讓 WinUI views 保持精簡，同時把主要 product rules 留在可重用、已測試的 code 中。
