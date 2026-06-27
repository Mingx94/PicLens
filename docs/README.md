# PicLens 文件

PicLens 是 Windows-only Avalonia / MVVM 圖片整理與檢視 app。

## 文件

- [產品規格](product-spec.md) 定義不含使用框架與工程實作細節的產品行為、使用者流程與品質要求。
- [Architecture](architecture.md) 說明 solution layout、責任邊界，以及目前的 Avalonia shell。
- [Runtime contract](runtime-contract.md) 記錄 desktop runtime 必須保留的行為契約。
- [Testing](testing.md) 列出驗證命令與目前的 test coverage。
- [Verification notes](verification/2026-06-24.md) 保存舊 milestone 的特定日期驗證結果與 smoke/profile 證據。
- [Portable release](portable-release.md) 說明如何建置免安裝輸出資料夾。

## 目前狀態

目前的原生 milestone 是一個整合完成的 Avalonia app，包含 UI-agnostic ViewModels、明確的資料夾選取、上一個資料夾還原、資料夾掃描、真實縮圖載入、contextual image actions、主視窗內嵌圖片預覽、繁體中文（台灣）runtime copy，以及保守的檔案操作。Core、Presentation、Infrastructure 與 UI smoke behavior 由聚焦的 test projects 覆蓋。
