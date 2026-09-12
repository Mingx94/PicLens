# Windows／WPF 文件

本目錄收錄平台專屬架構、視覺與發布規則。共用功能與工程契約見[文件索引](../README.md)。

- [架構](architecture.md)：平台執行緒、圖庫、解碼與系統整合。
- [設計](design.md)：配色、字型與原生元件。
- [封裝與發布](release.md)：版本、產物與 GitHub Actions。
- [開發與診斷入口](../../apps/windows/README.md)：工具、建置、測試及命令列參數。
- [第三方授權](../../apps/windows/THIRD-PARTY.md)：平台依賴與散布聲明。

## 驗收狀態

Windows 實作與平台驗收已於 2026-09-12 完成：

- 使用實際注音輸入法完成 `ㄋ`、`ㄋ一` 到「你」的組字；UI Automation 可讀取搜尋欄、資料夾樹、圖庫項目、按鈕名稱、角色與狀態。
- 使用 Windows 原生資料夾選擇器開啟測試資料夾；「在檔案總管顯示」會開啟檔案總管並選取正確圖片。
- 在 150% 與 200% 縮放，以及「水」對比佈景主題下檢查實際畫面；Viewer 仍回報 `300 × 300` 且圖片未拉伸。驗收後已還原為 100% 與無對比佈景主題。
- 實際 Release 視窗使用 PicLens 圖示；Windows Shell 將執行中的 EXE 辨識為 PicLens，視窗提供有效的大、小圖示，EXE 也可抽出關聯圖示。原生視窗、工作列與封裝捷徑共用 `assets/AppIcon.ico`。
