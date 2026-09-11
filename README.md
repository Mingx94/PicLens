# PicLens

PicLens 是以本機資料夾為中心的圖片瀏覽與整理工具。採同一個 repo、兩套獨立原生實作：

| 平台 | 技術 | 實作計畫 |
|---|---|---|
| Windows | C#、.NET、WPF、XAML | [TODO.win.md](TODO.win.md) |
| Arch Linux | C++20、Qt 6、Qt Quick／QML、CMake | [TODO.arch.md](TODO.arch.md) |

兩版共用產品規格、驗收案例與品牌資產；各自實作 UI、掃描、縮圖、快取與檔案操作。目標版本不保留 Rust，也不設共用 Rust 核心或跨語言橋接。

## 目前狀態

2026-09-11 已確定重寫方向，正在文件與計畫階段。repo 內仍是既有 Rust／egui 程式；WPF 與 Qt App 尚未建立，兩份 TODO 的實作項目尚未完成。現有 Cargo、封裝腳本與 release workflow 只適用舊版。

本次先保留舊程式作為行為參考。兩版完成驗收後，再依 TODO 移除 Rust、egui 與舊建置流程。歷史版本與查閱方式見[舊版基準](docs/reference/legacy-baseline.md)。

## 功能範圍

保留格狀圖庫、資料夾樹、遞迴瀏覽、搜尋與排序、多選、縮圖、內嵌原圖檢視器、格式轉換、回收筒及拖放重新命名。動畫 GIF／WebP 顯示不支援預覽提示，不新增動畫播放。

詳細行為以[產品規格](docs/product/product-spec.md)為準。Arch 是本次 Linux 發行目標；Ubuntu、Fedora、DEB 與 RPM 不列入此次重寫交付。

## 從哪裡開始

1. 閱讀[文件索引](docs/README.md)及[架構](docs/engineering/architecture.md)。
2. 選擇對應平台 TODO，從第一個未完成且前置條件已滿足的項目開始。
3. 依[驗收對照](docs/product/acceptance.md)與[測試指南](docs/guides/testing.md)留下證據，再勾選完成。

目前沒有新版可執行的建置指令。第一階段會建立實際專案與命令，再更新[開發指南](docs/guides/development.md)。不要把舊版 Cargo 的成功當成新版驗證。
