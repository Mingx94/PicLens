# Arch TODO evidence audit

2026-09-12 23:18:00 Asia/Taipei — `gpt-5.6-sol`／effort `low`

稽核基準：commit `743199b619be46971323eb797acd89f5294e0e1c`。本次只讀原始碼、QML、測試、文件及封裝檔。未執行 build、CTest、App、封裝或桌面流程。下列「實作」只表示來源中可觀察到對應邏輯，不表示執行成功或平台驗收通過。

## 共用覆蓋標記

`[REQ✓][DISC✓][CW✓]`：已讀 `TODO.arch.md`、產品／工程／設計／測試契約；已盤點指定 Linux、封裝與 `test-data` 路徑；已逐 ID 對照來源和現有測試。三個標記都是文件與 codewalk 覆蓋，不是 runtime acceptance。

狀態定義：`有`＝主要行為在來源中；`部分`＝只有部分條款；`無`＝必要行為或資產不存在。`測試`欄只列能證明的條款。未列出的條款沒有精確自動測試證據。

## 逐 ID 證據矩陣

| ID | 行為 | 精確測試證據 | 缺少與最低工作／證據 |
|---|---|---|---|
| A0.3 | **部分**。Arch 大小寫、絕對路徑及保留名規則見 `domain.h:10`、`domain_test.cpp:98-102`。現有共用資料只有排序與設定。 | `domain_test.cpp:35-49` 讀共用 JSON；`:40` 設定；`:42-48` 排序。 | `test-data/windows-native-cases.json:1-14` 不是測試指南要求的 fixture manifest，也無六格式、hash、尺寸、選取、命名序號、舊 JSON、Unicode／路徑案例。最低：擴充同一 manifest，明列 Windows／Arch 預期，再讓 Arch 測試逐案例讀取。 |
| A0.4 | **部分**。README 有相依、configure、Debug／Release、CTest、run 與平台限制，見 `apps/linux/README.md:13-66`。 | 無；文件不證明命令已實測。 | 最低：保存本 host 的 configure／Debug／Release／CTest／Wayland smoke 命令、版本、exit code 與 log；X11、KDE 另留待驗。 |
| A0.5 | **有（來源）**。log、`--folder`、`--data-root`、`--smoke-ms`、隔離 root 與 parse error exit 2 見 `main.cpp:19-33,56`、`services.cpp:58-66,84-86`。 | 沒有 CLI／log 測試。 | 最低：程序測試合法優先序、未知／缺值／非法數字、exit 2、隔離 log 路徑；Wayland 短啟動 log 另證。 |
| A0.6 | **部分**。四個 CTest suite 與 offscreen 設定見 `CMakeLists.txt:39-50`。 | `app_test.cpp`、`controller_test.cpp`、`domain_test.cpp`、`imaging_test.cpp` 是入口；`imaging_test.cpp:233-249` 建立真實 `QQuickWindow`。 | 未有「Arch 開啟及關閉空視窗」測試／紀錄；offscreen 不等於桌面。最低：跑四 suite，再以 Wayland 空 fixture smoke 記錄 Qt plugin、桌面、session 與正常關閉。 |
| A1.1 | **部分偏有**。QImageReader、動畫辨識、alpha RGBA、原尺寸、損壞與超限路徑見 `worker_main.cpp:20-44,127-152`、`services.cpp:19-35`。 | `imaging_test.cpp:33-55` 六副檔名與尺寸；`:56-78` 動畫 GIF／超限；`:172-184` 壞圖；`:20-25,103-134` alpha 與配置。 | 沒有動畫 WebP、透明結果逐像素、實際插件清單／缺插件條件。最低：fixture manifest 加靜／動畫 WebP、透明 PNG、損壞六格式，記錄 `QImageReader::supportedImageFormats()` 與 Arch plugin。 |
| A1.3 | **部分**。GridView reuse/cache 與物化計數見 `Gallery.qml:22-56`、`controller.cpp:240-248`。 | `app_test.cpp:13` 只證明 10,000 rows 單次 reset；不證明 GridView、縮放、捲動或 delegate 上限。 | 最低：真實 QML window 以 10,000 筆捲動、縮放，斷言 `maxMaterialized` 有界並留 metrics／畫面證據。 |
| A1.4 | **有（來源）**。不可變 `FramePtr`、RGBA premultiplied、tile border、render-thread texture ownership 見 `imaging.h:12-31`、`imageitem.cpp:66-102`。 | `imaging_test.cpp:103-155` 傳輸配置、分塊與邊界像素；`:233-249` scene graph submission signal。 | 未證明真實 renderer、alpha 像素及 render-thread 資源釋放。最低：原生視窗 render/capture 比對透明與跨 tile 邊界，關閉後檢查資源／錯誤。 |
| A1.5 | **有（來源）**。QProcess 逾時、kill、reap、暫存清理見 `imaging.cpp:88-92,133-145,218-250`；README 記錄 codec／傳輸決策於 `apps/linux/README.md:72-76`。 | `imaging_test.cpp:185-231` 佇列、shutdown、stall timeout、quiesce/reap；`domain_test.cpp:149-154` trash stall/cancel。 | 缺真實 Arch child-process 殘留檢查與 renderer 決策的 runtime 證據。最低：focused tests 後核對 child PID 消失及 temp namespace 清空。 |
| A2.1 | **有（來源）**。設定正規化、數字 enum、損壞隔離、QSaveFile 無 fallback、XDG root 見 `domain.cpp:16-61`、`services.cpp:58-82`。 | `domain_test.cpp:38-58` shared／正規化／錯型／roundtrip；`app_test.cpp:11` 原子寫讀與損壞隔離。 | 缺真正舊版 JSON 副本、權限／原子失敗、XDG 升級位置整合測試。最低：加 legacy fixtures 與 data-root/XDG 程序測試。 |
| A2.2 | **部分；有實際 defect**。FolderDialog 只在 accepted 呼叫 `pick`，picker 才保存 root，見 `Main.qml:45-49`、`controller.cpp:63-65`。 | `controller_test.cpp:28-38` picker、root 不隨導覽改變與保存。 | `Controller::start()` 對已保存路徑只檢查非空，未確認存在、是資料夾且可讀（`controller.cpp:41`）；無效路徑仍成為 root/folder，而非「沒有可用資料夾」空狀態。最低：先驗證；失敗保留空 root 並回饋／log；加遺失與不可讀路徑測試。原生 dialog 仍需 Wayland/KDE 證據。 |
| A2.3 | **有（來源）**。目前／遞迴掃描、六格式、資料夾列、錯誤、cancel、generation、canonical visited 與略過 symlink 見 `services.cpp:36-50`、`controller.cpp:71-80`。 | `app_test.cpp:12` 非遞迴、資料夾、格式過濾、自然排序搜尋及預先取消。 | 未測深層、symlink loop、無權限、執行中取消、快速 request identity。最低：隔離目錄失敗注入與 controller A-B-A 測試。 |
| A2.5 | **有（來源）**。root 由獨立按鈕呈現且無收合；後代按需載入；樹、卡片、history、側鍵、refresh 不換 root，見 `Main.qml:190-255`、`controller.cpp:63-70,99-109`。 | `controller_test.cpp:28-38` 卡片導覽、history、root 保持。 | 未測 tree expand/collapse、lazy load、側鍵與 refresh。最低：QML/controller 互動測試，再以真實 pointer 驗收。 |
| A2.6 | **有（來源）**。QtConcurrent watcher 回 UI thread；generation 拒收舊 scan/tree；refresh/project 清選取，見 `controller.cpp:49-55,71-85,99-104`。 | `controller_test.cpp:35` 搜尋清過期選取；沒有競態測試。 | 最低：可控阻塞 scan/tree，快速切換 folder/sort/recursive/refresh，斷言舊結果不落地且無 stale selection。 |
| A3.1 | **有（來源）**。Rows model + GridView；project 建完整 rows 後一次 replace，見 `models.cpp:10`、`controller.cpp:82-85`、`Gallery.qml:48-56`。 | `app_test.cpp:13` 10,000 rows 恰一次 reset。 | 搜尋的 10,000 筆單次 reset 尚未由 controller 測試；最低加 signal spy 覆蓋 load 與 search。 |
| A3.2 | **有（來源）**。名稱、路徑、數量、folder card、slider、sidebar；只有 GridView，見 `Main.qml:118-123,163-425,548-550`、`Gallery.qml:85-183`。 | 無完整 UI 測試。 | 最低：原生視窗逐控制項檢查並驗證設定重啟保存。 |
| A3.3 | **有（來源）**。C++ 對已載入 `source_` 投影；Ctrl+F 全選、清除不重掃，見 `domain.cpp:128-145`、`controller.cpp:58,82-85`、`Main.qml:77-83,286-304`。 | `domain_test.cpp:64` 名稱搜尋；`app_test.cpp:12`；`controller_test.cpp:35` root 保持。 | 沒有完整路徑查詢、清除後焦點與 tree 狀態測試。最低：純規則完整路徑 + QML focus/tree 測試。 |
| A3.5 | **有（來源）**。右鍵 selection scope 與資料夾 action gating 見 `controller.cpp:88-90`、`Main.qml:566-585`；左鍵只導覽／選取。 | `domain_test.cpp:80-85` 已選保留、未選改為單一；`controller_test.cpp:17-25,32-35` 選取。 | 缺真實 context menu 與 folder 不觸發 rename/trash 測試。 |
| A3.6 | **部分偏有**。pooled/reused source binding 清空；可見請求取消；model reset 清 provider 與 selection，見 `Gallery.qml:100-145`、`controller.cpp:110-129`。 | 無 delegate reuse 殘留測試。 | 最低：捲動、filter、reset 後截圖／model assertion，驗證圖片、selected、error、訂閱皆無殘留。 |
| A4.2 | **有（來源）**。120 ms 採集實體可見 delegate；靜態圖片才排程；identity 去重；捲出、generation、viewer、shutdown 取消，見 `Gallery.qml:22-47,296-300`、`controller.cpp:110-129`、`imaging.cpp:185-223`。 | `controller_test.cpp:28-34` 可見縮圖完成；`imaging_test.cpp:80-101` 同 identity 快取；`:172-231` cancel/shutdown。 | 未測實際快速捲出與相同 request 合併。最低：阻塞 helper + visible-set churn 測試。 |
| A4.3 | **有（來源）**。identity 含絕對路徑、mtime、size、edge；token/serial；queued GUI delivery，見 `imaging.cpp:23-39,225-264`。 | `imaging_test.cpp:80-101` cold/warm/token/source change；`:233-249` render submission。 | 未測同 mtime/size 但內容改變（契約本就以這些欄位）；未有 thread-affinity assertion。最低：spy callback thread 與 stale-token controller 測試。 |
| A4.4 | **有（來源）**。cold 產生專用 RGBA transport + PNG，warm 讀 PNG；輸出 temp 由 finish/stop 清，見 `worker_main.cpp:137-152`、`imaging.cpp:88,202-234`。 | `imaging_test.cpp:80-101` cold/warm PNG；`:136-155` 原圖不寫 cache；`:172-231` failure/cancel/shutdown terminal。 | 沒有逐案檢查 RGBA temp 在成功／失敗／取消後皆消失。最低：暴露隔離 temp root 或由 child marker 驗證清理。 |
| A4.5 | **有（來源）**。32 MiB／256 LRU、單一 backend thread、5 秒 dirty prune、最新 2,000 PNG 見 `imaging.cpp:20,81-100,121,171-176,235-244`。 | 無上限／prune 測試。 | 最低：注入超過 256/32 MiB 與 2,001 cache，驅動 5 秒 dirty tick，斷言 LRU、檔數與只清 owned non-symlink。 |
| A4.6 | **部分偏有**。失敗、timeout、bounded queue、shutdown/reap 均有程式。 | `imaging_test.cpp:172-231` 壞圖、取消、滿佇列、shutdown、stall、resume。 | 未證明「一個壞／逾時工作時其他可見縮圖持續完成」、快速捲動及 OS zombie 查核。最低：混合成功＋stall 並行測試，加 PID reap runtime 檢查。 |
| A5.1 | **有（來源）**。projection snapshot 排除 folder；選取第一張；內嵌導航、名稱、Escape、focusGallery 見 `controller.cpp:138-147`、`Main.qml:27,429-525`。 | `controller_test.cpp:32-35` 選取順序第一張、next、name、close；未驗焦點。 | 最低：測 snapshot 在背景 projection 改變仍固定，以及 Escape 後 focus。 |
| A5.2 | **有（來源）**。fit 基準 1、0.1–8、1.2、pointer anchor、reset、pan、press accept 見 `imageitem.cpp:30-64,83-87`。 | `imaging_test.cpp:233-249` 只證界限、reset 與 frame signal。 | 缺 pointer-anchor 座標、drag pan、未放大方向鍵、輸入不穿透測試；最低補 QQuick pointer/keyboard 測試及真實視窗流程。 |
| A5.3 | **有（來源）**。先 1024，再 full；preview failure 仍 full；full failure 不清現有 preview；動畫提示，見 `controller.cpp:148-175`。 | `imaging_test.cpp:56-78,103-155` 解碼／動畫／preview；沒有 controller 序列測試。 | 最低：可控 imaging 測 preview→full、preview fail→full、full fail 保留 preview、animated 不排 full。 |
| A5.4 | **有（來源）**。2046 interior + border，不降原圖；256 MiB；三 preview 實配 12 MiB，見 `worker_main.cpp:16-75`。 | `imaging_test.cpp:56-78` 256 MiB reject；`:103-155` 12 MiB、tiles、border、原尺寸。 | 尚需真實 GPU 最大 texture／跨 tile renderer 畫面驗證；來源單元證據本身充足但不是平台畫面。 |
| A5.5 | **有（來源）**。開 viewer cancel gallery；full 完成後 next/previous 各一次 priority preview；不 preload full；有效 preview 沿用，見 `controller.cpp:138-175`。 | 無。 | 最低：fake/blocking backend 記錄精確排程順序與 cache reuse；同時驗證 gallery 在 viewer 期間無新請求。 |
| A5.6 | **部分偏有**。session/token/current checks 與 close resource clear 可見於 `controller.cpp:143-175`。 | `imaging_test.cpp:172-231` service cancel/shutdown；不是 Viewer A-B-A。 | 最低：A-B-A、快速 step、close/reopen 同圖、來源消失、超限、pending cancel；對 frame/previews/request/process/scene graph 各做釋放 assertion。 |
| A6.1 | **有（來源）**。projection snapshot→plan→confirm→execute→per-item；stamp/stem 重查與 renameat2 no-replace，見 `controller.cpp:186-239`、`fileoperations.cpp:89-127`。 | `domain_test.cpp:113-148,165-168` preview race、atomic collision、changed source、逐項結果。 | 缺 controller request identity（準備 plan 期間狀態變更）測試。最低：阻塞 plan，嘗試 navigation/filter，斷言 scope 固定。 |
| A6.2 | **有（來源）**。JPG quality 100、lossless WebP、skip、保留 source、50 threshold、cancel no commit，見 `worker_main.cpp:78-123`、`domain.cpp:198-215`。 | `domain_test.cpp:86,93-94,118-120,137-164`；`imaging_test.cpp:156-171` lossless WebP／不覆寫／保留；`controller_test.cpp:39-44` 50 張取消零修改。 | 缺 49 張「不顯示確認且執行」、動畫 WebP skip、JPG quality metadata 測試。 |
| A6.3 | **有（來源）**。cleanup 只移除同 basename 的其他格式，保留 JPG/JPEG/WebP，見 `domain.cpp:216-230`。 | `domain_test.cpp:95-97` protected formats 與傳入可見範圍。 | 缺 controller 確認文字到實際 gio 成功整合。最低：可丟棄 fixture + fake/real trash 分層測試。 |
| A6.4 | **有（來源）**。basename only、same-name skip、collision no overwrite、Linux case-sensitive／Unicode 由 QString/fs，見 `domain.cpp:172-197`。 | `domain_test.cpp:98-104,114-124,139-140` 非法名、Arch `CON`、同名、衝突、source changed、Linux rename。 | 缺大小寫 only rename 與 Unicode 實際 mutation。最低：Arch fixture 加 `a`→`A`、組合字與來源消失。 |
| A6.5 | **有（來源）**。`gio trash`、15 s bounded、cancel kill/reap、無永久刪除 fallback，見 `fileoperations.h:17-20`、`fileoperations.cpp:50-87`；依賴見 `PKGBUILD:8`、README。 | `domain_test.cpp:120-121,149-154` 預取消、helper failure、stall timeout、active cancel。 | 缺真實 `gio trash` 成功與桌面回收筒內容／reap 證據。最低：獨立可丟棄 fixture 在授權 Arch profile 驗收。 |
| A6.6 | **有（來源）**。只把含空白／Unicode／特殊字元的路徑組成 `QUrl`，至少開所在 folder；失敗保留狀態並 log，見 `controller.cpp:178-179`。 | 無。 | 最低：mock URL opener 單元測參數與 failure，再在 Wayland/KDE 驗收檔案管理員。 |
| A6.7 | **部分偏有**。多選來源由 controller selection；threshold、ghost、highlight、autoscroll、cancel/pooled cleanup 見 `Gallery.qml:185-320`。 | 無。 | `MouseArea.onCanceled` 可涵蓋 grab loss，但沒有可觀察證據。最低：QML pointer 測試 threshold 前不 drag、multi scope、target、autoscroll、cancel與 ungrab/pooled 一律清 session。 |
| A6.8 | **有（來源）**。跨副檔名 occupancy、最小序號、confirm 前只 plan、執行重查 stem、取消清狀態見 `domain.cpp:238-266`、`controller.cpp:185-210`。 | `domain_test.cpp:105-113` 最小 `-02`、相對 entryList、preview 後衝突；`controller_test.cpp:39-44` 一般 confirm cancel 零修改。 | 缺 drop 專屬確認取消與 drag state assertion。 |
| A6.9 | **有（來源）**。逐項與 summary、失敗繼續、source/target/reason log、6/12 秒 toast+詳情見 `fileoperations.cpp:133-150`、`controller.cpp:219-238`、`Main.qml:658-744`。 | `domain_test.cpp:141-168` partial cancel、continue after failure、counts；無 UI toast timing 測試。 | 最低：controller/QML 測 summary、12 秒含 unknown/failed、6 秒 success、詳情資料與手動關閉。 |
| A6.10 | **部分**。權限/來源變更/新衝突/cancel/不確定的防線存在。 | `domain_test.cpp:113-168` 新衝突、來源變更、部分完成取消、trash timeout/cancel unknown、continue。 | 未測真實權限失敗、來源「消失」、App 關閉中的 file batch 與磁碟核對。最低：Linux 可丟棄副本整合案例；關閉時確認 helper reap 與已完成／未開始／unknown 報告。 |
| A7.1 | **有（來源）**。Basic style、Theme、元件展示、繁中、elide/wrap 與空/載入/錯誤見 `main.cpp:18-20`、`Theme.qml`、`ComponentPanel.qml`、`Main.qml`。 | 無像素／互動證據。 | 最低：`--components --dark --width 800 --height 600` 原生截圖，另測長檔名、Unicode、disabled、error。 |
| A7.2 | **有（來源尺寸）**。預設 1600×1000、最小 800×600、adaptive layout、scrollbar margin 見 `main.cpp:23-25`、`Main.qml:22-25`、`Gallery.qml:48-63`。 | 無。 | 最低：Wayland 1600×1000、800×600、窄 toolbar/sidebar、scrollbar、高 DPI 100/150/200% 原生截圖。無法由來源證明比例。 |
| A7.3 | **未驗收**。對應控制項存在。 | 無 KDE Plasma／Wayland runtime 證據。 | 本 host 是 Hyprland Wayland，不可接受 KDE。最低：KDE Plasma/Wayland 實測 keyboard、繁中 IME、focus、pointer cancel、menu/dialog、picker、reveal、trash，記錄版本與 log。 |
| A7.4 | **未驗收**。 | 無 native X11 證據。 | 已知環境無 native X11；XWayland 不算。最低：native X11 session 驗收啟動、輸入、DPI、rename/trash/reveal，記錄 Qt `xcb` 與依賴。 |
| A7.5 | **部分（來源）**。theme 切換與部分 Accessible name/role 已加，見 `Main.qml:124-129,297-333`、`Gallery.qml:273-277`、`ActionButton.qml:26`。 | 無。 | 未盤點所有控制項的 name/role/state，也無原生輔助工具、淺深色/DPI 證據。最低：accessibility tree 稽核 + 100/150/200% 原生截圖。 |
| A7.6 | **有（來源）**。`--screenshot` grab/save/error 3、desktop entry、icon install、aboutToQuit shutdown 見 `main.cpp:17-20,46-55`、`CMakeLists.txt:30-37`。 | 無 renderer／desktop lifecycle runtime 證據。 | 最低：Wayland 真實 renderer 截圖，從 desktop entry 啟動、圖示顯示、正常關閉與 log。 |
| A8.1 | **有（來源）**。`--viewer`、`--metrics`、exercise 連續切換、schemaVersion 1 與 `afterRendering` submission 見 `main.cpp:19,44-53`、`controller.cpp:134,240-254`。 | `imaging_test.cpp:233-249` 只證 original submission signal 一次。 | 最低：定義/文件化每個 metrics 欄位與樣本語意；以同 Viewer 連切輸出 JSON，核對 unpainted；明列非 compositor 呈現。 |
| A8.2 | **未驗收**。量測入口存在。 | 無 Release 冷／暖代表圖庫數據。 | 最低：Release 混合 fixture 分開 cold/warm，逐 selection 以 `fullPaintSamples`/`unpaintedSelections` 計 500 ms、超標與未完成；不得把 decode callback 當 paint。 |
| A8.3 | **未驗收**。10,000 diagnostic items 與 model/materialized metrics 存在。 | `app_test.cpp:13` 只證 model reset。 | metrics 無 CPU、RSS/peak、shutdown 欄位（`controller.cpp:240-248`）。最低：以外部量測工具記 load/search/scroll/CPU/RSS/peak/shutdown，並說明 GPU/複製限制；必要時才新增 schema 欄位。 |
| A8.4 | **部分**。現有測試涵蓋若干競態／取消／資料安全，但沒有逐節 checklist。 | 證據散見 A2–A6 各列。 | 最低：依 `product-spec.md` 全文與 `acceptance.md:9-36` 建 clause matrix，補缺少競態、取消、資料安全案例；未知功能不得擴 scope。 |
| A9.3 | **未驗收**。PKGBUILD／release scripts 存在，依賴宣告含 imageformats/libwebp，見 `packaging/arch/PKGBUILD:7-33`。 | 無乾淨 Arch `.pkg.tar.zst` 與 codec/轉換結果。 | 最低：clean Arch build，移除開發 cache/額外 plugin 假設，以 manifest 六格式和 JPG/WebP conversion 實測，保存 package/hash/log。 |
| A9.4 | **未驗收**。安裝表與 profile 路徑邏輯存在。 | 無授權環境 lifecycle 證據。 | 最低：可授權 Arch VM/host 做 install、desktop launch、upgrade、uninstall、profile retained；舊 JSON 副本驗證遷移。 |
| A9.6 | **部分偏有**。使用／相依／授權與 tag/hosted 流程文件存在，見 `apps/linux/README.md:13-78`、`packaging/arch/README.md:1-75`、`CMakeLists.txt:36-37`。 | 無 final license inventory 或 hosted result。 | 最低：核對 package file/license/dependency 清單；只有得到公開發布授權後才建立 tag/push並保存 hosted 結果。AUR 仍需另行明確授權。 |
| A10.3 | **未驗收**。`validate.sh` 與 PKGBUILD 提供候選流程。 | 無只含必要工具鏈的 clean OS build/test/package 證據。 | 最低：乾淨 Arch OS 安裝列出的必要套件，跑 configure/build/四 CTest/package，保存環境版本、完整 log、產物 hash。開發 host 不可代替。 |

