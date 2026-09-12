# 發佈與封裝

## 狀態

目前的 `.github/workflows/release.yml`、Cargo 版本與封裝腳本仍屬舊 Rust／egui 版。Windows 新流程是 `.github/workflows/windows-native.yml`；Arch 新流程是 `.github/workflows/arch-native.yml`，封裝使用 `packaging/arch/PKGBUILD`。歷史資料見[舊版基準](../reference/legacy-baseline.md)。

## 新版版本規則

兩版允許獨立發布，採不同 tag 命名空間：

| 平台 | 新版 tag | 版本權威 | 目標產物 |
|---|---|---|---|
| Windows | `windows/v<version>` | `apps/windows/Directory.Build.props` 的共用 Version | MSI、portable ZIP、SHA-256 |
| Arch | `arch/v<version>` | `apps/linux/CMakeLists.txt` 的 project VERSION | PKGBUILD、來源封存及 SHA-256、`.pkg.tar.zst` |

Windows 目前來源版本為 4.0.2，目標為 self-contained x64。Arch 來源版本為 4.0.0，`pkgrel=1`；`pkgrel` 是封裝修訂，與 App 版本分開，`pkgver` 必須與 CMake 版本及來源 tag 一致。

Windows MSI 保留 UpgradeCode `{4B3899A4-2E9E-4B4F-9CF5-36F8D8D6767D}`。新 Windows 安裝版本需可從既有版本升級；WiX UpgradeCode、產品識別及版本排序在封裝階段檢查，不能因新框架就從不相容的安裝版本重新開始。新版本不再以 Cargo 作權威。

## CI 與觸發範圍

兩版採精簡發布流程。Windows 只由 `windows/v*` tag 觸發；單一 job 核對 annotated tag 與版本、建置 MSI／ZIP、發布 GitHub Release。Arch 只由 `arch/v*` tag 觸發；單一 job 核對乾淨 checkout、annotated tag 指向 HEAD、CMake／pkgver／pkgrel，匯出來源後在 `archlinux:base-devel` 容器建置並發布。PR／main 推送不跑這兩個流程。

Arch 容器更新完整套件庫並安裝 Qt 相依，以一般帳號執行 `makepkg --nocheck`，同時設定 `PICLENS_BUILD_TESTING=OFF`。發布未簽署 `.pkg.tar.zst`、來源 tar.gz、已填 checksum 的 PKGBUILD、來源 manifest、套件版本清單及 SHA256SUMS。一般本機 makepkg 仍預設建置及執行測試；只有發布流程省略測試。建置失敗會停止發布，不執行功能、桌面或安裝測試。流程遵守 [makepkg 的一般帳號與 --nocheck 規則](https://man.archlinux.org/man/makepkg.8)。

封裝階段建立平台 release workflow，驗證 annotated tag、來源 commit 和平台版本一致。舊 `v*` workflow 必須在新流程啟用前確認隔離，不能產生錯誤的 Rust 產物。第一版平台成果可先是候選套件，不等另一版開發完畢。

Arch 需在記錄版本的乾淨建置環境檢查相依與 PKGBUILD；桌面驗證另外執行。發布至 AUR 或其他外部位置不屬於建立 PKGBUILD 本身。

## Windows 套件

- 使用新的 WPF Release 輸出與必要解碼 helper，不能複製舊 Rust exe。
- 選定 .NET self-contained 或 framework-dependent，文件說明離線機器的需求；portable 名稱不能掩蓋缺少 runtime。
- 包含圖示、字型及必要 codec、第三方授權與 SHA-256。
- 驗證開始功能表、工作列與執行檔圖示、無 console 的正常啟動、路徑與資料延續性。
- MSI 驗證乾淨安裝、啟動、舊版升級／替換、解除安裝與 profile 保留；ZIP 另外驗證解壓啟動。

Windows 生命週期腳本是 `packaging/windows/test-lifecycle.ps1`。須在乾淨且已授權的 Windows 環境傳入 `-ConfirmSystemChanges`；`-PreviousMsiPath` 可加入舊版升級測試。未提供舊 MSI 時，升級結果會明確記為 `not-tested`。此腳本保留供手動驗證，不由 Windows 發布 workflow 自動執行。

## Arch 套件

- PKGBUILD 以固定來源 tag／commit 與 checksum 建置，不在 build 時下載未宣告依賴。
- 分清 makedepends 與 depends，包含實際使用的 Qt 模組、圖片外掛與回收筒 helper。
- 提供 desktop entry、圖示、必要 AppStream metadata；安裝路徑與權限符合封裝結果。
- 用乾淨 Arch 環境建置，確認全部支援格式和無損 WebP 可用。
- 驗證安裝、桌面啟動、升級、解除安裝與 profile 保留；Wayland／X11 結果分開列出。

## 發佈完成條件

Windows 自動發布只要求 tag／版本一致且建置封裝成功；功能、效能與安裝驗證改由發布者自行決定是否手動執行，不能把發布成功視為這些驗證已通過。以下完整驗收清單保留作為手動驗證與 Arch 規劃參考。

1. 對應平台功能驗收、測試與效能紀錄齊全。
2. 建置候選套件，檢查內容、授權、相依及 hash。
3. 在有授權的乾淨環境完成生命週期驗證。
4. 經使用者授權提交、建立 annotated tag、推送與公開發佈。
5. 確認 hosted workflow 成功，發布資產、版本與 checksum 相符。

本機建置成功不代表已發佈。未設定簽章就標明未簽署，不宣稱已簽署。

## Rust 退場

兩版皆通過功能與封裝驗收，且替代流程已能獨立運作後，才移除 Rust crates、Cargo／toolchain、egui 專用腳本與舊 workflow。保留使用中的 assets、LICENSE、規格、案例與 Git 歷史。

這是共同最後一步：一版先完成可以先交付，但不能提前刪除另一版仍需對照的舊程式。兩份 TODO 的收尾項目都需引用同一份清理證據。
