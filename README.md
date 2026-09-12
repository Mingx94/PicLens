# PicLens

PicLens 是以本機資料夾為中心的圖片瀏覽與整理工具。採同一個 repo、兩套獨立原生實作：

| 平台 | 技術 | 狀態與入口 |
|---|---|---|
| Windows | C#、.NET、WPF、XAML | 已完成；見 [Windows 文件](docs/windows/README.md) |
| Arch Linux | C++20、Qt 6、Qt Quick／QML、CMake | [TODO.arch.md](TODO.arch.md) |

兩版共用產品規格、驗收案例與品牌資產；各自實作 UI、掃描、縮圖、快取與檔案操作。兩版不共用執行時核心或跨語言橋接。

## 目前狀態

Windows WPF 已完成實作與平台驗收；Arch Qt 提供建置、測試與封裝入口，未完成項目見 Arch TODO。

發布流程分別使用 `windows/v<version>` 與 `arch/v<version>`。來源版本分別見 Windows 的 [Directory.Build.props](apps/windows/Directory.Build.props) 與 Arch 的 [CMakeLists.txt](apps/linux/CMakeLists.txt)。

Windows 的桌面驗收狀態見[平台文件](docs/windows/README.md)；Arch 的狀態見 [TODO.arch.md](TODO.arch.md)。

## 功能範圍

保留格狀圖庫、資料夾樹、遞迴瀏覽、搜尋與排序、多選、縮圖、內嵌原圖檢視器、格式轉換、回收筒及拖放重新命名。動畫 GIF／WebP 顯示不支援預覽提示，不新增動畫播放。

詳細行為以[產品規格](docs/product/product-spec.md)為準。Arch 是本次 Linux 發行目標；Ubuntu、Fedora、DEB 與 RPM 不列入此次交付。

## 從哪裡開始

1. 閱讀[文件索引](docs/README.md)及[共用架構](docs/engineering/architecture.md)，再進入 [Windows 文件](docs/windows/README.md)或 [Linux 文件](docs/linux/README.md)。
2. 進入對應平台文件；Arch 工作再從 TODO 第一個未完成且前置條件已滿足的項目開始。
3. 依[驗收對照](docs/product/acceptance.md)與[測試指南](docs/guides/testing.md)留下證據，完成後移出 TODO。

Windows 建置、執行與診斷指令見 [apps/windows/README.md](apps/windows/README.md)。候選套件由 `packaging/windows/build.ps1` 產生；套件未簽署，尚未公開發布。

Arch 建置、執行與 Windows 預先開發說明見 [apps/linux/README.md](apps/linux/README.md)。來源快照與 PKGBUILD 交付方式見 [packaging/arch/README.md](packaging/arch/README.md)。
