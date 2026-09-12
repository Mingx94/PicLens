# Arch 平台驗證紀錄

驗證日期：2026-09-13（Asia/Taipei）  
runtime／tests 來源 commit：`27464f3fa33bf999540fc3745f07e3b596b2c74d`  
平台：Omarchy 4.0.3 x86_64、Hyprland Wayland、Qt 6.11.2、GCC 16.2.1、Intel UHD 620、Mesa 26.2.2。乾淨建置另使用官方簽章驗證的 2026.09.01 Arch bootstrap。

## 結論

原始 53 項中，48 項已有對應實作與證據。Debug 與 Release CTest 均為 6/6，分別耗時 43.81 秒與 42.61 秒。尚缺 A2.2、A7.3、A7.4、A7.5、A8.2。這代表目前可行項目已完成，不代表 KDE、X11、200%／原生輔助工具或代表性圖片效能已驗收。

## 本輪完成項目

| ID | 驗證結果 |
|---|---|
| A2.5 | 原生流程涵蓋 root 固定、後代展開／收合、樹與資料夾卡片導覽、前後歷史、滑鼠側鍵、F5 重新整理、快速切換清除舊選取；picker 路徑不變。 |
| A6.6 | Nautilus 實際開啟含空白、中文與 `;$()` 的正確資料夾。offscreen 失敗會顯示狀態並寫 log，選取與目前資料夾不變。只關閉測試建立的視窗。 |
| A6.7 | 三張 PNG 的原生 probe 確認實際放下、目標提示、多來源最小序號與取消不改檔；QtTest 另涵蓋 threshold、ghost、自動捲動與 ungrab／capture-lost 清理。固定 900×700 視窗在 Hyprland 平鋪時會變成 960×1054；只將該 PID 浮動到 900×700 後，同一 binary 通過。這是測試 geometry 前提，不是執行時失敗。 |
| A7.2 | 實際視窗與人工檢視涵蓋 1600×1000、800×600，以及 150% 時 1200×900 pixel／800×600 logical。工具列、側欄、捲軸與圖片比例正常。視窗操作只套用到測試 PID，未修改 Hyprland 設定。 |
| A8.1 | `--viewer`／`--metrics` schema、scene graph submission 觀測點、CPU 與 RSS／peak RSS 均有 Release 證據，shutdown 另由原生 probe 與 log 驗證。實際 `--viewer` 首次完整繪製提交為 216 ms。CLI 的 help、version、參數錯誤、寫入失敗與 JSON 成功路徑也以實際 PTY 驗證。 |
| A8.3 | 相同 Release Controller 與 Main QML 載入 10,000 筆 synthetic model：64 ms；搜尋 `image999` 得 11 筆：76 ms；捲到 2,000／4,000／6,000／8,000／9,999 時圖庫保持開啟。`maxMaterialized=38`、`maxVisible=13`、CPU 872 ms（8 logical CPU 正規化 2.37%）、RSS 180,011,008 B、peak 187,039,744 B、shutdown 低於毫秒時鐘解析度。 |
| A8.4 | 已逐節核對規格與驗收表。既有 codec、掃描、symlink、原子 profile、競態、取消、暫存、程序回收與資料失敗案例，加上本輪原生導覽、Reveal、drop、效能及生命週期證據，已涵蓋原條款。尚不可用的平台與素材條款仍以獨立 TODO 保留，不重複擴大 A8.4。 |
| A9.3、A10.3 | 在 rootless、單一 UID、隔離 Arch userland 中，只使用必要工具鏈、宣告的 Qt 依賴與基礎套件。未掛入主機 library、plugin、config、home 或 cache。建置時停用網路；build、6/6 CTest（42.85 秒）與 `.pkg.tar.zst` 均通過。套件 SHA-256 為 `593e3431f308a34758264ddc8c9eaac06498565d1f003ff6dee5d8e9051eb602`，來源快照 SHA-256 為 `5868e13e988de9757e53d0f0e0675600ba76285d6475ee58b1752f761b9e85bf`。 |
| A9.4 | 隔離 Arch 中實際以 pacman 安裝 `4.0.0-1`，透過 desktop entry 啟動，使用舊格式／大小寫混合／數字 enum JSON 還原 `/work/lifecycle-images`，再以相同已驗證來源的本機 `pkgrel=2` 測試升級與 smoke，最後解除安裝。App、helper、desktop entry 與 license 均移除；profile 全程保持相同 SHA-256 `2fb6b79a3bfe7a81cd42153537d5ab5da83d5c5d05b1c67facbb072f8d5c6bd6`。repository 的 `pkgrel` 仍為 1，未建立公開 release。 |

