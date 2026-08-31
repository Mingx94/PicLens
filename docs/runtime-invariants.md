# Runtime invariants

這份文件只記錄不容易從畫面直接看出的工程不變條件。使用者可見功能、支援格式與產品範圍由 [Product specification](product-spec.md) 定義；測試方式由 [Testing](testing.md) 維護，layer ownership 由 [Architecture](architecture.md) 維護。

若產品規格、runtime invariants、實作或測試不一致，先確認預期行為，再在同一個 change 更新權威文件與測試。

## 資料與設定

- 未設定 `PICLENS_DATA_ROOT` 時，settings、logs 與 thumbnail cache 使用平台 local application data 下的 `PicLens`。
- 正式 settings 檔名是 `piclens-settings.json`；歷史 JSON 欄位與 numeric sort enums 必須持續可讀。
- Settings 寫入必須先 normalization，再以 atomic replacement 完成；corrupt JSON 必須 quarantine，而不是覆寫原檔。
- 上次透過 folder picker 選取的有效資料夾是 startup restore authority，也是 folder tree 的 root。Folder tree、folder tile 與 history navigation 不得覆寫它，也不得重設、縮減或更換 tree 的 root 與頂層項目。
- 測試、smoke、performance 與 package lifecycle 必須使用隔離 data root。

詳見 [Data continuity](data-continuity.md)。

## Library 與 selection

- Search 只投影已載入的 items，不重新掃描磁碟，也不得 reset、縮減或收合 folder tree。
- Search、sort、folder navigation、reload、recursive-mode change 與 file operations 不得留下 stale selected paths。
- Clear selection 必須同時更新 controller selection state 與 `LibraryItemModel` selected role。
- Multi-selection 保留 selection order；開啟 viewer 時，多選優先使用 selection order 中的第一張圖片。
- Range selection 的 anchor 與 selection order 分開保存；連續範圍只使用當下 visible image projection，不能包含資料夾項目。
- Viewer 和 visible-file operations 使用當下的 visible projection，不得偷偷回到未篩選 source collection。

## Thumbnail pipeline

- 只對 visible/materialized static-image tiles 排程 thumbnail work；tile unload 或 generation change 必須取消或淘汰過期結果。
- Decode 不得在 UI thread 同步執行。
- Request concurrency、timeout、logical-slot accounting 與 cache capacity 必須有界限；單一 stalled decoder 不得永久阻塞後續 visible requests。
- UI-bound model updates 必須回到 UI thread。
- Cache entries 是可重建資料；pruning 不得修改 source images。
- Cache pruning 由主程式單一 background task 負責，不得在每個 decoder worker 內掃描目錄。啟動時清理既有快取，之後每五秒只在有新寫入時清理；每輪保留該次快照中最新的 2,000 筆，兩輪之間的新寫入可暫時超出此目標。Shutdown 必須停止後續清理排程。

## Viewer

- Viewer 使用開啟當下的 immutable image-sequence snapshot；主 library 後續 reload 不得直接改變已開啟 viewer 的 navigation list。
- Viewer 開啟時取消並暫停被遮住的 gallery thumbnail requests，關閉後恢復可見縮圖排程；共用 decoder 的實體程序總數上限仍為八個。
- Viewer 只持有一個目前載入或預載工作。目前圖片的 1024-pixel preview 完成後，才依序預載 snapshot 中相鄰的下一張與上一張靜態圖片；不得掃描整個序列預載或建立無界的像素快取。
- 安全預覽 PNG 在 background executor 解碼成 GPUI BGRA pixels；UI render 不再讀取或解碼清晰預覽檔。只保留目前與相鄰兩張的像素，共最多三張、12 MiB；切換淘汰與 close 同時釋放對應 GPU atlas entries。重用前須在背景比對來源路徑、mtime、檔案大小與預覽尺寸形成的 cache key。
- 清晰預覽完成後以完整不透明度繪製，不再等待淡入。每次選取只記錄一次成功的清晰繪製，500ms 目標包含背景處理與 GPUI paint submission，但不宣稱量到 OS compositor 的實際呈現時間。
- 切換到正在預載的圖片時沿用該工作；其他切換、動畫提示、close、generation change 與 shutdown 必須取消過期工作。只有 request identity 相符的結果能清除工作或更新畫面，包含快速 A-B-A 與關閉後重開同張圖片的情況。
- Viewer canvas 從 pointer press 起攔截 zoom/pan input，避免事件穿透到底層 gallery 或啟動 drag/drop rename。
- Zoom 維持 clamp 與 pointer-anchor invariants；未 zoom in 時，左右方向鍵可導覽圖片。
- Escape 或 close action 必須關閉 viewer 並將 focus 還給 main gallery。
- Animated GIF/WebP 顯示 unsupported feedback，不嘗試播放。
- Reveal-in-file-manager 失敗時保留 viewer state，顯示狀態回饋並寫入診斷。

## File operations

- Trash-like operations 必須送到 OS recycle bin/trash。Linux 使用 `gio trash`，不可 fallback 為永久刪除。
- JPG 與 lossless WebP conversion 保留原檔；target collision 必須略過，不覆寫。
- Lossless WebP conversion 略過 JPG、JPEG、既有 WebP 與 animated images。
- Same-basename cleanup 必須保留 JPG/JPEG 與 WebP，只將其他同 basename 格式移至 trash。
- Batch operations 回報逐檔結果；單一項目失敗不阻止其他獨立項目繼續。
- Single rename 只修改 basename；same-name 是 skip，existing target 是 invalid request。
- Drop-target rename 依不含副檔名的 basename 判斷 sequence occupancy，填入最小可用序號，並在使用者確認 preview 後才執行。
- Drop-target rename 的逐項失敗必須記錄 source、target 與 reason。

## Interaction boundaries

- 圖片 tile pointer press 不得立即 capture；超過 drag threshold 後才進入 drag session，避免破壞 selection。
- Drag preview、drop-target highlight、autoscroll、pointer cancel 與 capture-lost 必須共享同一 session cleanup。
- Folder expansion、pointer、selection 與 loaded/unloaded handlers 是 view lifecycle glue，不應被搬成持久化 domain state。
- Dialog confirmation 不可使用 auto-confirm default；取消確認不得修改檔案。

## Diagnostics

- App log 位於 local app data 下的 `PicLens/Logs/PicLens.log`，測試則位於隔離 data root。
- Startup、navigation、thumbnail、folder-tree child load、file operations、drop rename 與 viewer lifecycle 都必須留下足以定位問題的 context。
- Crash 或 deployed-runtime 問題不能只以成功 build 作結論；應執行隔離的短時間 launch 並檢查 app log。
