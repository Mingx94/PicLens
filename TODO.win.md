# Windows 重寫 TODO

目標：在同一個 repo 以 C#、.NET、WPF／XAML 完整重寫 PicLens。新版不依賴 Rust、egui、Qt、WinUI 或共用跨語言核心。

狀態：2026-09-12 Windows WPF 來源版本已升至 4.0.2，包含 Lucide 圖示與元件樣式調整；4.0.2 尚未重新封裝。既有候選封裝為 4.0.1。Release 45 項測試通過。未勾選項目包含仍待人工／OS／安裝環境驗證的部分，不代表全部尚未寫程式。完整差異與證據見 [Windows 驗證紀錄](docs/engineering/windows-validation.md)。現有 Rust 程式依共同退場條件暫留。

## 執行規則

- 依 W0 → W9 順序推進；W10 須同時滿足 Arch 收尾條件。每階段前置條件是上一階段通過驗收。
- 先讀[產品規格](docs/product/product-spec.md)、[架構](docs/engineering/architecture.md)、[不變條件](docs/engineering/runtime-invariants.md)與[驗收對照](docs/product/acceptance.md)。
- 勾選代表該項實作與對應驗證都完成。只有計畫、可編譯或畫面骨架不能勾選功能完成。
- 每次交付填寫末尾證據表；無法驗證的項目保持未勾選。工程選型在階段內完成，不把未定套件留到功能交付後。
- 產品規格保留原有功能；不額外加入 SQLite、持續監看、動畫播放或全螢幕。

## W0 — 專案、規格案例與工具鏈

- [x] W0.1 建立 `apps/windows/` solution，分開 WPF App、產品規則／應用服務、平台服務、解碼 helper 與 tests；禁止依賴 Rust。
- [x] W0.2 選定可維護的 .NET SDK、最低 Windows 版本和 x64 目標，鎖定 SDK／NuGet 依賴；建立 `Directory.Build.props` 版本來源，記錄第三方授權。
- [x] W0.3 依[舊版基準](docs/reference/legacy-baseline.md)盤點產品規格每節與既有純規則；建立或重用 `test-data/`，納入排序、命名、設定、格式及 OS 差異案例。
- [x] W0.4 建立平台 README，寫入實測可執行的 restore、Debug／Release build、test、run 命令及 prerequisites，更新開發指南連結。
- [x] W0.5 建立基本 app log、`--folder`／`--data-root`／`--smoke-ms` 解析與隔離啟動；無效參數需有錯誤與 exit code。
- [x] W0.6 建立純規則測試與必要 STA／Dispatcher 測試入口；用乾淨隔離資料開啟及關閉空 WPF 視窗。

階段驗收：獨立 WPF 專案可建置／啟動／結束，命令與相依已記錄；共用案例有來源與預期結果。這不代表圖片功能完成。

## W1 — 先驗證圖片與格狀虛擬化風險

- [ ] W1.1 驗證 WIC／WPF 與候選 codec 對 JPG、JPEG、PNG、BMP、WebP、GIF 的能力；確認靜態／動畫辨識、損壞圖、透明圖片與原始尺寸。
- [x] W1.2 選定 JPG quality 100 與無損 WebP 編碼方式，用解碼後像素檢查無損輸出；封裝依賴不得依靠開發機額外安裝的 codec。
- [x] W1.3 以 10,000 筆合成資料驗證換行格狀 VirtualizingPanel、容器回收、視窗縮放與捲軸；記錄實體容器數不隨總筆數無限增加。一般 WrapPanel 不算完成。
- [x] W1.4 驗證完整原圖與超過單張貼圖尺寸圖片的分塊繪製方案；確認 WPF 圖片資源、alpha 與背景至 UI 的所有權。
- [x] W1.5 用刻意卡住的測試 helper 驗證可終止、回收、清理暫存與重開；將 codec、panel 與 helper 決策寫入平台 README。

階段驗收：THUMB-01、FILE-01、GRID-01、VIEW-03 的技術風險有最小證據；原型不算後續完整功能通過。

## W2 — 設定、掃描與資料夾導覽

