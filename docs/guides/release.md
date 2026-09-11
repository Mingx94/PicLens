# 發佈與封裝

## 狀態

目前的 `.github/workflows/release.yml`、Cargo 版本與封裝腳本仍屬舊 Rust／egui 版。Windows 新流程是 `.github/workflows/windows-native.yml`，候選套件由 `packaging/windows/build.ps1` 建置。Arch PKGBUILD 尚未建立。歷史資料見[舊版基準](../reference/legacy-baseline.md)。

## 新版版本規則

兩版允許獨立發布，採不同 tag 命名空間：

| 平台 | 新版 tag | 版本權威 | 目標產物 |
|---|---|---|---|
| Windows | `windows/v<version>` | `apps/windows/Directory.Build.props` 的共用 Version | MSI、portable ZIP、SHA-256 |
| Arch | `arch/v<version>` | 未來 `apps/linux/CMakeLists.txt` 的 project VERSION | PKGBUILD、來源封存及 SHA-256、可驗證的 `.pkg.tar.zst` |

Windows 目前版本為 4.0.1，self-contained x64，未簽署。Arch 版本檔尚未建立。Arch 的 `pkgrel` 是封裝修訂，與 App 版本分開；`pkgver` 必須能對應來源 tag。

Windows MSI 保留 UpgradeCode `{4B3899A4-2E9E-4B4F-9CF5-36F8D8D6767D}`。新 Windows 安裝版本需可從既有版本升級；WiX UpgradeCode、產品識別及版本排序在封裝階段檢查，不能因新框架就從不相容的安裝版本重新開始。新版本不再以 Cargo 作權威。

## CI 與觸發範圍

建立互相獨立的 Windows／Arch 建置與測試工作。平台程式變更觸發該平台；共用規格、test-data 與資產變更需檢查兩版。

封裝階段建立平台 release workflow，驗證 annotated tag、來源 commit 和平台版本一致。舊 `v*` workflow 必須在新流程啟用前確認隔離，不能產生錯誤的 Rust 產物。第一版平台成果可先是候選套件，不等另一版開發完畢。

Arch 需在記錄版本的乾淨建置環境檢查相依與 PKGBUILD；桌面驗證另外執行。發布至 AUR 或其他外部位置不屬於建立 PKGBUILD 本身。

## Windows 套件

- 使用新的 WPF Release 輸出與必要解碼 helper，不能複製舊 Rust exe。
- 選定 .NET self-contained 或 framework-dependent，文件說明離線機器的需求；portable 名稱不能掩蓋缺少 runtime。
- 包含圖示、字型及必要 codec、第三方授權與 SHA-256。
- 驗證開始功能表、工作列與執行檔圖示、無 console 的正常啟動、路徑與資料延續性。
- MSI 驗證乾淨安裝、啟動、舊版升級／替換、解除安裝與 profile 保留；ZIP 另外驗證解壓啟動。

Windows 生命週期腳本是 `packaging/windows/test-lifecycle.ps1`。須在乾淨且已授權的 Windows 環境傳入 `-ConfirmSystemChanges`；`-PreviousMsiPath` 可加入舊版升級測試。未提供舊 MSI 時，升級結果會明確記為 `not-tested`。CI 已接上乾淨 runner 的安裝、啟動、修復、解除安裝與設定保留檢查，但尚未推送或執行 hosted 工作。

## Arch 套件

- PKGBUILD 以固定來源 tag／commit 與 checksum 建置，不在 build 時下載未宣告依賴。
- 分清 makedepends 與 depends，包含實際使用的 Qt 模組、圖片外掛與回收筒 helper。
- 提供 desktop entry、圖示、必要 AppStream metadata；安裝路徑與權限符合封裝結果。
- 用乾淨 Arch 環境建置，確認全部支援格式和無損 WebP 可用。
- 驗證安裝、桌面啟動、升級、解除安裝與 profile 保留；Wayland／X11 結果分開列出。

## 發佈完成條件

1. 對應平台功能驗收、測試與效能紀錄齊全。
2. 建置候選套件，檢查內容、授權、相依及 hash。
3. 在有授權的乾淨環境完成生命週期驗證。
4. 經使用者授權提交、建立 annotated tag、推送與公開發佈。
5. 確認 hosted workflow 成功，發布資產、版本與 checksum 相符。

本機建置成功不代表已發佈。未設定簽章就標明未簽署，不宣稱已簽署。

## Rust 退場

兩版皆通過功能與封裝驗收，且替代流程已能獨立運作後，才移除 Rust crates、Cargo／toolchain、egui 專用腳本與舊 workflow。保留使用中的 assets、LICENSE、規格、案例與 Git 歷史。

這是共同最後一步：一版先完成可以先交付，但不能提前刪除另一版仍需對照的舊程式。兩份 TODO 的收尾項目都需引用同一份清理證據。
