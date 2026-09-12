* _2026-09-13 01:22:14 (gpt-5.6-sol/low)_

# Arch TODO 最終獨立交叉檢查（修正驗證）

Reviewed implementation commit: 3d486321151e654de43dab62793a938cd435721e

Verdict: PASS

Outcome: PASS

Minimality: PASS

Conformance: PASS

## 結論

前次完整審查唯一的阻擋問題已修正。`apps/linux/README.md:136` 現在明確說明 metrics 輸出檔無法開啟、`write()` 短寫入或 `flush()` 失敗時，程序皆回傳 `3`。這與 `apps/linux/src/main.cpp:53` 的條件判斷一致，也與 `apps/linux/tests/metrics_test.cpp:285` 的 `metricsOutputFailureReturnsThree()` 一致。該測試用目錄路徑涵蓋開啟失敗，並在 Linux 以 `/dev/full` 涵蓋寫入或 flush 失敗。

修正提交的 parent 正是已完整審查的 `9b7ee941ef75be673397f7c3f05bf02a87945815`。精確差異只有 `apps/linux/README.md` 1 行新增、1 行刪除，以及 `docs/linux/arch-validation.md` 4 行新增、2 行刪除，共 2 個檔案、8 行變動。後者只把兩處 Markdown hard break 改成段落分隔。沒有 runtime、test、設定、介面、資料格式、工作區配置或信任邊界變更。`apps/linux` 排除 README 後，parent 到最終提交的 diff 為空。此範圍正好修正已確認的文件契約，沒有額外 refactor、rename、相鄰修復或依賴變更。

`git diff --check 9b7ee941ef75be673397f7c3f05bf02a87945815 3d486321151e654de43dab62793a938cd435721e` 無輸出。前次報告指出的兩處 Markdown 行尾空白也已消除。

## 原始目標與處置

原始 `TODO.arch.md` 有 53 項。最終結果仍是移除已有實作與證據的 48 項，保留 5 項：A2.2 原生 picker 互動、A7.3 KDE Plasma／Wayland 與繁中 IME、A7.4 原生 X11、A7.5 200% 與原生輔助工具，以及 A8.2 代表性混合圖片圖庫效能。這是真實且可行的交付，不代表 53 項全部完成。現有環境與素材無法證實這 5 項，因此 `TODO.arch.md` 應保留，不能刪除。

前次完整審查已逐項核對新增概念的 owner outcome。啟動路徑檢查對應 START-01／DATA-01；搜尋清除與焦點對應 SEARCH-01；`preventStealing`、ghost `Drag.source` 與實際 drag/drop 回歸測試對應 DRAG-01／DRAG-02；metrics 的 null 語意、selection/session ID、preview／full paint、target miss、batch、CPU／RSS 與量測範圍對應 A8.1／A8.3。修正提交沒有再增加概念或改變這些歸屬。

## 繼承驗證與本輪範圍

本輪沒有重跑產品建置或測試。原因是修正只有文件，且已確認 runtime／test bytes 相對完整審查提交完全相同。沒有新的行為或失敗需要重複完整 suite。

前次獨立完整審查在相同 runtime／test bytes 上完成 Debug 建置，完整 CTest 為 6/6 通過、43.46 秒。實際 `LastTest.log` 顯示 imaging 22、app 7、controller 12、QML 11、metrics 9 個案例皆通過；metrics 共 9 passed、0 failed，包含 `metricsOutputFailureReturnsThree()`。該輪另重跑指定 QML 搜尋／drag 高風險案例及完整 metrics 測試。主機紀錄另有 Debug 6/6、43.81 秒，Release 6/6、42.61 秒，以及乾淨 Arch userland 6/6、42.85 秒。runtime／test 基準為 `27464f3fa33bf999540fc3745f07e3b596b2c74d`，其後只有 TODO 與文件提交。

原生證據的界線不變。動態三張 PNG drop probe 通過；固定 900×700 測試在 compositor 改成 960×1054 時失敗，只將該 PID 浮動後，同一 binary 通過。這不代表所有原生 QML 測試普遍通過。乾淨環境是使用官方簽章素材的 rootless Arch userland，共用主機 kernel 與單一 UID，不是完整開機 VM。desktop 啟動只分享一個 Wayland socket，沒有證實 KDE 或 X11。

A8.2 仍未完成。Viewer 使用 synthetic fixture，cold 最大 336 ms、warm 最大 269 ms；cold／warm 是 App cache 語意，不是清除 OS page cache。原始條件不要求清除 OS page cache，因此前次報告的 page-cache 說明只是量測範圍，不是新增驗收條件。10,000 項測試是未解碼圖片的 generated model。CPU／RSS 只量主程序，GPU、圖片複製、compositor 與子程序成本未涵蓋。這些資料只能證實量測管線，不能取代代表性混合圖片 corpus。

Self-check: 已直接核對最終 commit、parent、精確 diff、README 契約、main.cpp、metrics_test、完整 CTest log、代表性主機證據、TODO 處置與 git diff --check；本輪未重跑產品建置或測試，只新增 cross-check-report.md，未修改 source、test、config、docs、notebook 或系統，未 commit、push、install、連網、委派或啟動其他 reviewer；必要標記各出現一次，且本行為最後一個唯一內容行。
