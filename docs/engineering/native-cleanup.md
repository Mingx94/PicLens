# 原生遷移清理

2026-09-12，依使用者「開始清理」指示，先完成舊來源退場。這調整原先等待兩版完整驗收後才刪除的順序；未完成的功能、桌面、安裝與升級案例仍保持待驗收。

## 移除範圍

共移除 60 個 Git 追蹤檔案：

- `crates/` 三個 Rust crate 的來源、測試與 Cargo 設定。
- 根目錄 `Cargo.toml`、`Cargo.lock`、`rust-toolchain.toml`。
- 舊 `installer/` WiX 專案。
- `scripts/` 的 Rust 建置、DEB／RPM、效能與套件生命週期腳本。
- `packaging/piclens.desktop`、`packaging/piclens.metainfo.xml`。
- `.github/workflows/release.yml` 的舊 `v*` Rust 發布流程。

保留 `apps/windows/`、`apps/linux/`、`packaging/windows/`、`packaging/arch/`、兩個 native workflow，以及全部共用 assets、LICENSE、規格與 test-data。Git 歷史及 tag 未改寫。歷史行為查閱方式見[舊版基準](../reference/legacy-baseline.md)。

後續依使用者「繼續清理」指示，移除本機 `target/` 與舊 `installer/obj/` 快取，以及已空的 `crates/`、`scripts/` 目錄。共刪除 9,311 個快取檔案，檔案大小合計 11,953,258,929 bytes（約 11.95 GB）；實際磁碟釋放量可能受檔案系統配置影響。刪除前已核對絕對路徑均在 repo 內，且沒有 reparse point；刪除後確認四個目錄不存在。

保留原生版 build、dist 與 artifacts 的產物及驗收證據，也保留 3.2.0 MSI 與 checksum 作為舊版升級驗收素材。快取清理清單存於 `artifacts/legacy-cache-cleanup.json`。

第三次清理移除 11 個封裝中間目錄：六份 `wpf-payload-*`、舊 Rust `msi-payload`、三份 Arch exporter 測試快照及初期 `piclens-4.0.0-handoff`。共 3,682 個檔案、2,179,908,064 bytes（約 2.18 GB）。刪除前已核對目錄在 dist 下且沒有 reparse point；測試快照的 manifest／checksum／PKGBUILD 保存在 `artifacts/packaging-stage-cleanup/`，作為紀錄，並非仍可安裝的交付包。

已保留並核對 3.2.0 MSI、4.0.0／4.0.1／4.0.2 Windows ZIP 及 4.0.2 MSI 的 SHA-256；Windows ZIP 與最終 Arch 交付 ZIP 的 CRC 均通過。原生版 build、最終交付包及操作／效能證據沒有刪除。封裝完整性不代表安裝／升級或桌面功能驗收。

## 驗證範圍

本機 Windows 原生 Release 建置成功（0 警告／0 錯誤），Qt windows-preview 建置成功。建置命令均未呼叫 Cargo，兩版專案／封裝沒有指向被刪除的舊來源或腳本。

- `dotnet test PicLens.Windows.slnx -c Release --no-build --no-restore`：45 通過，0 失敗／略過。
- `ctest --preset windows-preview --output-on-failure`：4/4 suite 通過，20.32 秒。
- 兩個 native workflow 的 actionlint 通過；Markdown 本機連結與 `git diff --check` 通過。

Windows 首次沙箱內建置因 obj 暫存檔寫入權限失敗；相同命令在核准的沙箱外成功，未修改原生程式。

尚未在移除 Rust 工具鏈的乾淨 OS 執行建置與封裝，也未安裝套件或觸發 GitHub 發布；W10.3／A10.3 保持待驗收。來源清理不能代替這些證據。
