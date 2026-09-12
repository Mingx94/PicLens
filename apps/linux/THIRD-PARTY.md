# Arch Qt 版第三方元件

PicLens 程式授權為 repo 根目錄的 MIT `LICENSE`。下表已依 2026-09-12 的 Arch 套件 metadata、安裝檔案、動態連結結果及實際內嵌資產核對。

| 元件 | 使用方式 | 授權與來源 |
|---|---|---|
| Qt Core／Gui／Quick／QuickControls2／Concurrent／Svg／Test | Arch 動態連結；Test 用於測試 | Arch 套件頁列 LGPL-3.0-only、GPL-3.0-only、商業授權及 Qt 例外；各模組／檔案條款以 Qt 隨附授權為準。[Qt licensing](https://doc.qt.io/qt-6/licensing.html) |
| Qt imageformats | 系統圖片外掛 | 同上；外掛所用 codec 另有授權，隨 Arch 套件提供 |
| libwebp | 系統動態函式庫，無損編碼 | BSD-3-Clause；[官方原始碼授權](https://chromium.googlesource.com/webm/libwebp/+/refs/heads/main/COPYING) |
| GLib / gio | 系統 helper | LGPL；[官方專案](https://gitlab.gnome.org/GNOME/glib)；不內嵌 gio binary |
| Lucide 圖示 | repo SVG，可能編入 Qt resources | ISC，完整文字：`assets/Icons/Lucide/LICENSE.txt`，需隨套件安裝 |
| Noto Sans CJK TC | 共用來源 tarball 含字型；若未編入 App，不是執行相依 | SIL OFL 1.1，完整文字：`assets/Fonts/NotoSansCJKtc-OFL.txt`；若內嵌／安裝字型也必須帶此授權 |
| PicLens 品牌圖示 | 現有 repo 品牌 PNG 的原樣副本 | 依 repo MIT；`packaging/arch/piclens.png` 為 48×48 圖示 |

Arch 套件使用系統 Qt／codec，不部署 Windows DLL、Rust 執行檔或開發機 Qt 外掛。本次套件保存 repo `LICENSE`、本表與 Lucide 完整授權文字；執行相依為 `qt6-base`、`qt6-declarative`、`qt6-svg`、`qt6-imageformats`、`qt6-wayland`、`libwebp`、`glib2`。若增加靜態連結或第三方資產，必須重新核對並更新本表。
