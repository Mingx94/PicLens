* _2026-09-12 23:20:34 (gpt-5.6-luna/xhigh)_

# 文件與 UI 修正報告

基準為 `743199b619be46971323eb797acd89f5294e0e1c` 的獨立 clone。本次只修改指定的 README、測試指南、`ComponentPanel.qml` 與本報告；未修改 TODO，未提交 commit。

## 變更

- `apps/linux/README.md` 補上實際 CLI 參數、預設值、驗證規則、exit code、資料與輸出路徑、隔離的 Debug／Release／CTest／run／screenshot／metrics 命令，以及 metrics schema v1 的觀測界限。
- `docs/guides/testing.md` 改為不固定 CTest suite 數量。
- `apps/linux/qml/ComponentPanel.qml` 將 `standardButtons: Dialog.Close` 改成既有 `DialogButtonBox` 的 `RejectRole` 按鈕，文字為「關閉」，並以 `panel.close()` 保留關閉行為。沒有為 label-only 修改新增測試。

## 本次驗證

- 隔離 Debug 與 Release configure／build 成功；兩套目前 CTest suites 全數通過。
- offscreen `--components --smoke-ms` QML 載入回傳 `0`，沒有 QML 錯誤輸出。
- offscreen 元件展示截圖回傳 `0`，輸出檔成功建立。這不是原生 renderer 截圖。
- offscreen `--diagnostic-items 1000 --metrics --smoke-ms` 回傳 `0`，產生 `schemaVersion: 1` JSON。
- `--not-a-real-option`、缺少 `--width` 值、`--width nope` 均回傳 `2`。
- `git diff --check` 通過。

## 主機提供的驗證事實（2026-09-12）

以下只轉錄本次主機提供的結果，不延伸成未觀察的支援承諾：

- Omarchy `4.0.3` Arch derivative、`x86_64`、Hyprland Wayland、Qt `6.11.2`；GCC `16.2.1`、CMake `4.4.3`、Ninja `1.13.2`、libwebp `1.6.0`。
- Debug／Release configure、build 與目前所有 CTest 均通過，耗時分別為 `18.40`／`17.88` 秒。DESTDIR install、`desktop-file-validate` 與 AppStream 檢查通過；後者有 2 個 informational advisories：content-rating、developer-info。
- 原生 Wayland 空資料夾 smoke 正常結束，exit `0`，背景工作正常關閉。原生 OpenGL 為 Mesa `26.2.2`、Intel UHD620。
- 9 個 PTY 案例（help、version、invalid、missing、range、start、profile precedence）通過。
- 真實 10,000 項 synthetic diagnostic items 的 `maxMaterialized=38`、`maxVisible=12`、projection `33 ms`。compositor 將要求的 `1600x1000` tiled 為 `960x1054`；要求尺寸不是實際 render geometry。
- 六種產生圖片（JPEG 2400x1600、PNG 1800x2400、BMP 2048x1536、WebP 3000x2000、GIF 800x600、alpha PNG 1024x768）均完成 gallery thumbnail render。
- synthetic same-viewer A-B-A 共 7 個 samples：cold `58–186 ms`、warm `62–181 ms`，unpainted 為零。這不代表 representative-photo acceptance，也不代表冷 OS cache。
- 一般 `makepkg` baseline 通過並產生 `pkg.tar.zst`；這不是乾淨 OS 或完整 lifecycle 驗證。
- KDE、native X11、IME、accessibility、DPI、clean OS，以及 install／upgrade／uninstall 仍未證實。Docker access denied，sudo 需要密碼。
- 本次沒有授權 release、tag 或 AUR 動作，也不宣稱 Arch TODO 已完成。

## 限制

metrics v1 仍不是完整效能合約。README 已明確記錄目前欄位只有 scene graph submission 觀測，沒有 preview-ready timing、CPU、RSS 或 target-miss 欄位；完整桌面、安裝生命週期與代表性圖片效能仍須另外驗證。原生 renderer 最終截圖由主機執行。

Self-check: Bounded platform docs and one observed untranslated UI label.
