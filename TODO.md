# TODO

這份文件記錄 `experiment/gpui` 相對於 `main` 尚未完成的功能與驗證工作。比對基準是 `main` / `origin/main` 的 `a292998c`，GPUI 分支基準是 `18b95aab`。

[產品規格](docs/product-spec.md) 與 [runtime invariants](docs/runtime-invariants.md) 仍是行為權威。完成項目時，應刪除對應待辦，並在需要時同步更新權威文件與測試。

## P1：恢復核心互動與檔案操作安全

### 多選語意

- [ ] 加入 accessibility action 的 deterministic GPUI test；pointer、modifier、keyboard 與 focus 已有 GPUI test。

### 檔案操作確認

- [ ] 移至回收筒前顯示包含圖片數量的確認 dialog；取消不得修改檔案。
- [ ] 清除同名格式前說明保留 JPG/JPEG 與 WebP 的規則，並要求確認。
- [ ] JPG / WebP 轉換在目前結果達 50 張以上時要求確認，並清楚顯示作用範圍與原檔保留規則。
- [ ] Dialog 不可自動確認；Escape、取消按鈕與 focus restore 必須使用同一個關閉路徑。
- [ ] Toolbar、native menu、context menu、keyboard action 與 accessibility action 必須共用相同的 enabled state 與確認流程。

### 可取消且不阻塞 UI 的檔案操作

- [ ] 將 trash、single rename、drop rename、JPG conversion、WebP conversion 與 cleanup 的 I/O / decode 工作移出 GPUI application thread。
- [ ] 新增「取消目前檔案操作」typed action，並接到可見控制與 native menu。
- [ ] 取消採 cooperative cancellation：尚未開始的項目不可產生 side effect；已完成項目仍需保留逐項結果。
- [ ] 取消、失敗、略過與成功使用不同的可見狀態，且錯誤 log 保留 source、target、reason 與必要 context。
- [ ] 同一時間只允許一個檔案操作；關閉視窗時，owned task 不可在背景繼續修改檔案。
- [ ] 加入 cancellation、partial result、window close 與 stale completion 測試。

## P2：完成大型圖庫與背景工作生命週期

### 拖放重新命名

- [ ] 拖曳接近圖庫上、下邊緣時，以有界速度自動捲動。
- [ ] Pointer up、Escape、pointer cancel、capture lost、window deactivation、source removal 與 invalid target 必須共用 session cleanup。
- [ ] 拖放完成並重新整理相同資料夾後，還原可用的圖庫捲動位置；切換資料夾時不可套用舊位置。
- [ ] 保留 drag threshold、selection order、drop-target highlight、preview confirmation 與鍵盤替代流程。
- [ ] 為 edge autoscroll、cancel、capture lost、scroll restore 與大量 virtualized rows 加入測試。

### Thumbnail 與 folder scan 取消

- [ ] 快速捲動、tile unload、縮圖尺寸變更或 generation change 時，取消或淘汰不再需要的 thumbnail work。
- [ ] 實際 decode 工作與 logical slot 都必須遵守 concurrency 上限；不可只從 pending set 移除工作後立即補排更多 task。
- [ ] Thumbnail decode 加入有界 timeout；單一 stalled decoder 不可永久佔用 slot 或阻止後續可見圖片。
- [ ] Folder navigation、recursive-mode change、reload 與 shutdown 應 cooperative-cancel 舊 scan，而不只是拒絕套用 stale result。
- [ ] 加入 deterministic timeout、rapid-scroll、generation replacement、shutdown 與 stalled-worker 測試。

### 效能驗收

- [ ] 建立 Release build 的 GPUI metrics 流程，至少記錄 startup、library ready、first thumbnail、持續捲動、search、viewer open、CPU 與 peak memory。
- [ ] 使用隔離 `PICLENS_DATA_ROOT` 與具代表性的 image library；記錄 commit、GPUI revision、OS、CPU/GPU、storage、window size 與 display scale。
- [ ] 產品決定正式效能門檻後，再加入自動 gate；在此之前不可用 Debug 或小型 smoke 宣稱效能完成。

## P2：恢復發佈與安裝能力

- [ ] 恢復 Windows x86_64 MSI 建置與 artifact 發佈。
- [ ] 恢復 Ubuntu/Debian x86_64 DEB 建置與 desktop integration。
- [ ] 恢復 Fedora/RPM x86_64 RPM 建置與 desktop integration。
- [ ] 安裝套件需包含正確的 PicLens 名稱、版本、icon、license、font notice 與 executable identity。
- [ ] 加入 install、launch、replace/upgrade、uninstall 與 profile preservation lifecycle gates。
- [ ] 在 clean Windows、Ubuntu 與 Fedora runner 驗證 package；cross-compilation 不算 runtime 驗證。
- [ ] Release workflow 只有在各平台 package 與 lifecycle gates 成功後才發布對應 assets。
- [ ] Code signing 狀態需明確揭露；未簽署 artifact 不可描述為已簽署。

## P3：恢復 CLI、診斷與小型操作 parity

- [ ] 提供 `--help`、`--version`、`-f` / `--folder`，並對未知或無效參數回傳清楚錯誤與非零 exit code。
- [ ] 恢復 `--data-root`、`--screenshot`、`--viewer`、`--metrics`、`--performance-scroll`、`--include-subfolders`、`--search`、`--list-view` 與 `--sidebar-closed`。
- [ ] CLI override 不可意外覆寫使用者的正式 startup restore authority 或其他持久設定。
- [ ] 在 UI 顯示由 Cargo workspace version 取得的 PicLens 版本，並提供 accessibility label。
- [ ] 排序控制可直接選擇四種排序，不需逐次循環；native menu 與 keyboard action 仍需同步目前狀態。

## 共用完成條件

- [ ] 行為變更有最接近責任層的純 Rust 測試。
- [ ] Pointer、keyboard、focus、scroll、dialog、accessibility 或 async UI 行為有 `#[gpui::test]`。
- [ ] Runtime UI 變更以隔離 profile 和 disposable source fixture 執行真實 app smoke。
- [ ] Windows smoke 至少檢查 1280×800 與最小 800×600，並檢查 delayed tooltip、UIA name、focus 與操作結果。
- [ ] 平台功能需在對應的 Windows / Linux 環境實際驗證；未驗證平台必須明確記錄。
- [ ] 執行 `cargo fmt --check`。
- [ ] 執行 `cargo build --workspace --all-targets --locked`。
- [ ] 執行 `cargo check --workspace --all-targets --locked`。
- [ ] 執行 `cargo test --workspace --locked`。
- [ ] 執行 `cargo clippy --workspace --all-targets --locked -- -D warnings`。
- [ ] 執行 `git diff --check`。
- [ ] 不以 compilation 或 launch-only smoke 取代互動、視覺、package lifecycle 或 hosted workflow 驗證。
- [ ] 未經明確授權，不 push、不建立 release tag、不發布 production release。

## 已完成，不列入待辦

- 普通、`Ctrl`、`Shift`、`Ctrl+Shift` 與右鍵圖片選取語意，以及 stale selection 清理。
- 可見範圍縮圖排程與 virtualized gallery。
- 深層資料夾樹展開，以及固定展開且不可收合的 picker root。
- 滑鼠側鍵資料夾歷史。
- Viewer 滾輪 pointer-anchor zoom、拖曳 pan、鍵盤導覽與動畫圖片提示。
- App 內拖放重新命名、target highlight 與確認 preview。
- 逐項 batch result panel、結果複製與 outcome-aware reveal。
- Windows ZIP 與 Linux tar.gz portable release artifacts。
