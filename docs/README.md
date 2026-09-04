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

## 參考與代理規則

- [授權與再散布](reference/licensing.md)：原始碼、相依套件與內嵌資產義務。
- [`agent/`](agent/)：供程式代理使用的精簡規則；詳細程序仍由上述現行文件負責。

產品行為以產品規格為準；工程限制以執行時不變條件為準。建置、測試與發佈指令必須使用 Cargo 及 `.github/workflows/` 下的現行 Rust 工作流程。

## 文件權責

| 主題 | 說明文件 | 可執行的權威來源 |
|---|---|---|
| 使用者需求與產品範圍 | [產品規格](product/product-spec.md) | 現行 `piclens-desktop` runtime |
| 工程不變條件 | [執行時不變條件](engineering/runtime-invariants.md) | Domain、infra 與 UI |
| 分層與相依方向 | [架構](engineering/architecture.md) | Cargo crate graph |
| 建置與測試指令 | [測試](guides/testing.md) | Cargo workspace 與 lockfile |
| 發佈準備度 | [發佈與封裝](guides/release.md) | `.github/workflows/release.yml` |
| 套件版本 | [發佈與封裝](guides/release.md) | 根目錄 `Cargo.toml` 的 `[workspace.package].version` |
