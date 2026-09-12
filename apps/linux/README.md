# PicLens Qt / Arch Linux

平台架構、設計與發布規則見[平台文件](../../docs/linux/README.md)。

新版目標為 Arch x86_64、C++20、Qt Quick，最低 Qt `6.8`。App 版本以 [CMakeLists.txt](CMakeLists.txt) 的 `project VERSION` 為準。App target 為 `piclens`，解碼 helper 為 `piclens-worker`。Windows MSYS2 Qt 可作開發建置；不能替代 Arch 建置、套件與桌面驗收。

## 平台限制

此 Qt App 的交付目標僅為 Linux／Arch；`windows-preview` 是開發與測試用途。正式 FileOperations 的改名與轉檔落地使用 Linux `renameat2(RENAME_NOREPLACE)`，不採會覆寫或跨裝置複製的 fallback。Windows 上此 no-replace mutation 路徑會 fail closed：回報失敗，不執行正式目標改名／提交。這不表示 Windows 完全不寫檔；隔離 profile、快取、測試 fixture 與編碼 helper 仍會寫入自己的資料。

Windows 測試不涵蓋 `Q_OS_LINUX` 條件內的成功改名、原子提交競態、部分完成取消及 trash 逾時案例；這些須在 Arch fixture 執行。App 的原圖提交是 scene graph submission，不是 compositor 呈現時間，也不是完整效能驗收。

## Arch 工具與相依

以下由使用者在 Arch 終端機自行執行；驗收腳本不會安裝套件。Arch 使用完整系統更新，避免部分升級。

```bash
sudo pacman -Syu --needed base-devel cmake ninja pkgconf git qt6-base qt6-declarative qt6-svg qt6-imageformats qt6-wayland libwebp glib2 desktop-file-utils appstream
```

`base-devel` 提供 GCC、makepkg 所需工具及 pkgconf。CMake 最低 3.25。Qt Core、Gui、Concurrent、Test 由 qt6-base 提供；Quick、QuickControls2 由 qt6-declarative 提供；Svg 由 qt6-svg 提供。qt6-imageformats 提供額外圖片外掛；libwebp 供直接無損編碼；glib2 提供 `gio trash`。圖片格式及無損結果仍須用 fixture 實測。

官方套件與用途：

