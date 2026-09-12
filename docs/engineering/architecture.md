# 架構

## 已確定的方向

同一個 repo 維護兩套獨立 App。Windows 使用 C#、.NET 與 WPF；Arch Linux 使用 C++20、Qt 6、Qt Quick／QML 與 CMake。兩版後端各自實作，不共用執行時服務。

建置與封裝入口見 [Windows README](../../apps/windows/README.md) 與 [Arch README](../../apps/linux/README.md)。Windows 已完成；剩餘工作見 [Arch TODO](../../TODO.arch.md)。

## 目標目錄

```text
apps/
  windows/
    src/              WPF App、應用流程、產品規則、Windows 服務、解碼 helper
    tests/            規則、整合與 WPF 專用驗證
  linux/
    src/              C++ 產品規則、服務、Qt models、解碼 helper
    qml/              Qt Quick 畫面與控制項
    tests/            規則、整合與 Qt 專用驗證
test-data/            共用案例 manifest、預期結果、小型合法圖片或產生方式
assets/               品牌圖示、字型及授權
packaging/
  windows/            MSI 與 portable ZIP
  arch/               PKGBUILD 與桌面整合
docs/
  windows/            Windows 架構、設計與發布
  linux/              Linux／Arch 架構、設計與發布
  product/            共用產品規格與驗收案例
  engineering/        共用架構、資料與執行時契約
  design/             共用設計原則
  guides/             共用開發、測試與發布原則
  reference/          共用參考資料
TODO.arch.md
```

不為了共用程式碼再抽第三個核心 repo。共用的是規格、案例和資產，兩邊可使用不同類別與資料結構。

## 分層與資料流

兩版都保留這個責任方向：

```text
View → 使用者意圖 → Application → Domain／平台 Services
  ↑                         ↓
UI 執行緒接收結果 ← 有界限的背景工作與 request identity
```

- Domain：排序、搜尋投影、選取順序、範圍 anchor、重新命名計畫、縮放計算；不依賴 UI。
- Application：導覽、Viewer snapshot、確認流程、取消、工作識別與結果整理。
- Services：檔案系統、設定 JSON、紀錄、快取、解碼、轉檔及系統整合。
- View：顯示狀態、焦點、pointer capture、容器生命週期及平台圖片資源。

各層維持明確責任分工。背景結果以 generation、request ID 和檔案識別檢查有效性。

## 平台實作

- [Windows／WPF 架構](../windows/architecture.md)
- [Linux／Arch Qt 架構](../linux/architecture.md)

## 不新增的範圍

保持本機資料夾掃描、記憶體投影與 JSON 設定。SQLite、持續檔案監看、雲端、影片、動畫播放、相簿與標籤均不在目前功能範圍。未來需要時再更新產品規格。

.NET SDK、Qt 小版本、codec 套件、測試套件與最低 Windows 版本，於各平台第一階段記錄選擇、相依鎖定及最小驗證結果。這些工程選擇不能改變既有功能規格。
