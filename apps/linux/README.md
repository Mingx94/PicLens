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

在 repo 根目錄執行：

```bash
bash packaging/arch/validate.sh inspect
bash packaging/arch/validate.sh build
```

`build` 在新建 `/tmp/piclens-arch-validation.*` configure、Release build、CTest 及 DESTDIR 暫存安裝；保留完整路徑與紀錄，不寫 `/usr`，不自動清除。CTest 使用 offscreen 與隔離 HOME／XDG／profile，並清空測試子程序的 `QT_QPA_PLATFORMTHEME`，避免 GTK 佈景仍要求螢幕連線。它不代表真實桌面通過。

CMake presets 為 `debug`、`release`、`windows-preview`。Windows 開發流程在 `apps/linux` 執行 `cmake --preset windows-preview`、`cmake --build --preset windows-preview`、`ctest --preset windows-preview`，需先讓 MSYS2 UCRT64 工具及 Qt DLL 可由 PATH 找到。

需要逐步開發時：

```bash
cmake -S apps/linux -B build/arch-debug -G Ninja -DCMAKE_BUILD_TYPE=Debug -DBUILD_TESTING=ON
cmake --build build/arch-debug
# 正式驗收使用上方腳本的隔離 CTest；不要讓檔案操作測試指向個人圖庫。
```

將下方路徑換成 `build` 模式輸出的真實位置：

```bash
QT_QPA_PLATFORM=wayland bash packaging/arch/validate.sh smoke /tmp/piclens-arch-validation.XXXXXXXX/stage/usr/bin/piclens
# 在真正 X11 登入工作階段另執行；Wayland 下強制 xcb 只算 XWayland。
QT_QPA_PLATFORM=xcb bash packaging/arch/validate.sh smoke /tmp/piclens-arch-validation.XXXXXXXX/stage/usr/bin/piclens
```

smoke 只開啟全新空 fixture 資料夾，使用 `--folder`、`--data-root`、`--smoke-ms 1500`，外層最多等待 30 秒，逾時視為失敗。不要把成功啟動當成 codec 或完整功能通過。

## 工作樹交付與封裝

見 [封裝說明](../../packaging/arch/README.md)。固定來源內容後產生快照。傳至 Arch 後以一般帳號 `makepkg`；不需要 tag、push 或 GitHub Actions。

## 實作與整合契約

Domain／Application／Services、Qt models、QML 與 helper 分層依照[架構](../../docs/engineering/architecture.md)。解碼使用 QProcess 子程序；QImageReader 加圖片外掛負責讀取，libwebp 負責無損編碼。RGBA 傳輸、紋理所有權、有界限佇列與完整原圖提交必須由程式與測試證實；本文件不把技術選型當成已完成證據。

App 必須能從 `/usr/bin/piclens` 找到 `/usr/libexec/piclens/piclens-worker`，開發建置則找到 build 目錄內 helper。CMake 需依[封裝安裝表](../../packaging/arch/README.md)安裝；打包檢查不會補上缺漏，未整合就會失敗。CLI 及資料根目錄遵守[資料延續性](../../docs/engineering/data-continuity.md)：`--data-root` 優先於 `PICLENS_DATA_ROOT`，預設 `$XDG_DATA_HOME/PicLens` 或 `~/.local/share/PicLens`。

完整功能、Wayland／X11、IME、DPI 與安裝生命週期的待辦見 [Arch TODO](../../TODO.arch.md)。GitHub 的 `arch/v*` tag 發布流程建置套件，但不跑功能／桌面／安裝測試；詳細產物見[Linux 發布指南](../../docs/linux/release.md)。