- [x] W2.1 依[資料延續性](docs/engineering/data-continuity.md)實作舊 JSON、數字 enum、正規化、原子寫入與損壞檔隔離；無權限時不可覆寫舊檔。
- [ ] W2.2 建立原生 folder picker、啟動還原與空狀態；只有 picker 更新持久化路徑及樹 root。
- [x] W2.3 實作可取消的目前／遞迴掃描、圖片辨識、資料夾項目、錯誤回報、generation／request ID；確認深層與連結不造成無限掃描。
- [x] W2.4 實作四種排序、非遞迴資料夾優先、自然排序與穩定相同值結果，重用 SORT-01 fixtures。
- [ ] W2.5 實作 root 固定展開不可收合、後代按需載入、資料夾卡片／樹導覽、歷史前後、滑鼠側鍵、重新整理；導覽不得更換 root。
- [x] W2.6 確認切換資料夾、排序、遞迴及重新整理清除過期選取；過期工作結果不更新新圖庫。

階段驗收：START-01、TREE-01、NAV-01、SCAN-01、SORT-01、DATA-01 的非封裝部分，以隔離資料及失敗案例通過。

## W3 — 圖庫、搜尋與選取

- [x] W3.1 整合 W1 panel 與正式 ViewModel；10,000 筆載入／搜尋以單次清單重設交付；UI 容器只管理可見／實體化範圍。
- [x] W3.2 加入名稱、路徑、項目數、資料夾卡片、縮圖大小與側欄設定；只提供格狀模式。
- [x] W3.3 搜尋只篩選已載入投影；清除保持輸入焦點，Ctrl+F 聚焦並全選；搜尋不掃描磁碟或改變樹。
- [x] W3.4 實作單選、Ctrl 取消／加入、Shift／Ctrl+Shift 範圍；anchor 與選取順序分開，資料夾不加入圖片範圍。
- [x] W3.5 實作右鍵已選／未選圖片的選取語意與情境選單；左鍵不開操作浮層，資料夾不顯示圖片操作。
- [ ] W3.6 驗證搜尋、容器回收、縮圖尺寸變更、排序與資料夾切換後的選取／焦點一致性。

階段驗收：GRID-01、SEARCH-01、SELECT-01、MENU-01 的選單與選取部分通過；檔案動作保持未完成直到 W6。

## W4 — 正式縮圖、取消與快取

- [x] W4.1 整合解碼 helper、有界限請求／事件佇列、最多 8 個實體解碼程序及 15 秒初始逾時；取消後確實終止並回收。
- [x] W4.2 只對可見／實體化靜態圖片排程，去除重複工作；滾出視窗、generation 改變與關閉時取消／淘汰。
- [x] W4.3 實作來源路徑、mtime、大小、解析度種類與 request ID 檢查；結果經 Dispatcher 更新，舊結果不能清除新工作。
- [x] W4.4 冷載入建立 PNG 快取與暫存 RGBA，暖載入使用 PNG；所有成功／失敗／取消路徑清理暫存。
- [x] W4.5 實作不可見近期快取 32 MiB／256 筆，以及單一背景清理者、5 秒 dirty 檢查／快照最新 2,000 筆規則。
- [x] W4.6 驗證壞圖、超時、連續快速捲動、事件佇列滿與關閉；失敗圖片仍可選取，後續縮圖繼續載入並有紀錄。

階段驗收：THUMB-01 與 JOB-01 縮圖部分通過；背景工作不阻塞 UI，沒有殘留 helper 或暫存。

## W5 — 內嵌完整原圖 Viewer

- [x] W5.1 依可見投影建立不可變序列；多選時使用選取順序的第一張；內嵌開啟、上一張／下一張、名稱與 Escape 返回焦點。
- [x] W5.2 實作 fit 為 100%、0.1～8.0 倍界限、1.2 倍步長、pointer-anchor 滾輪縮放、按鈕縮放／重設與拖曳平移；輸入不穿透圖庫。
- [x] W5.3 依序載入 1024 預覽與完整原圖；預覽失敗仍嘗試原圖，原圖失敗保留預覽並提示，不以縮小圖冒充完成。
- [x] W5.4 背景準備完整原圖／分塊與邊界像素，UI 提交繪製；只保留一張原圖且 RGBA 最多 256 MiB、三張預覽合計 12 MiB。
- [x] W5.5 暫停被遮住的圖庫工作；目前原圖完成後依序預載下一張／上一張的 1024 預覽；不預載相鄰原圖，切換時沿用有效預載。
- [ ] W5.6 驗證 A-B-A、快速前後切換、關閉再開同圖、超限、動畫提示、檔案消失及關閉釋放；完成 JOB-01 Viewer 部分。

