# PicLens 文件

本文件集描述 Windows／WPF 與 Arch Linux／Qt Quick 的原生實作。兩版放在同一個 repo，分別實作。兩份 TODO 只保留未完成項目。

## 文件分工

- `windows/`：Windows 專屬架構、視覺與封裝發布。
- `linux/`：Linux 專屬說明，目前交付目標為 Arch。
- `product/`、`engineering/`、`design/`、`guides/`、`reference/`：兩平台共用規格與原則。簡短的平台差異以同頁表格對照。
- 建置與診斷指令留在各 `apps/` README；封裝腳本細節留在 `packaging/`，不在 docs 重複維護。

## 實作入口

- [Windows TODO](../TODO.win.md)：C#、WPF。
- [Windows 文件](windows/README.md)：架構、設計、發布及開發入口。
- [Linux／Arch 文件](linux/README.md)：架構、設計、發布及開發入口。
- [Arch TODO](../TODO.arch.md)：C++20、Qt Quick／QML。
- [開發指南](guides/development.md)：執行順序、文件更新與交付方式。

## 文件權責

| 文件 | 權責 |
|---|---|
| [產品規格](product/product-spec.md) | 使用者行為、功能範圍與既有品質要求 |
| [驗收對照](product/acceptance.md) | 共用案例編號、預期結果、兩版實作階段 |
| [設計系統](design/system.md) | 視覺角色、版面與原生元件適配 |
| [架構](engineering/architecture.md) | 分層、技術決策與平台邊界 |
| [執行時不變條件](engineering/runtime-invariants.md) | 資料安全、資源上限、取消與競態 |
| [資料延續性](engineering/data-continuity.md) | 舊設定相容、路徑、隔離與遷移 |
| [效能](engineering/performance.md) | 新版量測定義、條件與目標 |
| [測試](guides/testing.md) | 驗證層級、案例資料與完成證據 |
| [發佈與封裝](guides/release.md) | 新版版本規則、產物、CI 與切換條件 |
| [授權與再散布](reference/licensing.md) | 套件、圖片解碼器、字型與資產清單 |

## 使用規則

產品規格決定「要做什麼」，工程文件決定「必須守住哪些限制」，TODO 記錄「還沒完成什麼」。驗收對照是索引，不取代完整規格。

此次只將平台範圍收斂為 Windows 與 Arch；其餘已定義功能保留。不因框架提供現成功能而加入動畫播放、全螢幕、相簿或 SQLite 索引。
