* _2026-09-13 01:20:03 (gpt-5.6-sol/low)_

# Arch TODO 最終獨立交叉檢查

Reviewed implementation commit: 9b7ee941ef75be673397f7c3f05bf02a87945815

Verdict: BLOCKING

Outcome: BLOCKING

Minimality: PASS

Conformance: BLOCKING

## 阻擋問題

1. `apps/linux/README.md:136` 對 metrics 短寫入的說明與同一提交的實作及測試相反。文件寫著「目前程式未把 metrics `write()` 的短寫入另轉成錯誤」。但 `apps/linux/src/main.cpp:53` 已檢查 `file.write(data) != data.size()` 與 `file.flush()`，失敗時回傳 `3`。`apps/linux/tests/metrics_test.cpp:285` 的 `metricsOutputFailureReturnsThree()` 也用 `/dev/full` 驗證此行為，並在本次重跑通過。這會讓 A0.4／A0.5 的 CLI 文件留下錯誤契約。請刪除該句，或改成短寫入與 flush 失敗皆回傳 `3`。修正只需文件變更，不需改程式。

## 結果重建

原始 `TODO.arch.md` 有 53 項。此提交移除 48 項，保留 5 項。保留項目為 A2.2 原生 picker 互動、A7.3 KDE Plasma／Wayland 與繁中 IME、A7.4 原生 X11、A7.5 200% 與原生輔助工具、A8.2 代表性混合圖片圖庫效能。這個處置正確。現有環境與素材不足以完成這 5 項，因此不能刪除 TODO，也不能宣稱原始 53 項已全部完成。

48 個移除項目有對應的既有程式、測試或本輪主機證據。新增的啟動路徑檢查屬於 START-01／DATA-01。搜尋清除按鈕與焦點處理屬於 SEARCH-01。`preventStealing`、ghost `Drag.source` 與實際 drag/drop 測試屬於 DRAG-01／DRAG-02。metrics 的 null 語意、selection/session ID、preview／full paint、target miss、batch、CPU／RSS 與量測範圍屬於 A8.1／A8.3。它們都有明確 owner outcome，沒有新增產品功能、持久化格式或信任邊界。

A8.2 的原始條件保持不變。現有 Viewer 資料是 synthetic fixture：cold 最大 336 ms、warm 最大 269 ms。它沒有代表性圖片 corpus，也沒有清除 OS page cache，因此只支援量測管線，不完成 A8.2。10,000 項測試是無解碼的 model；CPU／RSS 只涵蓋主程序，GPU、圖片複製與子程序成本未知。最終文件已正確揭露這些限制。

## 驗證

- 精確差異：讀取 `743199b619be46971323eb797acd89f5294e0e1c..9b7ee941ef75be673397f7c3f05bf02a87945815` 的 20 個變更檔。HEAD 與受審提交一致。
- 乾淨 Debug 建置：`cmake -S apps/linux -B /tmp/piclens-arch-crosscheck-build/debug -G Ninja -DCMAKE_BUILD_TYPE=Debug -DBUILD_TESTING=ON`，再執行 `cmake --build /tmp/piclens-arch-crosscheck-build/debug -j2`。結果成功。
- 完整測試：隔離 `HOME`、XDG 與 `TMPDIR`，設定 `QT_QPA_PLATFORM=offscreen`、空 `QT_QPA_PLATFORMTHEME`，執行 `ctest --test-dir /tmp/piclens-arch-crosscheck-build/debug --output-on-failure`。結果 6/6 通過，43.46 秒。
- QML 高風險重跑：`qml_test searchClearControlUsesActualInputAndPreservesProjection actualDragDropMouseJourneyOpensConfirmationAndCancelPreservesFiles`。結果 4 passed、0 failed（含 init／cleanup），603 ms。
- Metrics 高風險重跑：完整 `metrics_test`。結果 9 passed、0 failed，1757 ms。包含 legacy `0`／新增 `null`、sample ID、preview／full paint、未繪製、500 ms miss、batch counts／duration，以及 metrics 寫入失敗回傳 `3`。
- Controller 檢視：`controller_test -functions` 列出 `missingStartupPathKeepsPickerState`、`fileStartupPathKeepsPickerState`、`unreadableStartupPathKeepsPickerState`、`invalidRestoredStartupPathKeepsPickerState`。這些案例也已在完整 suite 通過。
- 主機紀錄：`final-debug-ctest.txt` 6/6、43.81 秒；`final-release-ctest.txt` 6/6、42.61 秒；`clean-build-ctest.txt` 6/6、42.85 秒。乾淨來源 manifest 的 94 個檔案 hash 與目前 runtime/test/package 檔一致；只有後續文件提交刻意改動的 `TODO.arch.md` 不同。
- 原生證據：動態三張 PNG drop probe 通過；固定 900×700 在 compositor 改成 960×1054 時失敗，而只浮動該 PID 後同一 binary 通過。這不能解讀成所有原生 QML 測試普遍通過。Reveal、視窗尺寸、10,000 model、Viewer、PTY CLI、套件建置及 install→本機 fixture pkgrel 2 upgrade→uninstall/profile 保留證據皆與最終文件敘述相符。
- 連結：`docs/linux/arch-validation.md` 列出的證據檔都可在主機 artifact 位置解析。來源內相對連結也可解析到對應 repository 路徑。
- `git diff --check` 只回報 `docs/linux/arch-validation.md:3-4` 的兩個 Markdown hard-break 行尾空白。這是 Markdown 換行語法，不是功能或交付阻擋問題。

## 限制與風險

本次沒有連入原生 Wayland socket，也沒有重做 KDE、X11、IME、200% 或輔助工具驗證。這些限制已由 5 個 TODO 保留。主機乾淨環境是 rootless Arch userland，共用 host kernel 與單一 UID，不是完整開機 VM；desktop launch 只分享一個 Wayland socket，沒有掛入 host library、cache、plugin 或 GPU device。Clone 本身也不是 OS confinement，仍繼承網路、credential 與絕對路徑可見性的限制。

除 README 的單一句子外，沒有發現可重現的 runtime、資料安全、架構或範圍問題。現有變更維持原架構與格式，沒有不必要的 refactor、rename、dependency 或相鄰修復。

Self-check: 已只建立 cross-check-report.md；未修改 source、test、config、docs、notebook 或系統，未 commit、push、install、連網、委派或啟動其他 reviewer；必要標記各出現一次，最後一行為本自我檢查。
