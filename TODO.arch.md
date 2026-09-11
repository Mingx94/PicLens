# Arch Linux 重寫 TODO

目標：在同一個 repo 以 C++20、Qt 6、Qt Quick／QML 完整重寫 PicLens。採 CMake／Ninja 與 Arch PKGBUILD，新版不依賴 Rust、egui 或 Windows App。

狀態：2026-09-11 建立計畫，以下全部尚未實作。程式目錄使用 `apps/linux/`，發行目標是 Arch x86_64；Ubuntu／Fedora 與 DEB／RPM 不列入本次交付。

## 執行規則

- 依 A0 → A9 順序推進；A10 須同時滿足 Windows 收尾條件。每階段前置條件是上一階段通過驗收。
- 先讀[產品規格](docs/product/product-spec.md)、[架構](docs/engineering/architecture.md)、[不變條件](docs/engineering/runtime-invariants.md)與[驗收對照](docs/product/acceptance.md)。
- 勾選代表實作與對應驗證都完成。Windows 編譯／測試不能代替 Arch 結果；容器建置不能代替 Wayland／X11 桌面驗證。
- 每次交付填寫末尾證據表。先做有界限的最小驗證，遇到問題再擴大範圍。
- 不新增 SQLite、持續監看、動畫播放、全螢幕或其他規格外功能。

## A0 — 專案、規格案例與工具鏈

- [ ] A0.1 建立 `apps/linux/` 的 CMake 專案，分開 C++ Domain／Application／Services、Qt models、QML 畫面、解碼 helper 與 tests；禁止 Rust 依賴。
- [ ] A0.2 選定 Arch 穩定套件庫的 Qt 6、小版本最低需求、C++20 工具鏈及 Ninja；建立 presets、相依清單、CMake project VERSION 與授權清單。
- [ ] A0.3 依[舊版基準](docs/reference/legacy-baseline.md)盤點產品規格與純規則，建立或重用 `test-data/`；與 Windows 共用案例 ID，明列路徑大小寫及非法名稱差異。
- [ ] A0.4 建立平台 README，記錄實測的 configure、Debug／Release build、CTest、run 命令及 Qt 模組需求，更新開發指南連結。
- [ ] A0.5 建立 app log、`--folder`／`--data-root`／`--smoke-ms` 與隔離啟動；錯誤參數有明確回饋與 exit code。
- [ ] A0.6 建立 CTest／Qt Test 與必要 Qt Quick 測試入口；在 Arch 開啟及關閉空視窗，記錄 Qt platform plugin、桌面與工作階段。

階段驗收：Qt App 可獨立建置／啟動／結束，共用案例有來源與預期結果。未具備真實 Arch 環境時保持待驗證。

## A1 — 先驗證圖片與 Qt 圖庫風險

- [ ] A1.1 使用 QImageReader 與必要圖片外掛，實測 JPG、JPEG、PNG、BMP、WebP、GIF、靜態／動畫辨識、損壞圖、透明圖片與原始尺寸。
- [ ] A1.2 確定 JPG quality 100 與無損 WebP 編碼實作，以解碼像素驗證無損；記錄外掛／codec 相依，不只依 quality 值推定。
- [ ] A1.3 以 10,000 筆 QAbstractListModel 驗證 GridView、reuseItems、cacheBuffer、視窗縮放與捲軸；記錄 delegate 數有界限。
- [ ] A1.4 驗證 QML 圖片提供介面與完整原圖／分塊的 scene graph 提交方式；確認 QImage、紋理、alpha、render thread 的所有權。
- [ ] A1.5 以刻意卡住的 helper 驗證 QProcess 取消、逾時終止、回收與暫存清理；在平台 README 記錄 codec、渲染與工作傳輸決策。

階段驗收：THUMB-01、FILE-01、GRID-01、VIEW-03 有最小技術證據；原型不代表完整功能通過。

## A2 — 設定、掃描與資料夾導覽