階段驗收：VIEW-01～VIEW-04 全部功能案例通過；500ms 目標留待 W8 實測，不用預覽時間代替。

## W6 — 檔案操作與拖放重新命名

- [x] W6.1 建立「可見投影快照 → 計畫 → 確認 → 執行 → 逐項結果」流程；實際修改前重查衝突並以不可覆寫操作防止競態。
- [x] W6.2 實作 JPG quality 100 與無損 WebP 轉換、略過規則、原檔保留、49／50 張確認邊界及取消不改檔。
- [x] W6.3 實作同 basename 格式清除，保留 JPG／JPEG 與 WebP，只回收其他格式；確認文案清楚說明規則。
- [x] W6.4 實作單張 basename 重新命名、同名略過、衝突不覆寫；Windows 非法檔名與保留名稱採既有路徑規則案例。
- [ ] W6.5 使用 Windows 回收筒、數量確認與檔案總管選取檔案；系統失敗需回報，不可改用永久刪除。
- [ ] W6.6 拖放支援多張、threshold、預覽、可放下目標、自動捲動、pointer cancel／capture-lost 的一致清理。
- [x] W6.7 預覽依目標 basename 找最小可用序號，跨副檔名檢查占用；確認前零修改，取消不改檔。
- [x] W6.8 逐項回報成功／略過／取消／失敗與總數；單項失敗繼續，紀錄來源／目標／原因。拖放結果用 6／12 秒 toast，使用者可開啟詳情。
- [ ] W6.9 以可丟棄副本驗證權限錯誤、來源消失、執行前新增衝突、部分完成後取消與關閉；核對檔案 hash 及實際結果。

階段驗收：FILE-01～FILE-03、DRAG-01～DRAG-02、RESULT-01、MENU-01 完整操作通過。取消不代表回滾已完成檔案，不確定結果需據實呈現。

## W7 — 視覺、輸入與 Windows 整合

- [x] W7.1 以 ResourceDictionary 套用[設計系統](docs/design/system.md)，建立元件展示入口；繁中、長檔名、空／載入／錯誤狀態完整。
- [x] W7.2 驗證啟動 1600×1000、最小 800×600、側欄與窄工具列，縮圖捲軸不遮住內容。
- [ ] W7.3 驗證鍵盤操作、IME、焦點、dialog／選單關閉返回、圖示提示與 UI Automation 名稱／角色／狀態。
- [ ] W7.4 檢查淺深色、Windows 高對比、100%／150%／200% DPI 的實際畫面與原圖比例；記錄未驗證環境。
- [ ] W7.5 驗證原生 picker、reveal 失敗仍保留狀態、工作列／exe 圖示與正常關閉；完成 `--screenshot` 診斷入口。

階段驗收：UI-01、OS-01 與 START-01 版面部分通過。原生像素與輔助工具證據分開記錄。

## W8 — 效能、整合與 CI

- [x] W8.1 實作 `--viewer`／`--metrics` 與同 Viewer 連續切換診斷；依[效能規則](docs/engineering/performance.md)定義新版 schema 與真實繪製提交量測點。
- [x] W8.2 使用代表性混合圖庫量測 Release 冷暖快取；完整原圖 500ms 目標、超標與未完成樣本全部回報。
- [x] W8.3 以 10,000 項目驗證載入、搜尋、連續捲動、CPU、峰值記憶體、取消與關閉；不得只以小圖副本推定解碼效能。
- [ ] W8.4 核對[驗收對照](docs/product/acceptance.md)及產品規格全文，補齊未完成案例與有意義的失敗路徑。
- [x] W8.5 依使用者決定改為 tag-only 發布：版本核對 → 建置封裝 → 發布。移除 PR／main 自動檢查、測試與安裝驗證；此勾選表示流程設定完成，不代表 hosted 或功能驗收通過。

