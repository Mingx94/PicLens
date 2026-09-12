# PicLens 文件

本文件集描述 Windows／WPF 與 Arch Linux／Qt Quick 的完整重寫。兩版放在同一個 repo，分別實作；舊 Rust 與 egui 原始碼已移除。兩版程式已建立；Arch Qt 已有 Omarchy 基礎驗收，完整桌面與升級驗收仍待完成。實作與驗證狀態由兩份 TODO 記錄。

## 實作入口

- [Windows TODO](../TODO.win.md)：C#、WPF。
- [Windows 開發入口](../apps/windows/README.md)與[驗證紀錄](engineering/windows-validation.md)。
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
| [舊版基準](reference/legacy-baseline.md) | 舊程式與歷史文件的定位方式 |

## 使用規則

產品規格決定「要做什麼」，工程文件決定「必須守住哪些限制」，TODO 記錄「還沒完成什麼」。驗收對照是索引，不取代完整規格。

此次只將平台範圍收斂為 Windows 與 Arch；其餘已定義功能保留。不因框架提供現成功能而加入動畫播放、全螢幕、相簿或 SQLite 索引。

兩版專案與封裝目錄已建立；驗收命令仍須依紀錄區分已執行與待執行。Windows 證據使用新版 WPF 測試與診斷；歷史 egui 證據不能移植成新版完成狀態。
