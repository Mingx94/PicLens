# PicLens 文件

PicLens 是原生 WinUI 3 / MVVM 圖片整理與檢視 app。

## 文件

- [產品規格](product-spec.md) 定義不含使用框架與工程實作細節的產品行為、使用者流程與品質要求。
- [Architecture](architecture.md) 說明 solution layout、責任邊界，以及目前的原生 shell。
- [Native behavior](native-behavior.md) 追蹤原生 app 目前支援與應保留的行為。
- [Testing](testing.md) 列出驗證命令與目前的 test coverage。
- [Portable release](portable-release.md) 說明如何建置免安裝輸出資料夾。

## 目前狀態

目前的原生 milestone 是一個整合完成的 WinUI 3 app，包含 MVVM view models、明確的資料夾選取、上一個資料夾還原、資料夾掃描、真實縮圖載入、contextual selected-image actions、次要圖片檢視視窗、繁體中文（台灣）runtime copy，以及保守的檔案操作。Core、Application、Infrastructure 與 ViewModel 行為由聚焦的 xUnit projects 覆蓋。
