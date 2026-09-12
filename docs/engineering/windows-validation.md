# Windows WPF 驗證紀錄

日期：2026-09-11～12。目前來源版本 4.0.2，既有候選封裝為 4.0.1；4.0.2 尚未重新封裝或發布。Windows 程式不引用 Rust；Arch 尚未開始，舊 Rust 依共同退場條件保留。

## 已交付

- 六個獨立 .NET 專案：WPF App、Core、Services、Imaging、可終止 Worker、Tests。
- 圖庫、資料夾樹與歷史、搜尋、四種排序、多選、縮圖、完整原圖 Viewer、JPG／WebP、回收與拖放重新命名流程。
- 暖灰／森林綠介面、深色、元件展示、鍵盤入口、診斷截圖與量測。
- self-contained x64 ZIP／MSI、SHA-256、獨立 Windows CI 與生命週期測試腳本。候選套件未簽署。

實作完成與各項驗收通過是不同狀態。下列未驗證項目仍在 TODO 保持未勾選。

## 自動測試

在 Windows 11 x64、.NET SDK 10.0.401／runtime 10.0.12 執行：

~~~powershell
Set-Location apps/windows
dotnet restore PicLens.Windows.slnx --locked-mode
dotnet test PicLens.Windows.slnx -c Release --no-restore --logger trx
~~~

Debug 與 Release 各 45 通過、0 失敗、0 略過。測試結果在本機 apps/windows/tests/PicLens.Tests/TestResults/；這是既有本機證據；精簡後的發布 workflow 不執行測試或上傳 TRX。主要證據：

| 範圍 | 已驗證內容 |
|---|---|
| 規則與資料 | 共用自然排序／設定 fixtures、資料夾優先、歷史分支、Ctrl／Shift 選取順序、指標縮放、非法檔名、設定往返與損壞隔離 |
| Codec | 六種靜態格式、GIF 多幀辨識與拒絕、WebP 無損像素往返 |
| 工作與快取 | helper 逾時後重開、8 程序上限、280 請求取消與關閉、PNG 快取／1024 預覽分流、壞快取重建與暫存清理 |
| 檔案安全 | 49／50 確認邊界、原檔保留、取消零修改、目標衝突、來源變更與消失、跨格式序號衝突、逐項失敗續跑、鎖定設定禁止覆寫、Shell 永久刪除 callback 拒絕 |
| WPF／Dispatcher | 10,000 項目的回收容器有界限、清單重設、5000 像素分塊、A-B-A、關閉重開、Viewer 序列快照、搜尋不掃描與 picker root 保存 |

測試只修改 artifacts/wpf-tests/ 的自有案例。測試清理對已核對的專屬目錄執行；短暫 Windows 檔案鎖最多重試 500ms，不忽略持續鎖定。

## 4.0.1 修正

- 縮圖成功重試與快取命中會清除錯誤標記。成功／失敗更新都檢查取消、generation、可見狀態與目前請求身分；舊工作不能改變新工作的錯誤或清除新 pending。
- 設定儲存失敗會在底部顯示持續提醒與重試按鈕，不被導覽狀態蓋掉。重試儲存最新設定，完成後才清除提醒，並保留原本的序列寫入與原子替換。
- 新增 5 項測試：縮圖重試／快取清除錯誤、舊失敗先後到達與舊成功遲到、設定鎖定失敗到重試成功。設定測試也確認舊檔保持不變、使用最新值，通知在 UI Dispatcher 執行。

## 實際程式與效能

以下效能數字是 4.0.0 的既有證據，4.0.1 本次修正未重新量測完整效能矩陣。

環境：Windows 11 build 26200、Intel i5-12400（12 邏輯處理器）、RTX 3060 Ti、100% DPI。尺寸 1600×1000。所有效能數值來自 Release；未量測 GPU／compositor 最終呈現完成時間。

混合圖庫為 207 張：129 JPG、53 WebP、25 PNG。使用自有隔離 profile；讀取原檔前後逐檔核對 SHA-256，207 張均未改變。私人路徑與 manifest 僅存本機 artifacts，不提交。

