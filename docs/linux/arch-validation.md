# Arch 平台驗證紀錄

驗證日期：2026-09-13（Asia/Taipei）

前輪 runtime／tests 來源 commit：`27464f3fa33bf999540fc3745f07e3b596b2c74d`

平台：Omarchy 4.0.3 x86_64、Hyprland Wayland、Qt 6.11.2、GCC 16.2.1、Intel UHD 620、Mesa 26.2.2。乾淨建置另使用官方簽章驗證的 2026.09.01 Arch bootstrap。

## 結論

原始 53 項已完成驗收。前輪完成 48 項，本輪補齊 A2.2、A7.3、A7.4、A7.5、A8.2，已移除全部待辦與 TODO 檔案。最新 Debug／Release 各 6/6 CTest 通過，耗時 45.78／51.86 秒；8 個真實 PTY 案例通過。

## 剩餘五項的最終驗證

| ID | 結果與證據 |
|---|---|
| A2.2 | 在 KDE 原生資料夾對話框實際選取、取消、重啟還原與選取空資料夾；取消保留設定 bytes。進入 child、返回與重新整理不改持久化 root。main 改用 `QApplication`，讓 KDE platform theme 提供原生 widget picker；Qt Widgets 已包含於既有 qt6-base 依賴。 |
| A7.3 | 私有 KDE Plasma／KWin 6.7.5 Wayland 工作階段，23 個檢查全數通過。以真實 XTest 輸入經 KWin 傳至 Wayland client，fcitx5-chewing 注音組字送出「中」。涵蓋 Ctrl+F、焦點、Viewer、選單、原生 picker、拖曳中 Esc、放下後取消、Dolphin 開啟正確資料夾與實際回收筒。 |
| A7.4 | 獨立 Xvfb 21.1.24／Openbox 3.6.1 原生 X11 工作階段，同樣 23 個檢查全通過。PicLens 使用 xcb，沒有以 Xwayland 代替 X11。 |
| A7.5 | X11／Wayland 均以 Qt 200% 縮放檢查 gallery、Viewer、原生 picker 與確認視窗。AT-SPI 驗證名稱、角色、圖片選取、排序選項選取、checkbox 勾選、slider 120–240 範圍、搜尋焦點及空圖庫按鈕停用；Orca 50.2 讀取真實 bridge 並產生繁中語音輸出。修正圖片 selected/selectable、排序選項無名及裝飾箭頭外露；新增兩個 QML 回歸測試。 |
| A8.2 | 9 檔混合素材，包含 3 張公開實拍照片、JPG／JPEG／PNG／BMP／無損 WebP、透明圖表及 12 MP 衍生拼圖。Release 冷暖各 12 次完整原圖繪製提交，最大 482／296 ms，超標 0、未完成 0。保留每筆 preview 與原圖 sample、selection/session ID、來源 hash、尺寸及錯誤。 |

最新證據在 [A-004](../../.agentflow/artifacts/A-004-arch-remaining/README.md)：[彙整](../../.agentflow/artifacts/A-004-arch-remaining/evidence/acceptance-summary.json)、[X11](../../.agentflow/artifacts/A-004-arch-remaining/evidence/x11-2-journey.json)、[KDE Wayland](../../.agentflow/artifacts/A-004-arch-remaining/evidence/kde-2-journey.json)、[冷快取](../../.agentflow/artifacts/A-004-arch-remaining/evidence/perf-final-cold-1.json)、[暖快取](../../.agentflow/artifacts/A-004-arch-remaining/evidence/perf-final-warm.json)、[來源檔案 hash](../../.agentflow/artifacts/A-004-arch-remaining/evidence/source-files.json)。

私有桌面使用獨立 DBus、HOME、XDG 與 PID／network namespace。KWin／Plasma 巢狀執行於私有 Xvfb；本輪沒有更動使用者桌面設定或安裝主機套件。共用主機 kernel，採軟體繪圖，並非完整開機 VM、實體 DRM 顯示器或硬體音訊驗證。Orca 的語音文字已觀察，未驗證實體喇叭或點字設備。