- [ ] A2.1 依[資料延續性](docs/engineering/data-continuity.md)實作舊 JSON、數字 enum、正規化、同目錄原子替換與損壞檔隔離；保留 XDG data 的既有位置。
- [ ] A2.2 整合原生 folder picker／桌面對話框、啟動還原與空狀態；只有 picker 更新持久化路徑及樹 root。
- [ ] A2.3 實作可取消目前／遞迴掃描、格式辨識、資料夾項目、錯誤回報及 request identity；處理深層與 symlink 迴圈，不無限掃描。
- [ ] A2.4 實作四種排序、非遞迴資料夾優先、自然排序與穩定相同值結果；不用 locale 偶然順序代替 SORT-01 契約。
- [ ] A2.5 建立 Qt 樹狀 model 的 root 固定展開、後代按需載入；支援樹／卡片、前後歷史、滑鼠側鍵與重新整理，導覽不換 root。
- [ ] A2.6 以 queued signal／UI 執行緒交付結果；快速換資料夾、排序、遞迴與重新整理清除過期選取，拒收舊結果。

階段驗收：START-01、TREE-01、NAV-01、SCAN-01、SORT-01、DATA-01 的非封裝部分通過，含取消與權限錯誤。

## A3 — 圖庫、搜尋與選取

- [ ] A3.1 整合正式 QAbstractListModel 與 GridView；10,000 筆載入／搜尋使用單次 model reset，不逐筆發出完整清單更新。
- [ ] A3.2 顯示名稱、路徑、項目數、資料夾卡片、縮圖大小與側欄設定；只提供格狀模式。
- [ ] A3.3 在 C++ 建立已載入資料的搜尋投影；名稱／完整路徑搜尋不掃描磁碟、不改樹；Ctrl+F 全選，清除保留焦點。
- [ ] A3.4 單選、Ctrl 加入／取消、Shift／Ctrl+Shift 範圍、穩定 anchor 與選取順序由應用層保存，資料夾不列入圖片範圍。
- [ ] A3.5 右鍵已選／未選圖片採正確作用範圍，資料夾不提供圖片動作；左鍵只更新選取。
- [ ] A3.6 delegate pooled／reused 時清除舊圖片、選取呈現及工作訂閱；驗證捲動、篩選與 model reset 後無殘留。

階段驗收：GRID-01、SEARCH-01、SELECT-01、MENU-01 的選單部分通過；檔案動作待 A6 完成。

## A4 — 正式縮圖、取消與快取

- [ ] A4.1 實作有界限工作／結果佇列，最多 8 個實體 decoder 子程序、15 秒初始逾時；QThreadPool 不得代替不可取消 codec 的程序隔離。
- [ ] A4.2 只排程可見／實體化靜態圖片；同一請求去重，捲出／generation 改變／關閉淘汰工作，不以 Image asynchronous 屬性代替排程。
- [ ] A4.3 依路徑、mtime、大小、解析度種類與 request ID 驗證結果；更新 model 回到所屬執行緒，遵守 Qt 渲染執行緒界線。
- [ ] A4.4 冷載入產生 PNG 與暫存 RGBA，暖載入使用 PNG；成功／失敗／取消清除暫存，格式與所有權可驗證。
- [ ] A4.5 實作不可見近期快取 32 MiB／256 筆、單一背景磁碟清理、5 秒 dirty 檢查及保留快照最新 2,000 筆。
- [ ] A4.6 驗證壞圖、逾時、快速捲動、事件佇列滿與 shutdown；回收 QProcess，不留殭屍程序，其他可見縮圖持續完成。

階段驗收：THUMB-01 與 JOB-01 縮圖部分通過；QML 不執行解碼或同步讀檔。

## A5 — 內嵌完整原圖 Viewer

