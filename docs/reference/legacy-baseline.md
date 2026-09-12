# 舊版基準

## 狀態

文件重寫前的基準 commit 是 `189d2e06812771fa5ed47de7a6550894935e277f`，Cargo 版本為 `3.2.0`。這是本機原始碼定位，不代表此 commit 已發佈或有新的執行驗證。

2026-09-12 依使用者指示完成來源清理。以下舊版內容已從目前工作樹移除，參考用途改由 Git 歷史提供：

| 舊版內容 | 用途 |
|---|---|
| `crates/piclens-domain/` | 排序、選取相關資料、重新命名與縮放規則參考 |
| `crates/piclens-infra/` | 設定讀取、掃描、快取、轉換及 OS 行為參考 |
| `crates/piclens-desktop/` | egui 互動、工作排程、Viewer 與驗證參考 |
| `Cargo.toml`、`Cargo.lock`、`rust-toolchain.toml` | 舊 Rust 建置 |
| `scripts/`、`installer/`、`packaging/piclens.*`、`.github/workflows/release.yml` | 舊封裝、效能與生命週期流程；保留原生版 `packaging/windows/` 與 `packaging/arch/` |

舊 `v*` tag workflow 已移除。目前只有 `windows/v*` 與 `arch/v*` 的原生版發布流程。沒有刪除或改寫 Git 歷史與既有 tag。

## 查閱歷史

例如從 repo root 讀取舊文件：

```powershell
git show 189d2e06812771fa5ed47de7a6550894935e277f:docs/engineering/runtime-invariants.md
git show 189d2e06812771fa5ed47de7a6550894935e277f:docs/guides/release.md
git show 189d2e06812771fa5ed47de7a6550894935e277f:docs/design/system.md
```

[舊版效能記錄](legacy-performance.md)保留原始量測與限制。1024 預覽時間不能當成完整原圖時間；歷史 schema 2 CPU 平均值無效。這些結果都不能證明 WPF 或 Qt 的效能。

共用 assets、LICENSE、規格與 test-data 已保留。清理來源不代表完成兩版 OS／安裝／升級驗收；未完成案例仍留在平台 TODO。後續已清除舊 Rust／WiX 建置快取，保留交付證據及舊 MSI 升級素材，見[清理紀錄](../engineering/native-cleanup.md)。