階段驗收：PERF-01、JOB-01 及所有非封裝功能通過；效能或平台未驗證不能標為完成。

## W9 — 封裝、升級與發布準備

- [x] W9.1 在 `packaging/windows/` 建立 WPF MSI／portable ZIP，選定 runtime 部署模式，包含 helper、codec、圖示、字型與必要聲明。
- [ ] W9.2 保留可升級的產品識別與版本排序；使用舊 profile 副本驗證設定延續，不能以新框架為由清空設定。
- [ ] W9.3 在有授權的乾淨 Windows 環境驗證安裝、啟動、舊版升級、解除安裝、profile 保留及 ZIP 解壓啟動。
- [x] W9.4 依[發布指南](docs/guides/release.md)建立 `windows/v<version>` 的獨立 workflow，避免舊 Rust workflow 誤觸發；核對來源、版本、產物及 SHA-256。
- [x] W9.5 完成最終相依／授權清單與使用說明，記錄簽章狀態；若本次授權發布，另留下 annotated tag、推送與 hosted 成功證據。

階段驗收：DATA-01 升級部分與 PACKAGE-01 通過；候選套件完成不等於已公開發布。

## W10 — 共同移除 Rust 與 egui

前置條件：W0～W9 與 [A0～A9](TODO.arch.md) 均完成，兩版可獨立建置、測試與封裝。此階段不要求兩版同日公開發布。

- [ ] W10.1 和 A10 共用一次清理清單，辨識 Rust crates、Cargo、toolchain、egui 腳本及舊封裝／workflow；確認沒有新版引用。
- [ ] W10.2 移除上述舊實作與工具，保留 assets、LICENSE、共用規格、fixtures、Git 歷史與舊版定位；更新 README 的實際狀態。
- [ ] W10.3 在不安裝 Rust 的 Windows 環境重跑新版建置、測試及封裝；確認 Arch 的對應證據也存在，再勾選兩份共同收尾。

## 完成證據

| TODO ID | commit／工作狀態 | 環境與 fixture | 命令或操作 | 結果與證據位置 |
|---|---|---|---|---|
| W0～W6 已勾項目 | 工作目錄實作 | Windows 11／自有 fixtures | Release tests | 45 通過；來源見 apps/windows/tests/PicLens.Tests |
| W1.3、W3.1、W8.3 | 已驗證 | 10,000 份圖示副本 | --exercise --metrics | 最多 28 個容器；載入約 234ms |
| W5、W8.1～W8.2 | 已驗證 | 207 張混合圖片，100% DPI | 冷暖 --exercise | 各 25 次完整繪製；最慢 280／256ms；原檔 hash 不變 |
| W7.1～W7.2 | 已檢查 | 1600×1000／800×600 | --screenshot、--dark | 見驗證紀錄，DPI／UIA 人工部分未完成 |
| W9.1、W9.4～W9.5 | 候選封裝 | self-contained x64 | packaging/windows/build.ps1 | MSI ICE 0 警告／0 錯誤；ZIP、授權、SHA-256 |
| W1.1、W2.2、W2.5、W3.6、W5.6、W6.5～W6.6、W6.9、W7.3～W7.5、W8.4 | 已實作，驗證未齊 | 需對應實檔、OS 與互動環境 | 見驗收對照 | 保持未勾選，未以程式碼檢查代替人工證據 |
| W8.5、W9.2～W9.3 | tag-only 發布／手動安裝腳本 | 乾淨且已授權 Windows | windows-native.yml、test-lifecycle.ps1 | 自動測試與安裝驗證已停用；安裝與升級另行手動驗證 |
| W10 | 尚未符合前置條件 | Arch 尚未開始 | — | 保留 Rust 對照 |

## 待決工程項目

已選定 .NET SDK 10.0.401、Windows 11 x64、WPF 自訂 VirtualizingPanel、SkiaSharp 3.119.4、xUnit、self-contained runtime、WiX 6.0.2。沒有待選的核心技術；剩餘工作是上列未完成驗證與兩平台共同收尾。