| 指標 | 冷縮圖快取 | 暖縮圖快取 |
|---|---:|---:|
| 圖庫載入 | 176.86ms | 181.98ms |
| 搜尋清除／投影重建 | 5.42ms | 7.90ms |
| 完整原圖繪製樣本 | 25 | 25 |
| 最慢完整原圖繪製 | 280.42ms | 256.28ms |
| 超過 500ms | 0 | 0 |
| 未完成選取 | 0 | 0 |
| 最多實體卡片 | 28 | 28 |
| App 峰值 working set | 249,921,536 bytes | 245,088,256 bytes |
| App 平均 CPU（未除以邏輯處理器數） | 27.80% | 27.07% |

冷暖指縮圖磁碟快取；未清除 Windows 檔案系統快取。完整原圖不落磁碟快取。量測從每次 Viewer 載入開始，到完整解析度 WPF OnRender 提交；1024 預覽不算完成。固定間隔連續前後切圖各最多 12 張，快速取消競態另由 STA 測試驗證。以上是此機器／樣本的結果，不能推成所有圖片的 500ms 保證。CPU 與記憶體只包含 App，不包含 helper。

另以品牌圖示的 10,000 份自有小圖副本驗證圖庫規模：載入 233.71ms、清除搜尋重建 0.98ms、最多 28 個容器、App 峰值 189,550,592 bytes、平均 CPU 26.29%，診斷完整結束。此組只能佐證載入／虛擬化，不能推定大型照片解碼效能。

本機證據：artifacts/wpf-final-cold.json、wpf-final-warm.json、wpf-10000.json、wpf-source-manifest.json。對應 profile 紀錄正常關閉，沒有 UI 未處理錯誤。

## 畫面

以 App 自己的 RenderTargetBitmap 診斷擷取實際 WPF client area，沒有使用 Computer Use。已檢查：

- 1600×1000 淺色圖庫。
- 800×600 最小視窗：工具列換行、固定縮圖比例、獨立捲軸。
- 深色圖庫：修正 TreeViewItem、ListBoxItem 與 CheckBox 的系統預設黑色文字。
- 內嵌 Viewer：名稱、返回、前後、縮放、原圖畫布。

本機截圖：artifacts/wpf-final-light.png、wpf-checked-narrow.png、wpf-checked-dark.png、wpf-checked-viewer.png。截圖不涵蓋 OS 標題列／工作列或原生 picker，也不能證明 UI Automation、IME 與高 DPI 操作。

## 驗收案例對照

| 案例 | 實作與證據 | 尚待驗證 |
|---|---|---|
| START-01、TREE-01、NAV-01 | 隔離啟動、固定尺寸、Picker root／歷史／搜尋 STA 測試、樹與側鍵事件 | 原生 picker 取消、深層樹與側鍵人工操作 |
| SCAN-01、SORT-01、SEARCH-01、GRID-01 | Scanner／Core／WPF 測試、10,000 筆實際程式診斷 | 人工長時間瀏覽與網路／權限異常環境 |
| SELECT-01、MENU-01 | 選取規則、原生 ListBox 狀態同步、右鍵情境與鍵盤入口 | IME、實際 UIA 選取、所有焦點返回組合 |
| THUMB-01、JOB-01 | 佇列／程序上限、逾時、壞圖、快取、取消、退出測試與實際捲動 | 長時間磁碟快取輪替壓力 |
| VIEW-01～VIEW-04 | 序列快照、縮放、完整原圖分塊、預覽與相鄰預載、A-B-A／重開測試、50 次完整繪製 | 動畫 WebP 實檔、超大圖與不同 GPU／DPI 顯示矩陣 |
| FILE-01、FILE-02、FILE-03 | codec、計畫、49／50、衝突／取消／部分失敗測試；回收 helper 已接上 Windows 回收筒 | 實際對話框、reveal 與 OS 回收失敗情境 |
| DRAG-01、DRAG-02、RESULT-01 | WPF DragDrop、預覽、序號、逐項結果、toast、紀錄 | 實際多選拖放、capture-lost 與邊界自動捲動 |
| DATA-01 | 舊 JSON／數字 enum、損壞與鎖定、原子替換、picker 保存測試 | 已安裝舊版升級與真實使用者 profile 副本 |
| OS-01、UI-01 | picker／reveal、圖示、鍵盤／主題／元件入口均已實作；100% DPI 畫面已檢查 | Windows 高對比、150%／200% DPI、IME、UIA、開始功能表／工作列 |
| PERF-01 | 上表冷暖混合圖庫與 10,000 筆規模證據 | 不推定其他硬體或任意圖片 |
| PACKAGE-01 | MSI 靜態 ICE 驗證、self-contained 封裝、授權與雜湊 | 乾淨機安裝／舊版升級／解除安裝及 hosted 結果 |

