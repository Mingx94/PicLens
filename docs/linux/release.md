# Linux／Arch 封裝與發布

共用授權、發布與驗證原則見[發布指南](../guides/release.md)。

## 版本與產物

版本以 [CMakeLists.txt](../../apps/linux/CMakeLists.txt) 的 `project VERSION` 為準。發布 tag 為 `arch/v<version>`；[PKGBUILD](../../packaging/arch/PKGBUILD) 的 `pkgver` 必須與 CMake 及 tag 一致。`pkgrel` 是獨立的封裝修訂。範例中的 `<version>` 與 `<pkgrel>` 請替換為該次發布的實際值。

## 本機封裝

本機可在 repo 根目錄執行 `sudo ./packaging/arch/build-release.sh`。腳本以 sudo 的原使用者匯出來源並建置，每次建置先清空 `dist/arch/`，來源、建置中間檔與成品全部放在該目錄，例如 `dist/arch/piclens-<version>-<pkgrel>-x86_64`；相依齊備時不更新系統。詳見 [Arch 本機交付](../../packaging/arch/README.md)。

## GitHub Actions

[arch-native.yml](../../.github/workflows/arch-native.yml) 只由 `arch/v*` tag 觸發；單一 job 核對乾淨 checkout、annotated tag 指向 HEAD、CMake／pkgver／pkgrel，匯出來源後在 `archlinux:base-devel` 容器建置並發布。PR／main 推送不觸發。

Arch 容器在缺少相依時更新完整套件庫並安裝 Qt 相依，以一般帳號執行 `makepkg --nocheck`，同時設定 `PICLENS_BUILD_TESTING=OFF`。發布未簽署 `piclens-<version>-<pkgrel>-x86_64` 套件、來源 tar.gz、已填 checksum 的 PKGBUILD、來源 manifest、套件版本清單及 SHA256SUMS。一般本機 makepkg 仍預設建置及執行測試；release 腳本的本機 sudo 與容器模式均省略測試。建置失敗會停止發布，不執行功能、桌面或安裝測試。流程遵守 [makepkg 的一般帳號與 --nocheck 規則](https://man.archlinux.org/man/makepkg.8)。

## 套件與手動驗證

- PKGBUILD 以固定來源 tag／commit 與 checksum 建置，不在 build 時下載未宣告依賴。
- 分清 makedepends 與 depends，包含實際使用的 Qt 模組、圖片外掛與回收筒 helper。
- 提供 desktop entry、圖示、必要 AppStream metadata；安裝路徑與權限符合封裝結果。
- 用乾淨 Arch 環境建置，確認全部支援格式和無損 WebP 可用。
- 驗證安裝、桌面啟動、升級、解除安裝與 profile 保留；Wayland／X11 結果分開列出。

Arch 需在記錄版本的乾淨建置環境檢查相依與 PKGBUILD；桌面驗證另外執行。發布至 AUR 或其他外部位置不屬於建立 PKGBUILD 本身。