效能在主機 Hyprland／Intel UHD 620 的原生 Wayland 測量，實際視窗為 960×1054 logical、DPR 1。cold 指新的 App profile/cache；保留 OS page cache。每次程序內另含 A-B-A 與關閉後重開。500 ms 是 scene graph submission 的固定素材目標，沒有聲稱量到 compositor 呈現。舊版冷載入會先壓縮 cache PNG，獨立 worker 同一 preview 的不寫 cache／寫 cache 為 127／718 ms；現在只對內部 cache 使用 compression 0 的無損 PNG，磁碟檔案較大，2,000 檔上限不變。先前與桌面驗證或建置重疊的結果仍有超標，完整保留於證據，不作任意負載保證。

桌面依賴版本、可重跑方式、照片授權與先前測試工具錯誤均列於 A-004 說明。KDE native picker 採用 QApplication 的條件已核對 [KDE 原始碼](https://raw.githubusercontent.com/KDE/plasma-integration/Plasma/6.7/qt6/src/platformtheme/kdeplatformtheme.cpp)。

## 前輪已完成項目

| ID | 驗證結果 |
|---|---|
| A2.5 | 原生流程涵蓋 root 固定、後代展開／收合、樹與資料夾卡片導覽、前後歷史、滑鼠側鍵、F5 重新整理、快速切換清除舊選取；picker 路徑不變。 |
| A6.6 | Nautilus 實際開啟含空白、中文與 `;$()` 的正確資料夾。offscreen 失敗會顯示狀態並寫 log，選取與目前資料夾不變。只關閉測試建立的視窗。 |
| A6.7 | 三張 PNG 的原生 probe 確認實際放下、目標提示、多來源最小序號與取消不改檔；QtTest 另涵蓋 threshold、ghost、自動捲動與 ungrab／capture-lost 清理。固定 900×700 視窗在 Hyprland 平鋪時會變成 960×1054；只將該 PID 浮動到 900×700 後，同一 binary 通過。這是測試 geometry 前提，不是執行時失敗。 |
| A7.2 | 實際視窗與人工檢視涵蓋 1600×1000、800×600，以及 150% 時 1200×900 pixel／800×600 logical。工具列、側欄、捲軸與圖片比例正常。視窗操作只套用到測試 PID，未修改 Hyprland 設定。 |
| A8.1 | `--viewer`／`--metrics` schema、scene graph submission 觀測點、CPU 與 RSS／peak RSS 均有 Release 證據，shutdown 另由原生 probe 與 log 驗證。實際 `--viewer` 首次完整繪製提交為 216 ms。CLI 的 help、version、參數錯誤、寫入失敗與 JSON 成功路徑也以實際 PTY 驗證。 |
| A8.3 | 相同 Release Controller 與 Main QML 載入 10,000 筆 synthetic model：64 ms；搜尋 `image999` 得 11 筆：76 ms；捲到 2,000／4,000／6,000／8,000／9,999 時圖庫保持開啟。`maxMaterialized=38`、`maxVisible=13`、CPU 872 ms（8 logical CPU 正規化 2.37%）、RSS 180,011,008 B、peak 187,039,744 B、shutdown 低於毫秒時鐘解析度。 |
| A8.4 | 已逐節核對規格與驗收表。既有 codec、掃描、symlink、原子 profile、競態、取消、暫存、程序回收與資料失敗案例，加上本輪原生導覽、Reveal、drop、效能及生命週期證據，已涵蓋原條款。當時缺少的平台與素材條款另列待辦，本輪已完成，不重複擴大 A8.4。 |
| A9.3、A10.3 | 在 rootless、單一 UID、隔離 Arch userland 中，只使用必要工具鏈、宣告的 Qt 依賴與基礎套件。未掛入主機 library、plugin、config、home 或 cache。建置時停用網路；build、6/6 CTest（42.85 秒）與 `.pkg.tar.zst` 均通過。套件 SHA-256 為 `593e3431f308a34758264ddc8c9eaac06498565d1f003ff6dee5d8e9051eb602`，來源快照 SHA-256 為 `5868e13e988de9757e53d0f0e0675600ba76285d6475ee58b1752f761b9e85bf`。 |
| A9.4 | 隔離 Arch 中實際以 pacman 安裝 `4.0.0-1`，透過 desktop entry 啟動，使用舊格式／大小寫混合／數字 enum JSON 還原 `/work/lifecycle-images`，再以相同已驗證來源的本機 `pkgrel=2` 測試升級與 smoke，最後解除安裝。App、helper、desktop entry 與 license 均移除；profile 全程保持相同 SHA-256 `2fb6b79a3bfe7a81cd42153537d5ab5da83d5c5d05b1c67facbb072f8d5c6bd6`。repository 的 `pkgrel` 仍為 1，未建立公開 release。 |

## 前輪主要證據

證據保存在 [A-003 evidence](../../.agentflow/artifacts/A-003-arch-todo/evidence/)；以下路徑皆相對於 repository。

- 最終測試：[Debug CTest](../../.agentflow/artifacts/A-003-arch-todo/evidence/final-debug-ctest.txt)、[Release CTest](../../.agentflow/artifacts/A-003-arch-todo/evidence/final-release-ctest.txt)。
- 原生互動：[Controller 與 drop](../../.agentflow/artifacts/A-003-arch-todo/evidence/controller-native-final.json)、[Reveal](../../.agentflow/artifacts/A-003-arch-todo/evidence/reveal-native.json)、[視窗尺寸](../../.agentflow/artifacts/A-003-arch-todo/evidence/native-size.json)、[平鋪結果](../../.agentflow/artifacts/A-003-arch-todo/evidence/drag-native-first.txt)、[浮動結果](../../.agentflow/artifacts/A-003-arch-todo/evidence/drag-native-floating.txt)。
- 效能：[10,000 筆 metrics](../../.agentflow/artifacts/A-003-arch-todo/evidence/grid-final-release.json)、[程序觀測](../../.agentflow/artifacts/A-003-arch-todo/evidence/grid-final-process.json)、[Viewer 摘要](../../.agentflow/artifacts/A-003-arch-todo/evidence/viewer-final-summary.json)、[PTY CLI](../../.agentflow/artifacts/A-003-arch-todo/evidence/cli-final-pty.json)。
- 乾淨 Arch 與生命週期：[環境](../../.agentflow/artifacts/A-003-arch-todo/evidence/clean-arch.json)、[建置測試](../../.agentflow/artifacts/A-003-arch-todo/evidence/clean-build-ctest.txt)、[來源 manifest](../../.agentflow/artifacts/A-003-arch-todo/evidence/clean-source-manifest.json)、[套件 hash](../../.agentflow/artifacts/A-003-arch-todo/evidence/clean-package-sha256.txt)、[生命週期](../../.agentflow/artifacts/A-003-arch-todo/evidence/clean-lifecycle.json)、[desktop 啟動](../../.agentflow/artifacts/A-003-arch-todo/evidence/clean-desktop.json)。

## 前輪證據界線

- 10,000 筆測試是 generated model，沒有圖片解碼；`libraryMilliseconds=0` 是舊欄位，不能視為磁碟掃描時間。CPU 與記憶體只含主程序。GPU、圖片複製與子程序成本未量測。
- Viewer 使用 synthetic fixture。cold／warm 指 App cache，不是清除 OS page cache；7 次完整繪製提交的最大值為 cold 336 ms、warm 269 ms，皆無 miss 或未繪製樣本。當時的這些 synthetic 結果不足以完成 A8.2；本輪混合素材證據見上文。
- 乾淨環境是 bwrap 隔離的 Arch userland，共用主機 kernel，不是完整開機 VM。rootless extraction 與私有 `pacman.conf` 未使用 `DownloadUser` 是已知限制；套件簽章要求未放寬。desktop 測試只分享主機 Wayland socket，沒有 GPU device mount；Mesa loader 警告表示不能推論硬體 GPU 效能。
- 前輪尚缺 A2.2、KDE／X11、繁中 IME、200% 與輔助工具；已由本輪分別完成，不以 Hyprland 結果代替。
- 沒有 public tag、GitHub Release 或 AUR 上傳。後續一般 repository commit／push 由 host 在交叉檢查後處理。