## 原生回收補強

回收 helper 使用 IFileOperation 的 RECYCLEONDELETE／EARLYFAILURE。PreDeleteItem 遇到不帶回收旗標的刪除操作會回傳錯誤並中止；沒有永久刪除替代路徑。非固定磁碟／網路位置目前回報不支援並保留來源。介面與中止語意依 [Microsoft PreDeleteItem 文件](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifileoperationprogresssink-predeleteitem)。

本次以 artifacts 下新建的品牌圖示副本實測：回收成功、在 Windows 回收筒找到唯一對應項目、核對內容 hash、還原同一項目後再次核對 hash。沒有操作既有圖片或其他回收筒項目；紀錄在 artifacts/wpf-recycle-result.json。另有測試確認不帶回收旗標時，callback 在執行前拒絕。

## 封裝與發布界線

建置腳本保留舊 UpgradeCode，以下既有封裝證據對應 4.0.1。MSI 使用 per-machine 程式檔案與符合 ICE 規則的捷徑 registry keypath；完整驗證沒有停用。Windows runtime、Skia codec、helper、品牌圖示及必要授權隨套件提供。

ZIP 已解壓至新的 artifacts 目錄並成功啟動。用模組路徑確認 coreclr.dll 由解壓套件載入；即使 DOTNET_ROOT 指向不存在位置仍可啟動。合成舊設定的排序、縮圖與側欄保持不變，舊視窗尺寸未被還原；元件展示也可正常關閉。這不取代乾淨機安裝證據。

MSI 資料庫以唯讀方式核對 ProductVersion、UpgradeCode，以及全部 610 個檔案的名稱／大小與 payload 一致。MSI 靜態驗證 0 警告、0 錯誤，簽章狀態 NotSigned。

4.0.0 ZIP 的解壓證據在 artifacts/wpf-portable-final-31bbca1d481d4da9b53dbb086d6584b7/。另驗證空 profile 啟動、無效參數輸出繁中錯誤並以 exit 2 結束，所有診斷結束後沒有殘留 PicLens／Worker 程序。

| 4.0.0 初次候選資產 | SHA-256 |
|---|---|
| PicLens-4.0.0-windows-x86_64.msi | ec6b6117374e7e2a5a91fc60c4e96a6da65f4b0f550dc3549b51b8255cc3368a |
| PicLens-4.0.0-windows-x86_64.zip | 5c5dc6e6e907ed13a5e7041b27eea7139a8f3ea10b829a48b27cbeddcee745c5 |

4.0.1 已重新建置。MSI 靜態驗證 0 警告／0 錯誤，ProductVersion 為 4.0.1，610 個檔案的名稱／大小均與 payload 相符。ZIP 解壓啟動、截圖及正常關閉通過，exit 0。證據在 artifacts/wpf-401-smoke-088bfc0bf1e84927add39822bc150fab/；此次尚未實測 4.0.0 → 4.0.1 安裝升級。

| 4.0.1 修正候選資產 | SHA-256 |
|---|---|
| PicLens-4.0.1-windows-x86_64.msi | 69dba269ad37c1dcbf07138f814472c90ee291ba73547f4c6663ffaa7ee06573 |
| PicLens-4.0.1-windows-x86_64.zip | cb83b149b06c984cee375d7af75d98a3ad6b46e3995bfd60350ed0c3513ea643 |

未在使用者系統安裝、解除安裝或變更全域設定。生命週期腳本需明確授權；精簡後的 workflow 不執行安裝、啟動、修復與解除安裝驗證，腳本保留供手動使用。本次未推送或觸發發布。提供 PreviousMsiPath 才能把舊版升級列為通過。

未提交、推送、建立 tag、簽署或公開發布。Arch 尚未通過 A0～A9，因此 W10 共同移除 Rust 的前置條件仍未滿足。