- [ ] A5.1 依可見投影建立不可變序列，多選使用選取順序第一張；內嵌開啟、前後導覽、名稱、Escape 關閉與焦點返回。
- [ ] A5.2 實作 fit 為 100%、0.1～8.0 倍界限、1.2 倍步長、pointer-anchor 縮放、重設與拖曳平移；畫布按下起阻止輸入穿透。
- [ ] A5.3 1024 預覽後完整解碼原圖；預覽失敗仍嘗試原圖，原圖失敗保留預覽與錯誤；動畫只顯示提示。
- [ ] A5.4 超過單張紋理尺寸時分塊及複製邊界像素，不降低解析度；一張原圖 RGBA 最多 256 MiB、三張預覽合計 12 MiB。
- [ ] A5.5 暫停背景圖庫請求；目前原圖後依序預載下一張／上一張 1024 預覽，不預載相鄰原圖，切換沿用有效預載。
- [ ] A5.6 驗證 A-B-A、快速前後、關閉重開同圖、來源消失、超限與取消；釋放原圖、預覽與 scene graph 資源。

階段驗收：VIEW-01～VIEW-04 與 JOB-01 Viewer 部分通過；500ms 目標留待 A8 實測。

## A6 — 檔案操作與拖放重新命名

- [ ] A6.1 建立可見投影快照、計畫、確認、執行及逐項結果；執行前重查衝突，以不可覆寫操作處理 TOCTOU 競態。
- [ ] A6.2 實作 JPG quality 100、無損 WebP、略過規則、原檔保留、49／50 張確認邊界與取消零修改。
- [ ] A6.3 同 basename 清除保留 JPG／JPEG 與 WebP，只將其他格式送回收筒，預先說明作用範圍與保留規則。
- [ ] A6.4 單張改名只改 basename、同名略過、目標存在不覆寫；確認 Linux 大小寫、Unicode 及來源消失案例。
- [ ] A6.5 使用 `gio trash` 或經驗證的等效桌面回收介面；宣告依賴，helper 有逾時／取消／回收。失敗不得永久刪除。
- [ ] A6.6 Reveal 至少開啟圖片所在資料夾；外部 helper 參數安全處理空白、Unicode 與特殊字元；失敗保留 UI 狀態並記錄。
- [ ] A6.7 QML 拖放支援多張、threshold、拖曳預覽、目標提示、自動捲動、pointer cancel 與 capture-lost 清理。
- [ ] A6.8 預覽依目標 basename 跨副檔名找最小可用序號；確認前不改檔，確認後再查衝突，取消清除拖曳狀態。
- [ ] A6.9 回報逐項及總數，單項失敗繼續，紀錄 source／target／reason；拖放完成用 6／12 秒 toast 與主動詳情入口。
- [ ] A6.10 用可丟棄副本驗證權限失敗、來源消失、新增衝突、部分完成後取消與 App 關閉；核對檔案及結果，據實處理不確定狀態。

階段驗收：FILE-01～FILE-03、DRAG-01～DRAG-02、RESULT-01、MENU-01 完整操作通過，沒有永久刪除替代路徑。

## A7 — 視覺、輸入與 Arch 桌面整合

- [ ] A7.1 套用[設計系統](docs/design/system.md)的 QML theme／Controls style，建立元件展示；繁中、長檔名與空／載入／錯誤狀態完整。
- [ ] A7.2 驗證啟動 1600×1000、最小 800×600、窄工具列、側欄、捲軸不遮內容與高 DPI 圖片比例。
- [ ] A7.3 在 KDE Plasma／Wayland 驗證鍵盤、繁中輸入法、焦點、pointer grab／取消、選單、dialog、原生 picker、reveal 與回收筒。
- [ ] A7.4 另在 X11 工作階段驗證啟動、輸入、DPI 與檔案操作整合；記錄桌面、Qt plugin 與依賴版本。
- [ ] A7.5 檢查淺深色、100%／150%／200% 縮放、Qt accessibility 名稱／角色／狀態及可用的原生輔助工具。
- [ ] A7.6 完成 `--screenshot` 入口並檢查真實 renderer 畫面；desktop entry、圖示與正常關閉先做開發版驗證。

