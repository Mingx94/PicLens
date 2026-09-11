# PicLens Windows（WPF）

Windows 原生重寫，C#／.NET 10／WPF，獨立於 Rust 舊版。版本來源是 Directory.Build.props。支援目標為 Windows 11 x64；開發使用 global.json 鎖定的 .NET SDK 10.0.401。

## 開發

在 repo root 執行：

```powershell
Set-Location apps/windows
dotnet restore PicLens.Windows.slnx --locked-mode
dotnet build PicLens.Windows.slnx -c Debug --no-restore
dotnet test PicLens.Windows.slnx -c Release --no-restore
dotnet run --project src/PicLens.App -- --data-root F:\PicLens\artifacts\wpf-profile --folder D:\Pictures
```

建置 WPF 需要 Windows。tests 包含純規則、圖片 codec、隔離檔案服務及 STA 虛擬化檢查；測試不操作個人圖片或安裝 App。

設定儲存失敗時，底部會持續顯示提醒；排除寫入問題後可按「重試儲存設定」。提醒在最新設定成功儲存後消失。

## 診斷

- `--folder <path>`：開啟本機資料夾；不改寫最後一次 picker 路徑。
- `--data-root <path>`：覆寫設定、快取與紀錄；優先於 PICLENS_DATA_ROOT。
- `--smoke-ms <positive integer>`：載入與選定診斷後計時關閉。
- `--viewer <image>`：從已載入圖庫開啟圖片。
- `--metrics <json>`：關閉時輸出 WPF schema 1 量測。
- `--screenshot <png>`：擷取 WPF client area，不包含 OS chrome。
- `--exercise`：程式內執行搜尋、捲動、Viewer 連續前後各最多 12 張；不執行檔案操作。
- `--components`：開啟不讀圖庫的元件展示視窗。
- `--dark`：診斷深色樣式；預設跟隨系統主題。
- `--width`／`--height`：診斷尺寸；正常啟動固定 1600×1000，最小 800×600。

隔離資料目錄不會隔離來源圖片。轉檔／改名／回收的測試必須使用副本。CPU 指標未除以邏輯處理器數；sharp paint 計到 WPF OnRender 提交完整原圖，不是 GPU 或 compositor 完成。

## 專案

| 專案 | 責任 |
|---|---|
| PicLens.Core | 排序、搜尋、選取、設定、檔案計畫與縮放 |
| PicLens.Services | 掃描、設定／紀錄、工作池、快取、檔案操作 |
| PicLens.Imaging | SkiaSharp 解碼、PNG／JPG／lossless WebP 編碼 |
| PicLens.Worker | 可終止的解碼／轉換／回收子程序 |
| PicLens.App | WPF／XAML、ViewModel、虛擬圖庫與分塊 Viewer |
| PicLens.Tests | 純規則、整合及 STA 測試 |

圖庫使用自行實作的 VirtualizingPanel 與 WPF container recycling。圖片 UI 資源為 BitmapSource，原圖超過 2048 像素分塊並加一像素邊界；未縮小原圖。SkiaSharp 只在 helper 解碼／編碼。

## 封裝

從 repo root 執行 `./packaging/windows/build.ps1`。輸出 `dist/PicLens-4.0.1-windows-x86_64.msi`、ZIP 與各自 SHA-256。封裝 self-contained .NET runtime，不要求終端使用者另裝 .NET；預設未簽署。

MSI 保留既有 UpgradeCode；實際舊版升級及乾淨機安裝仍須依授權驗證。建置腳本不安裝、不推送、不發布。新 CI 使用 `windows/v<version>`，舊 `v*` tag 路線保留給 Rust 歷史版。

回收使用 IFileOperation 與回收檢查；不提供永久刪除替代路徑。網路／非固定磁碟的回收會回報不支援，保留來源。

第三方清單見 THIRD-PARTY.md。功能與未完成驗收狀態見 repo 根目錄 TODO.win.md。
