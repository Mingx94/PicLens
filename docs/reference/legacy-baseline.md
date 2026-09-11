# 舊版基準

## 狀態

文件重寫前的基準 commit 是 `189d2e06812771fa5ed47de7a6550894935e277f`，Cargo 版本為 `3.2.0`。這是本機原始碼定位，不代表此 commit 已發佈或有新的執行驗證。

2026-09-11 的修改只更新規格定位、工程文件與實作 TODO。以下舊版內容暫時保留在原位置：

| 舊版內容 | 用途 |
|---|---|
| `crates/piclens-domain/` | 排序、選取相關資料、重新命名與縮放規則參考 |
| `crates/piclens-infra/` | 設定讀取、掃描、快取、轉換及 OS 行為參考 |
| `crates/piclens-desktop/` | egui 互動、工作排程、Viewer 與驗證參考 |
| `Cargo.toml`、`Cargo.lock`、`rust-toolchain.toml` | 舊 Rust 建置 |
| `scripts/`、`packaging/`、`.github/workflows/release.yml` | 舊封裝、效能與生命週期流程 |

舊 workflow 仍以 `v*` tag 觸發 Windows Rust 套件。新平台 workflow 上線前必須處理觸發衝突；不可直接用舊流程發布 WPF／Qt 版本。

## 查閱歷史

例如從 repo root 讀取舊文件：

```powershell
git show 189d2e06812771fa5ed47de7a6550894935e277f:docs/engineering/runtime-invariants.md
git show 189d2e06812771fa5ed47de7a6550894935e277f:docs/guides/release.md
git show 189d2e06812771fa5ed47de7a6550894935e277f:docs/design/system.md
```

[舊版效能記錄](legacy-performance.md)保留原始量測與限制。1024 預覽時間不能當成完整原圖時間；歷史 schema 2 CPU 平均值無效。這些結果都不能證明 WPF 或 Qt 的效能。

最終移除工作目錄內的 Rust 檔案時，保留 Git 歷史與此基準定位，不改寫歷史。參考測試應轉成語言無關案例；不要把舊實作複製成新的規格權威。