階段驗收：UI-01、OS-01 與 START-01 版面部分通過。容器或無頭 Qt 測試不算真實桌面、DPI 或 accessibility 證據。

## A8 — 效能、整合與 CI

- [ ] A8.1 實作 `--viewer`／`--metrics` 及同 Viewer 連續切換診斷；依[效能規則](docs/engineering/performance.md)定義 schema 與 Qt 完整原圖繪製提交觀測點。
- [ ] A8.2 代表性混合圖庫量測 Release 冷暖快取，核對完整原圖 500ms 目標、超標與未完成樣本，不使用圖片載入 callback 代替繪製。
- [ ] A8.3 10,000 項目量測載入、搜尋、連續捲動、CPU、RSS／峰值記憶體與 shutdown；記錄 GPU／複製開銷的量測限制。
- [ ] A8.4 逐節核對產品規格與[驗收對照](docs/product/acceptance.md)，補齊競態、取消及資料安全案例。
- [ ] A8.5 建立乾淨 Arch 環境的 CMake build、CTest／Qt 測試 CI；記錄套件版本，共用規格／fixtures 變更觸發兩版檢查。

階段驗收：PERF-01、JOB-01 及全部非封裝功能通過。未達目標或缺少實機證據需保持待辦。

## A9 — Arch 封裝與發布準備

- [ ] A9.1 建立 `packaging/arch/PKGBUILD`，固定來源 tag／commit 與 checksum，分開 depends／makedepends；納入實際 Qt 模組、codec 與 trash helper。
- [ ] A9.2 提供 desktop entry、圖示、AppStream metadata、授權與正確安裝權限；套件包含新版 Qt App 與 helper，不含 Rust 舊版。
- [ ] A9.3 在乾淨 Arch 建置環境產生 `.pkg.tar.zst`，驗證缺少開發機快取／額外插件時仍可處理全部規定格式與轉換。
- [ ] A9.4 在有授權的 Arch 環境驗證安裝、桌面啟動、升級、解除安裝與 profile 保留；以舊 JSON 副本確認資料延續性。
- [ ] A9.5 依[發布指南](docs/guides/release.md)建立 `arch/v<version>` workflow，核對 CMake version、pkgver／pkgrel、來源及 SHA-256，避開舊 `v*` 流程。
- [ ] A9.6 補齊使用說明與最終相依／授權清單；若授權公開發布，記錄 tag、推送、hosted 結果。AUR 上傳另需明確授權。

階段驗收：DATA-01 升級部分與 PACKAGE-01 通過；PKGBUILD 建立、乾淨建置、安裝驗證及公開發布各自留證據。

## A10 — 共同移除 Rust 與 egui

前置條件：A0～A9 與 [W0～W9](TODO.win.md) 均完成，兩版可獨立建置、測試與封裝。此階段不要求兩版同日公開發布。

- [ ] A10.1 和 W10 共用一次清理清單，核對 Rust crates、Cargo、toolchain、egui 腳本及舊封裝／workflow，確認沒有新版引用。
- [ ] A10.2 配合 W10 移除舊實作與工具，保留 assets、LICENSE、規格、fixtures、Git 歷史與舊版定位，不重複執行清理。
- [ ] A10.3 在不安裝 Rust 的乾淨 Arch 環境重跑新版建置、測試與 PKGBUILD；Windows 對應證據齊全後一起完成收尾。

## 完成證據

| TODO ID | commit／工作狀態 | 環境與 fixture | 命令或操作 | 結果與證據位置 |
|---|---|---|---|---|
| 尚無 | — | — | — | 尚未開始實作 |

## 待決工程項目

Qt 最低小版本、codec／WebP 編碼器、圖片提供介面與 renderer 提交方式在 A0～A1 選定。具體 Arch 套件依照實際使用版本記錄，不假設開發環境的外掛會自動出現在使用者機器。