## 主要證據

證據保存在 [A-003 evidence](../../.agentflow/artifacts/A-003-arch-todo/evidence/)；以下路徑皆相對於 repository。

- 最終測試：[Debug CTest](../../.agentflow/artifacts/A-003-arch-todo/evidence/final-debug-ctest.txt)、[Release CTest](../../.agentflow/artifacts/A-003-arch-todo/evidence/final-release-ctest.txt)。
- 原生互動：[Controller 與 drop](../../.agentflow/artifacts/A-003-arch-todo/evidence/controller-native-final.json)、[Reveal](../../.agentflow/artifacts/A-003-arch-todo/evidence/reveal-native.json)、[視窗尺寸](../../.agentflow/artifacts/A-003-arch-todo/evidence/native-size.json)、[平鋪結果](../../.agentflow/artifacts/A-003-arch-todo/evidence/drag-native-first.txt)、[浮動結果](../../.agentflow/artifacts/A-003-arch-todo/evidence/drag-native-floating.txt)。
- 效能：[10,000 筆 metrics](../../.agentflow/artifacts/A-003-arch-todo/evidence/grid-final-release.json)、[程序觀測](../../.agentflow/artifacts/A-003-arch-todo/evidence/grid-final-process.json)、[Viewer 摘要](../../.agentflow/artifacts/A-003-arch-todo/evidence/viewer-final-summary.json)、[PTY CLI](../../.agentflow/artifacts/A-003-arch-todo/evidence/cli-final-pty.json)。
- 乾淨 Arch 與生命週期：[環境](../../.agentflow/artifacts/A-003-arch-todo/evidence/clean-arch.json)、[建置測試](../../.agentflow/artifacts/A-003-arch-todo/evidence/clean-build-ctest.txt)、[來源 manifest](../../.agentflow/artifacts/A-003-arch-todo/evidence/clean-source-manifest.json)、[套件 hash](../../.agentflow/artifacts/A-003-arch-todo/evidence/clean-package-sha256.txt)、[生命週期](../../.agentflow/artifacts/A-003-arch-todo/evidence/clean-lifecycle.json)、[desktop 啟動](../../.agentflow/artifacts/A-003-arch-todo/evidence/clean-desktop.json)。

## 證據界線

- 10,000 筆測試是 generated model，沒有圖片解碼；`libraryMilliseconds=0` 是舊欄位，不能視為磁碟掃描時間。CPU 與記憶體只含主程序。GPU、圖片複製與子程序成本未量測。
- Viewer 使用 synthetic fixture。cold／warm 指 App cache，不是清除 OS page cache；7 次完整繪製提交的最大值為 cold 336 ms、warm 269 ms，皆無 miss 或未繪製樣本。這些結果不完成 A8.2。
- 乾淨環境是 bwrap 隔離的 Arch userland，共用主機 kernel，不是完整開機 VM。rootless extraction 與私有 `pacman.conf` 未使用 `DownloadUser` 是已知限制；套件簽章要求未放寬。desktop 測試只分享主機 Wayland socket，沒有 GPU device mount；Mesa loader 警告表示不能推論硬體 GPU 效能。
- A2.2 仍缺原生 picker 選取／取消及啟動入口的操作證據。Hyprland 不能取代 KDE Plasma／Wayland、繁中 IME 或原生 X11。200% 與原生輔助工具仍未驗證。
- 沒有 public tag、GitHub Release 或 AUR 上傳。後續一般 repository commit／push 由 host 在交叉檢查後處理。