## 已確認 defect 與判定邊界

唯一由靜態呼叫路徑可直接重現的產品 defect 是 A2.2：profile 的 `lastFolderPath` 若已刪除或變成非資料夾，`Controller::start()` 仍設定 root 並導覽（`apps/linux/src/controller.cpp:41`）；接著 scanner 回「無法讀取資料夾」，而不是產品規格要求的無有效資料夾入口狀態。重現最小條件：profile JSON 寫入不存在的絕對路徑後啟動。這是來源事實加上確定的控制流推論；本次未執行程式。

其餘列為「缺證據」者不等於 defect。KDE、native X11、clean OS、真實回收筒、檔案管理員、IME、DPI、accessibility、GPU／compositor 與 package lifecycle 均未在本次環境觀察，不能聲稱接受或失敗。已知 host 的 Omarchy 4.0.3、Qt 6.11.2、Hyprland Wayland 也不能替代 KDE 或 native X11。

## 公開邊界、慣例與可能修改點

- CLI/public process boundary：`apps/linux/src/main.cpp:15-56`。exit code 2 是參數／啟動契約；3 是 screenshot/metrics 輸出失敗。
- QML/controller boundary：`apps/linux/src/controller.h:22-109` 的 properties、signals、`Q_INVOKABLE`；正式畫面是 `apps/linux/qml/Main.qml` 與 `Gallery.qml`。
- Model/domain boundary：`Rows::replace()` 是一次 reset；純規則在 `domain.h/.cpp`；scan/profile/log 在 `services.h/.cpp`。
- Helper/transport boundary：`Imaging` 接受 unique token；worker CLI 與 RGBA tile 格式在 `imaging.h/.cpp`、`worker_main.cpp`。GUI thread 更新，decode/file I/O 不應搬回 UI thread。
- 檔案安全 boundary：plan snapshot 與 `FileStamp`、`renameat2(RENAME_NOREPLACE)`、`gio trash`、逐項 `FileResult` 在 `domain.*`、`fileoperations.*`。不可新增覆寫或永久刪除 fallback。
- 慣例：絕對路徑、Linux 大小寫；mtime 為 UTC Unix ms；圖片限六副檔名；動畫只辨識不播放；格狀模式；QML lifecycle state 不持久化。
- 最可能修改位置：A2.2 修正 `controller.cpp:41` 並擴 `controller_test.cpp`；共用資料補 `test-data/` 與 `domain_test.cpp`；競態/快取/helper 補 `imaging_test.cpp`、`domain_test.cpp`；QML 互動補新的 Qt Quick 測試 target 與 `CMakeLists.txt`；平台驗收主要應新增外部 artifacts，不應為了製造通過標記改產品來源。

