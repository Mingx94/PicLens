# PicLens 文件

PicLens 是 Windows / 主流 Linux Avalonia / MVVM 圖片整理與檢視 app。

## 文件

- [產品規格](product-spec.md) 定義不含使用框架與工程實作細節的產品行為、使用者流程與品質要求。
- [Architecture](architecture.md) 說明 solution layout、責任邊界，以及目前的 Avalonia shell。
- [Design system](../DESIGN.md) 記錄 PicLens 的視覺 token、字型與 UI 規則。
- [Runtime contract](runtime-contract.md) 記錄 desktop runtime 必須保留的行為契約。
- [Testing](testing.md) 列出驗證命令與目前的 test coverage。
- [Portable release](portable-release.md) 說明如何建置免安裝輸出資料夾。
- [Installer release](installer-release.md) 說明如何建置 Windows MSI 與目前已實作的 Fedora RPM 安裝檔；主流 Linux 支援不以 Fedora/RPM 作為最終完整範圍。
- [Qt Quick PoC](../poc/qtquick/README.md) 說明用於遷移評估的獨立 Qt 6 / Qt Quick 圖片瀏覽器原型、建置方式與通過條件。
- [Qt migration](qt-migration.md) 追蹤正式 Qt 6 production tree、功能切片 parity 與 legacy removal gates。
- [Qt parity audit](qt-parity-audit.md) 從 legacy product surface 反向對照 Qt owner、tests 與剩餘 evidence。
- [Qt redistribution inventory](qt-licensing.md) 記錄 Qt modules、dynamic linking、第三方 runtime/font notices 與 distribution blockers。
- [Avalonia to Qt data continuity](data-migration.md) 定義 settings/log/cache 路徑、雙向 schema compatibility、rollback 與 installer preservation gates。
- [Qt Release performance evidence](performance.md) 定義 machine-readable regression gate、10,000-image CI fixture 與目前本機量測結果。

## 目前狀態

主 `release` 命令目前產生 Qt migration candidate；`qt/` production tree 已具備 Qt Quick search/grid/list shell、native folder picker、typed library/folder-tree state、bounded thumbnails、selection/file operations、inline viewer、drag/drop rename 與 Windows installer lifecycle，MIT license 與授權後 real-profile 副本 smoke 也已通過。Avalonia Core、Presentation、Infrastructure 與 UI tests 仍作為 rollback baseline，直到 Qt 的遠端 Linux/Windows 與其餘 cutover gates 通過。
