# 資料延續性

本文件是兩版重寫必須保留的資料契約。換語言不代表可以重設設定或搬走使用者資料。新版尚未驗證相容性，驗收見 [DATA-01](../product/acceptance.md)。

## 路徑

未設定覆寫時，保留既有位置：

| 內容 | Windows | Arch Linux |
|---|---|---|
| 資料根目錄 | `%LOCALAPPDATA%\PicLens` | `$XDG_DATA_HOME/PicLens`；未設定則 `~/.local/share/PicLens` |
| 設定 | 根目錄下 `piclens-settings.json` | 同左 |
| 紀錄 | `Logs/PicLens.log` | 同左 |
| 縮圖 | `Thumbnails/` | 同左 |

`PICLENS_DATA_ROOT` 與診斷用 `--data-root` 保留隔離用途；命令列優先於環境變數。覆寫指向的目錄就是資料根目錄，不再多加一層 PicLens。需測試空值、空白、Unicode 路徑與既有 Windows 百分比環境變數展開行為。

不要直接採用框架預設路徑而悄悄改成 Roaming、另一個組織名稱或 XDG config 根目錄。

## 設定格式

保留 camelCase JSON 與數字排序值。下列是欄位範例，不代表固定使用者路徑：

```json
{
  "lastFolderPath": null,
  "sort": { "key": 0, "direction": 0 },
  "includeSubfolders": false,
  "thumbnailSize": 160,
  "sidebarCollapsed": false,
  "windowWidth": null,
  "windowHeight": null
}
```

- `sort.key`：0 是名稱、1 是修改時間；`direction`：0 是升冪、1 是降冪。其他整數回到對應的 0 預設。
- 縮圖大小預設 160，範圍 120～240，以 20 為步長正規化；0 回到預設。兩版須以相同案例確認取整邊界。
- 缺少欄位使用預設；未知欄位忽略。錯誤型別、損壞 JSON 與合法但超界值需分開測試。
- 舊 `windowWidth`／`windowHeight` 保持可讀；兩者都有值時正規化至少 800×600，否則視為未設定。
- 視窗每次啟動固定 1600×1000，不還原舊尺寸。欄位相容性不改變此產品行為。
- 只有資料夾選擇器更新 `lastFolderPath`；樹、圖庫資料夾與歷史導覽不更新它。

## 寫入與失敗

先正規化，再寫入同目錄暫存檔，完成後以平台支援的原子替換方式更新。寫入失敗不得破壞既有設定。

損壞設定先改名為 `piclens-settings.json.corrupt.<suffix>`，再使用預設。若無法讀取或隔離舊檔，禁止以預設值直接覆寫，需回報錯誤。

第一階段保留 JSON，不新增 SQLite 遷移。若未來調整 schema，需定義版本、備份、失敗恢復及舊欄位讀取規則。

## 快取與紀錄

快取可以重建，不要求跨語言沿用 PNG 命中率，但必須辨識格式／版本，不能把不相容檔案當成有效像素。快取清理只能處理自身擁有的資料，不能修改來源圖片。

保留 append-only 的 app log 位置。新版紀錄需包含平台、版本與 build profile，避免和舊版診斷混淆。輪替政策若要新增，需保留排錯需要的資訊並另行記錄。

## 驗證與升級

使用合成設定，或經授權取得的 profile 副本。測試完整欄位、缺欄位、數字排序、未知欄位、損壞 JSON、無權限、寫入中斷與失效資料夾。不得從個人 profile 擷取資料提交到 repo。

`PICLENS_DATA_ROOT` 只隔離設定、快取與紀錄，並不隔離 `--folder` 指向的圖片。檔案操作使用可丟棄的圖片副本。

封裝驗收要從舊版 profile 副本啟動新版，確認可讀、可保存、取消不改檔、解除安裝保留 profile。兩版各自驗證；不要求把 Windows 絕對路徑自動翻譯成 Linux 路徑。
