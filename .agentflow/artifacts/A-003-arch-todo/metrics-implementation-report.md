* _2026-09-13 00:13:28 (gpt-5.6-luna/xhigh)_

# Arch Metrics 實作報告

基準為 `87f2666`。本次只處理 A8.1 診斷輸出，沒有修改 `TODO.arch.md`，沒有 commit 或 push。這是獨立 clone，不能作為 host 的 Arch TODO 完成判定。

## 變更

- `apps/linux/src/controller.h`、`apps/linux/src/controller.cpp`
  - 保留 schema v1 與既有欄位。
  - `libraryMilliseconds`、`searchMilliseconds` 維持 legacy 初始數值 `0`；新加入且沒有樣本的欄位才使用 `null`。
  - 以 `lifetime_` 記錄第一筆成功可見 gallery 縮圖的 `firstThumbnailReadyMilliseconds`。
  - 以既有 `ImageItem::framePresented`／Qt `afterRendering` 觀測完整原圖提交；1024 預覽不會進入 full paint。
  - 每次 Viewer 選取建立 `selectionId`，並以 `viewerSessionId`、path 綁定預覽與完整圖樣本。重用有效預覽也會記錄時間，失敗選取保留 `null` 樣本。
  - 從既有 `paints_` 推導 `viewerSharpPaintCount`、maximum、`>500 ms` misses 與 `unpaintedSelections`；不建立第二份成功計數器。
  - `executePlans()` 啟動單一 batch `QElapsedTimer`。既有 `finished` callback 在 UI 結果渲染前寫入最新 `lastCompletedBatch`，包含 `total`、`succeeded`、`skipped`、`canceled`、`failed`、`unknown` 與 `durationMilliseconds`。
  - `failed` 沿用 `BatchResult.failed()`，因此包含 `unknown`；`unknown` 是子集合，不重複加總。只保留最新結果，批次執行中或完成 callback 尚未抵達時保留前一筆。
  - Linux 以 `getrusage(RUSAGE_SELF)` 量測 self CPU 與 peak RSS，以 `/proc/self/statm` 量測目前 RSS；標示 elapsed origin、logical-processor normalization、worker child 排除及 GPU／copy 限制。Windows preview 仍以 `null` 表示不可用。
  - 移除不屬於量測的固定限制欄位：`childProcessLimit`、`gpuSingleTextureEdgePixels`、`viewerOriginalRgbaLimitBytes`、`viewerPreviewRgbaLimitBytes`。
- `apps/linux/src/main.cpp`
  - Metrics JSON 的 `write()` 短寫入或 `flush()` 失敗回傳 `3`；成功行為仍回傳原本結果。
- `apps/linux/tests/metrics_test.cpp`
  - 先加入紅測試，捕捉缺少 `lastCompletedBatch`、取消確認仍產生／誤讀結果與缺少欄位移除。
  - 驗證 legacy timing 是 JSON number `0`，新 unavailable timing/resource 是 `null`。
  - 以真實 worker、`Controller`、`ImageItem` 與 offscreen `QQuickWindow` 驗證縮圖、預覽、完整圖、A-B-A、關閉重開、每次選取一次提交及延遲 paint 超標。
  - 以真實 Controller rename boundary 驗證同名略過批次的六類計數與耗時；取消 rename confirmation 驗證不產生 batch 結果。
  - 在 Linux 以 `/dev/full` 驗證 Metrics 輸出失敗回傳 `3`，並保留既有目錄輸出失敗測試。
- `apps/linux/CMakeLists.txt`
  - 保留 `metrics_test` target、CTest 設定與 offscreen 環境。
- `apps/linux/README.md`
  - 只更新 `Metrics schema v1`：補上 legacy `0` 例外、`lastCompletedBatch` 的欄位／單位／計時界線、`unknown` 子集合語意，以及移除的固定限制欄位；保留 CPU/RSS scope 與 GPU 未量測限制。

## 紅色證據

加入新測試後、實作修補前，Debug focused metrics suite 失敗 `3` 項：`lastCompletedBatch` 不存在、取消確認的值不是可用的 `null` 結果、完成批次物件沒有 `total=1`。這些失敗對應新增契約，而不是 mock summary 值。

## 綠色證據

所有命令都在隔離 build 與 `QT_QPA_PLATFORM=offscreen QT_QPA_PLATFORMTHEME=` 執行：

```text
cmake --build /tmp/piclens-arch-metrics/build-metrics -j2
ctest --test-dir /tmp/piclens-arch-metrics/build-metrics --output-on-failure
Debug：5/5 通過，19.74 sec

cmake --build /tmp/piclens-arch-metrics/build-release -j2
ctest --test-dir /tmp/piclens-arch-metrics/build-release --output-on-failure
Release：5/5 通過，19.34 sec
```

focused `metrics` suite 通過；其中包含 `/dev/full` 與既有目錄輸出失敗案例。實際 Release `piclens --diagnostic-items 3 --metrics ... --smoke-ms 100` smoke exit `0`，讀回 JSON 確認 `schemaVersion=1`、`libraryMilliseconds=0`、`lastCompletedBatch=null`、GPU 欄位為 `null`、`processScope=self` 與 `childProcessesIncluded=false`。

## 未證明項目

- 沒有宣稱完成整個 Arch TODO，也沒有宣稱 clean OS、KDE、Wayland／X11、IME、DPI、GPU compositor 或部署驗收。
- A8.2 的代表性混合圖庫 Release 冷／暖快取與完整原圖 500 ms 驗收尚未完成；小型 fixture 與延遲 paint 測試不代表實際圖片效能。
- A8.3 的 10,000 項目 Release 載入／搜尋／捲動、CPU、RSS／峰值記憶體與 shutdown 效能紀錄尚未完成。
- Metrics 的 CPU／RSS 是 Linux 主程序 self snapshot；worker child CPU／RSS、GPU 記憶體、scene graph 上傳副本與 compositor 呈現時間仍未量測。這些限制不能由本次測試推論。
- clone 中刻意保留 `TODO.arch.md` 原狀；host 端仍須依 native 證據決定 TODO 移除。繼承的 credential、network 與 provider 限制仍在。

Self-check: Preserve additive metrics compatibility, add bounded batch measurement, and reject unnecessary policy duplication.