| 套件 | 用途 |
|---|---|
| [qt6-base](https://archlinux.org/packages/extra/x86_64/qt6-base/) | Core／Gui／Concurrent／Test |
| [qt6-declarative](https://archlinux.org/packages/extra/x86_64/qt6-declarative/) | QML／Quick／Controls |
| [qt6-svg](https://archlinux.org/packages/extra/x86_64/qt6-svg/) | SVG 圖示 |
| [qt6-imageformats](https://archlinux.org/packages/extra/x86_64/qt6-imageformats/) | 包含 WebP 圖片外掛 |
| [libwebp](https://archlinux.org/packages/extra/x86_64/libwebp/) | WebP 編碼 |
| [glib2](https://archlinux.org/packages/core/x86_64/glib2/) | gio |
| [qt6-wayland](https://archlinux.org/packages/extra/x86_64/qt6-wayland/) | Wayland，必要相依 |

使用 Arch 套件庫提供的相依版本，不釘住舊版 Qt。PKGBUILD 的最低版本與來源 checksum 是不同用途。

## 建置與執行

以下命令都從 repository 根目錄執行。每次驗證使用新的 `RUN`，並隔離 `HOME`、XDG data/config/cache。`XDG_DATA_HOME` 必須是絕對路徑；程式只用它解析預設資料根目錄，設定與快取目錄也一併隔離 Qt 與平台狀態。

```bash
RUN="$(mktemp -d /tmp/piclens-docs-XXXXXX)"
export HOME="$RUN/home"
export XDG_DATA_HOME="$RUN/xdg-data"
export XDG_CONFIG_HOME="$RUN/xdg-config"
export XDG_CACHE_HOME="$RUN/xdg-cache"
export PICLENS_DATA_ROOT="$RUN/env-data"
mkdir -p "$HOME" "$XDG_DATA_HOME" "$XDG_CONFIG_HOME" "$XDG_CACHE_HOME" "$RUN/folder" "$RUN/artifacts"
```

Debug 與 Release 分開 configure、建置，再各自執行目前的 CTest suites：

```bash
cmake -S apps/linux -B "$RUN/debug" -G Ninja \
  -DCMAKE_BUILD_TYPE=Debug -DBUILD_TESTING=ON
cmake --build "$RUN/debug"
ctest --test-dir "$RUN/debug" --output-on-failure

cmake -S apps/linux -B "$RUN/release" -G Ninja \
  -DCMAKE_BUILD_TYPE=Release -DBUILD_TESTING=ON
cmake --build "$RUN/release"
ctest --test-dir "$RUN/release" --output-on-failure
```

隔離 smoke run 使用 `offscreen`，不代表 Wayland／X11 原生桌面驗收：

```bash
QT_QPA_PLATFORM=offscreen QT_QPA_PLATFORMTHEME= \
  "$RUN/debug/piclens" \
  --folder "$RUN/folder" \
  --data-root "$RUN/cli-data" \
  --smoke-ms 1500
```

CLI `--data-root` 優先於 `PICLENS_DATA_ROOT`。沒有這兩者時，Arch 預設為 `$XDG_DATA_HOME/PicLens`；若 `XDG_DATA_HOME` 未設定或不是絕對路徑，改用 `~/.local/share/PicLens`。資料根目錄下的設定、紀錄與縮圖路徑為：

```text
<data-root>/piclens-settings.json
<data-root>/Logs/PicLens.log
<data-root>/Thumbnails/thumbnails-v1/
```

截圖會先建立父目錄，再以 `QQuickWindow::grabWindow()` 寫入絕對化的 `--screenshot` 路徑。此範例也載入元件展示，讓 QML footer 一起經過載入：

```bash
QT_QPA_PLATFORM=offscreen QT_QPA_PLATFORMTHEME= \
  "$RUN/release/piclens" \
  --folder "$RUN/folder" \
  --data-root "$RUN/screenshot-data" \
  --components --width 1600 --height 1000 \
  --screenshot "$RUN/artifacts/components.png" \
  --smoke-ms 3000
```

Metrics 在事件迴圈結束、Controller 關閉背景工作後寫入；父目錄會先建立：

```bash
QT_QPA_PLATFORM=offscreen QT_QPA_PLATFORMTHEME= \
  "$RUN/release/piclens" \
  --diagnostic-items 10000 \
  --data-root "$RUN/metrics-data" \
  --metrics "$RUN/artifacts/metrics.json" \
  --smoke-ms 4000
```

原生桌面需要在對應登入工作階段執行。Wayland 使用 `QT_QPA_PLATFORM=wayland`；真正 X11 工作階段使用 `QT_QPA_PLATFORM=xcb`。不要把 offscreen 或容器結果當成桌面、codec 或完整效能驗收。

CMake presets 為 `debug`、`release`、`windows-preview`。Windows 開發流程在 `apps/linux` 執行 `cmake --preset windows-preview`、`cmake --build --preset windows-preview`、`ctest --preset windows-preview`，需先讓 MSYS2 UCRT64 工具及 Qt DLL 可由 PATH 找到。

快速驗證入口仍可在 repository 根目錄執行：

```bash
bash packaging/arch/validate.sh inspect
bash packaging/arch/validate.sh build
```

## CLI 參數與診斷語意

`piclens --help` 與 `piclens --version` 直接顯示資訊並正常結束。Qt parser 也提供對應的短選項。需要值的選項若缺值、未知選項、數字無法解析或低於下限，皆回傳 `2`。

| 參數 | 實際行為與預設值 |
|---|---|
| `--folder <path>` | 初始圖庫資料夾。未指定時讀取設定中的最後資料夾；空設定不會自動選資料夾。 |
| `--data-root <path>` | 本次 profile 根目錄；值去除前後空白後，非空值優先於 `PICLENS_DATA_ROOT`。相對路徑會轉成絕對路徑。 |
| `--smoke-ms <n>` | `n >= 0`；預設 `0`，不自動結束。大於零時排程結束。 |
| `--screenshot <path>` | 在視窗擷取並儲存 PNG 等由 Qt 判定的格式；輸出失敗回傳 `3`。 |
| `--metrics <path>` | 結束事件迴圈後輸出 JSON；檔案無法開啟等輸出失敗回傳 `3`。 |
| `--viewer <path>` | 以絕對路徑輪詢目前圖庫項目，每 50 ms 檢查一次，最多 10 秒，找到後開啟 Viewer。找不到不額外回傳錯誤。 |
| `--width <n>` | 初始寬度；預設 `1600`，最小 `800`。 |
| `--height <n>` | 初始高度；預設 `1000`，最小 `600`。 |
| `--diagnostic-items <n>` | `n >= 0`；預設 `0`。大於零時注入 `n` 個合成動畫 placeholder，不掃描資料夾、不解碼縮圖。 |
| `--dark` | 啟用深色 palette。 |
| `--exercise` | 執行非破壞性的搜尋、選取與 Viewer A-B-A 來回流程。 |
| `--components` | 顯示元件展示 Dialog；只展示控制項，不執行檔案操作。 |

數字欄位只接受整數。CLI parse／驗證、`piclens-worker` 缺失及 QML 根物件載入失敗回傳 `2`。截圖儲存失敗，或 metrics 輸出檔無法開啟、`write()` 短寫入、`flush()` 失敗，皆回傳 `3`；其他正常關閉為 `0`。

診斷計時器是固定的。`--diagnostic-items` 先在 `0 ms` 注入資料，接著在 `600/1200/1800/2400/3000 ms` 發出捲動位置。`--exercise` 在 `1200 ms` 開始，250 ms 後清除搜尋、選取第一個非資料夾項目並開啟 Viewer；之後在 `700/1400/2100/2800/3500/4200 ms` 交替前後移動，包含初始選取共可觀察 7 次選取。它不呼叫檔案轉換、重新命名或回收。

`--screenshot` 會在 `400/1000/1800 ms` 做暖身 `grabWindow()`；最後一次儲存時間為沒有 exercise 時 `max(2200, smoke-ms - 500)`，有 exercise 時 `max(6000, smoke-ms - 500)`。若啟用 smoke，含截圖時最早在 `2600 ms`（含 exercise 為 `6500 ms`）結束。隱藏或被遮住的視窗可能要等擷取後才提交 scene graph frame。

## Metrics schema v1

`--metrics` 輸出 JSON `schemaVersion: 1`。以下是目前實作的觀測值，不是完整效能合約。所有時間單位都是毫秒；沒有可用樣本的時間或資源數值是 JSON `null`，不把 `0` 當成未知。例外是相容性欄位 `libraryMilliseconds`／`searchMilliseconds`：尚未有掃描或 projection 觀測時仍保留初始數值 `0`。

| 欄位 | 型別與意義 |
|---|---|
| `frontEnd` | 固定為 `qt-quick`。 |
| `buildProfile` | 編譯時有 `NDEBUG` 為 `Release`，否則為 `Debug`。 |
| `qtVersion`／`platformPlugin` | `qVersion()` 與 `QGuiApplication::platformName()` 的執行時值。 |
| `itemCount` | 目前排序、搜尋後的 projection 項目數，包含資料夾項目；診斷模式則為合成項目數。 |
| `readyThumbnailCount` | 目前 gallery `imageKeys_` 中已取得且不是 `failed` 的縮圖數，不是完整圖庫成功總數。 |
| `maxMaterialized`／`maxVisible` | QML `GridView` 回報的 delegate／viewport 路徑高峰；不是解碼像素、程序記憶體或 compositor 像素。 |
| `libraryMilliseconds`／`searchMilliseconds` | 最近一次有效掃描／projection 排序、篩選與 model replace 的耗時；尚未有觀測時依 legacy 相容性保留數值 `0`。兩者都不是完整首屏呈現時間。 |
| `firstThumbnailReadyMilliseconds` | 從 Controller `lifetime_` 啟動到第一筆成功且仍屬可見 gallery 的縮圖交付；沒有成功縮圖為 `null`。 |
| `viewerSelections` | Viewer `showCurrent()` 的選取次數，包含開啟、前後移動與重新開啟。 |
| `viewerPreviewReadyMilliseconds` | 成功可用的 1024 預覽耗時陣列；可重用已有效預覽，沒有成功預覽為 `null`。1024 預覽不算完整原圖。 |
| `viewerPreviewSamples` | 每次選取一筆 `{selectionId, viewerSessionId, path, milliseconds, ready}`。`selectionId` 與 `viewerSessionId` 是字串；失敗或尚未有可用預覽時 `milliseconds` 為 `null`、`ready` 為 `false`。 |
| `fullPaintSamples` | 既有欄位。每次選取最多一筆成功完整圖提交，保留 `path`、`milliseconds`、`fullResolution: true`，並加入 `selectionId`、`viewerSessionId` 身分。 |
| `viewerSharpPaintMilliseconds` | `fullPaintSamples` 成功提交耗時的陣列；沒有成功提交為 `null`。 |
| `viewerSharpPaintCount`／`viewerSharpPaintMaximumMilliseconds` | 成功完整圖提交總數／最大耗時；count 沒有樣本時為 `0`，maximum 沒有樣本時為 `null`。 |
| `viewerSharpTargetMilliseconds`／`viewerSharpTargetMisses` | 目標固定為 `500`；misses 只計成功提交且耗時 `> 500` 的樣本。未提交樣本另計，不混入 misses。 |
| `unpaintedSelections` | 沒有成功完整圖提交的選取數，等於 `viewerSelections - viewerSharpPaintCount`；動畫、解碼失敗、取消或關閉都保留在這裡。 |
| `lastCompletedBatch` | 尚未由既有完成 callback 觀察到批次結果時為 `null`；否則為最新一筆 `{total, succeeded, skipped, canceled, failed, unknown, durationMilliseconds}`。耗時從接受執行的 `executePlans` 到既有 `finished` callback，單位為毫秒；只保留最新結果，批次進行中或 UI callback 尚未抵達時仍保留前一筆。`failed` 沿用 `BatchResult.failed()` 並包含 `unknown`；`unknown` 是其中的子集合，不可再相加。 |
| `observation` | 固定為 `scene graph submission; not compositor presentation`。`ImageItem::framePresented` 仍觀察 Qt `afterRendering`，不宣稱 OS compositor 已呈現。 |

CPU、RSS 與量測範圍欄位如下：

| 欄位 | 型別與意義 |
|---|---|
| `metricsTimestampUtc` | 輸出當下的 UTC ISO 8601 時間，含毫秒。 |
| `metricsElapsedMilliseconds` | 從 Controller 建構時的 `lifetime_` 到輸出的經過時間；可為有效的 `0`。 |
| `processCpuMilliseconds` | Linux `getrusage(RUSAGE_SELF)` 的 user + system CPU 毫秒，扣除 Controller 建構時基準；worker child 不含在內。非 Linux preview 或無法量測為 `null`。 |
| `averageCpuUtilizationPercent` | `processCpuMilliseconds / metricsElapsedMilliseconds / logicalProcessorCount * 100`，明確正規化為整台機器邏輯處理器容量；輸入不可用或經過時間為 `0` 時為 `null`。 |
| `cpuNormalizedByLogicalProcessors`／`logicalProcessorCount` | Linux 正規化標記與線上邏輯處理器數；非 Linux preview 為 `null`。 |
| `rssBytes`／`peakRssBytes` | Linux self process 的目前 resident RSS（`/proc/self/statm` resident pages）與 process lifetime peak（`ru_maxrss`），單位為 bytes；不可用為 `null`。 |
| `processScope`／`childProcessesIncluded` | 固定為 `self`／`false`；CPU、RSS 與峰值只屬於主程序，reaped worker child 不包含在內。 |
| `gpuMemoryBytes`／`gpuCopyBytes`／`gpuMetricsIncluded`／`imageCopyMetricsIncluded` | 固定為 `null`／`null`／`false`／`false`；GPU 記憶體、scene graph 上傳副本與 compositor 未量測。 |

`fullPaintSamples` 與 `viewerPreviewSamples` 的身分欄位避免 A-B-A、快速切換及關閉後重開的舊 callback 混入目前選取。`viewerSharpPaintCount`、maximum 與 target misses 都從既有成功 paint sample 推導，不另建第二份成功計數器。`lastCompletedBatch` 只保存最新已完成結果，不建立歷史 telemetry；`unknown` 已包含在 `failed` 內。CPU/RSS 是 metrics emission 當下的 Linux self snapshot；worker child、GPU 記憶體、像素複製與 compositor 限制已明確標示，不能用這份輸出宣稱代表性圖片效能或完整程序總記憶體。

## 工作樹交付與封裝

見 [封裝說明](../../packaging/arch/README.md)。固定來源內容後產生快照。傳至 Arch 後以一般帳號 `makepkg`；不需要 tag、push 或 GitHub Actions。

## 實作與整合契約

Domain／Application／Services、Qt models、QML 與 helper 分層依照[架構](../../docs/engineering/architecture.md)。解碼使用 QProcess 子程序；QImageReader 加圖片外掛負責讀取，libwebp 負責無損編碼。RGBA 傳輸、紋理所有權、有界限佇列與完整原圖提交必須由程式與測試證實；本文件不把技術選型當成已完成證據。

App 必須能從 `/usr/bin/piclens` 找到 `/usr/libexec/piclens/piclens-worker`，開發建置則找到 build 目錄內 helper。CMake 需依[封裝安裝表](../../packaging/arch/README.md)安裝；打包檢查不會補上缺漏，未整合就會失敗。CLI 及資料根目錄遵守[資料延續性](../../docs/engineering/data-continuity.md)：`--data-root` 優先於 `PICLENS_DATA_ROOT`，預設 `$XDG_DATA_HOME/PicLens` 或 `~/.local/share/PicLens`。

完整功能、Wayland／X11、IME、DPI 與安裝生命週期的結果見 [Arch 驗證紀錄](../../docs/linux/arch-validation.md)。GitHub 的 `arch/v*` tag 發布流程建置套件，但不跑功能／桌面／安裝測試；詳細產物見[Linux 發布指南](../../docs/linux/release.md)。

## 本機開發驗證（2026-09-12）

基準 commit：`743199b619be46971323eb797acd89f5294e0e1c`。以下為開發機基準結果。

以下保留早期開發機基準。最新桌面、效能與套件驗收結果見 [Arch 驗證紀錄](../../docs/linux/arch-validation.md)。

- Omarchy `4.0.3` Arch derivative、`x86_64`、Hyprland Wayland、Qt `6.11.2`；GCC `16.2.1`、CMake `4.4.3`、Ninja `1.13.2`、libwebp `1.6.0`。
- Debug／Release configure、build 與目前所有 CTest 均通過，耗時分別為 `18.40`／`17.88` 秒。DESTDIR install、`desktop-file-validate` 與 AppStream 檢查通過；後者有 2 個 informational advisories：content-rating、developer-info。
- 原生 Wayland 空資料夾 smoke 正常結束，exit `0`，背景工作正常關閉。原生 OpenGL 為 Mesa `26.2.2`、Intel UHD620。
- 9 個 PTY 案例（help、version、invalid、missing、range、start、profile precedence）通過。
- 真實 10,000 項 synthetic diagnostic items 的 `maxMaterialized=38`、`maxVisible=12`、projection `33 ms`。compositor 將要求的 `1600x1000` tiled 為 `960x1054`；要求尺寸不是實際 render geometry。
- 六種產生圖片（JPEG 2400x1600、PNG 1800x2400、BMP 2048x1536、WebP 3000x2000、GIF 800x600、alpha PNG 1024x768）均完成 gallery thumbnail render。
- synthetic same-viewer A-B-A 共 7 個 samples：cold `58–186 ms`、warm `62–181 ms`，unpainted 為零。這不代表 representative-photo acceptance，也不代表冷 OS cache。
- 一般 `makepkg` baseline 通過並產生 `pkg.tar.zst`；這不是乾淨 OS 或完整 lifecycle 驗證。
- 當時尚未證實 KDE、native X11、IME、accessibility、DPI 與套件生命週期；後續已完成，隔離 userland 仍不等同完整開機 OS。
- 沒有建立公開 release、tag 或上傳 AUR；平台驗收結果另見 Arch 驗證紀錄。
