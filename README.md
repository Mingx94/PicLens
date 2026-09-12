# PicLens

PicLens 是以本機資料夾為中心的圖片瀏覽與整理工具。採同一個 repo、兩套獨立原生實作：

| 平台 | 技術 | 實作計畫 |
|---|---|---|
| Windows | C#、.NET、WPF、XAML | [TODO.win.md](TODO.win.md) |
| Arch Linux | C++20、Qt 6、Qt Quick／QML、CMake | [TODO.arch.md](TODO.arch.md) |

兩版共用產品規格、驗收案例與品牌資產；各自實作 UI、掃描、縮圖、快取與檔案操作。目標版本不保留 Rust，也不設共用 Rust 核心或跨語言橋接。

## 目前狀態

Windows WPF 已完成程式實作，提供獨立建置、測試與候選封裝。已驗證功能、效能與仍待人工／安裝環境確認的項目，見 [Windows 驗證紀錄](docs/engineering/windows-validation.md)。Arch Qt 4.0.0 已在 Omarchy 完成本機建置、測試與候選套件；實際 Arch 桌面與套件驗收見 [Arch 驗收紀錄](docs/engineering/arch-validation.md)。

Windows 新版不依賴 Rust。現有 Cargo 與舊 release workflow 只適用歷史版；新流程使用 `windows/v<version>`。

本次先保留舊程式作為行為參考。兩版完成驗收後，再依 TODO 移除 Rust、egui 與舊建置流程。歷史版本與查閱方式見[舊版基準](docs/reference/legacy-baseline.md)。

## 功能範圍

保留格狀圖庫、資料夾樹、遞迴瀏覽、搜尋與排序、多選、縮圖、內嵌原圖檢視器、格式轉換、回收筒及拖放重新命名。動畫 GIF／WebP 顯示不支援預覽提示，不新增動畫播放。

詳細行為以[產品規格](docs/product/product-spec.md)為準。Arch 是本次 Linux 發行目標；Ubuntu、Fedora、DEB 與 RPM 不列入此次重寫交付。

## 從哪裡開始

1. 閱讀[文件索引](docs/README.md)及[架構](docs/engineering/architecture.md)。
2. 選擇對應平台 TODO，從第一個未完成且前置條件已滿足的項目開始。
3. 依[驗收對照](docs/product/acceptance.md)與[測試指南](docs/guides/testing.md)留下證據，再勾選完成。

Windows 建置、執行與診斷指令見 [apps/windows/README.md](apps/windows/README.md)。候選套件由 `packaging/windows/build.ps1` 產生；套件未簽署，尚未公開發布。

Arch 建置、執行與 Windows 預先開發說明見 [apps/linux/README.md](apps/linux/README.md)。來源快照與 PKGBUILD 交付方式見 [packaging/arch/README.md](packaging/arch/README.md)。
