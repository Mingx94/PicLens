# PicLens 文件

PicLens 是使用 Rust、egui、eframe 與 wgpu 建置的桌面圖片圖庫與檢視工具。專案簡介請見[儲存庫 README](../README.md)。

## 產品與設計

- [產品規格](product/product-spec.md)：使用者功能、產品範圍與驗收意圖。
- [設計系統](design/system.md)：現行 egui 版面、色彩與元件規則。

## 工程

- [架構](engineering/architecture.md)：crate 分層、相依方向與執行時組成。
- [執行時不變條件](engineering/runtime-invariants.md)：資料、非同步工作、檔案操作與互動邊界。
- [資料延續性](engineering/data-continuity.md)：設定、紀錄、快取與隔離 profile。
- [效能](engineering/performance.md)：現行保護機制、量測規則與有效證據。

## 開發與發佈

- [開發指南](guides/development.md)：修改入口與交付檢查。
- [測試](guides/testing.md)：Cargo 檢查、驗證層級與執行時 smoke。
- [發佈與封裝](guides/release.md)：版本、產物、驗證與發佈流程。

## 參考

- [授權與再散布](reference/licensing.md)：原始碼、相依套件與內嵌資產義務。

文件描述預期行為。驗證時，以現行 runtime、Cargo workspace、lockfile 與 `.github/workflows/` 為準。