## 來源變更是否 consequential

本次沒有修改 source/config/test，只新增本報告。若修 A2.2，會改變啟動時可觀察狀態與 profile 恢復行為，屬 consequential source change，應先有失敗測試，再做最小修正。A8.3 若要把 CPU/RSS 納入 app schema，也會改公開診斷輸出，屬 consequential；較小替代是先用外部量測，不改 schema。純補測試資料、測試或外部驗收 artifacts 不改 runtime 行為，但仍須審查資料範圍與 fixture 安全。

## 建議 focused commands（本次未執行）

```bash
cmake -S apps/linux -B build/arch-debug -G Ninja -DCMAKE_BUILD_TYPE=Debug -DBUILD_TESTING=ON
cmake --build build/arch-debug
QT_QPA_PLATFORM=offscreen ctest --test-dir build/arch-debug --output-on-failure --no-tests=error --timeout 120
bash packaging/arch/validate.sh inspect
bash packaging/arch/validate.sh build
QT_QPA_PLATFORM=wayland bash packaging/arch/validate.sh smoke /tmp/<validated-run>/stage/usr/bin/piclens
```

先用 `ctest -R '^(domain|imaging|app|controller)$'` 聚焦四 suite。修 A2.2 時，先只跑新增的 controller case，再跑 controller suite 與四 suite。桌面、效能和封裝命令要在對應授權環境執行；不要在目前 session 宣稱 KDE、native X11 或 clean OS 結果。

## 未檢查區域

依 brief 未檢查指定範圍外的 Windows 實作、`.github/workflows` 實際內容、`docs/linux/*`、assets 全量內容、LICENSE/THIRD-PARTY 全文、Git history/diff、產物與 artifacts。也未檢查安裝後 ELF linkage、Qt plugin discovery、WebP/JPEG 實際 encoder metadata、GPU texture limit、process table、回收筒內容、桌面 accessibility tree。未使用網路、Computer Use、build 或 test。

Self-check: Immutable bounded audit brief; no implementation authorized.
