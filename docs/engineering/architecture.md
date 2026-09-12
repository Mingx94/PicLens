# 架構

## 已確定的方向

同一個 repo 維護兩套獨立 App。Windows 使用 C#、.NET 與 WPF；Arch Linux 使用 C++20、Qt 6、Qt Quick／QML 與 CMake。兩版均重寫後端，不保留 Rust、egui、FFI 或共用執行時服務。

Windows 專案與封裝已建立，模組細節見 [Windows README](../../apps/windows/README.md)。Arch Qt 實作與獨立 CMake 建置已建立，見 [Arch README](../../apps/linux/README.md)；Arch 桌面驗收仍待執行。現有 Rust 程式暫留作對照。工作順序見 [Windows TODO](../../TODO.win.md) 與 [Arch TODO](../../TODO.arch.md)。

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
docs/                 共用規格與平台工程說明
TODO.win.md
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

保留責任分離，不要求照抄 Rust 的 reducer 類別。背景結果以 generation、request ID 和檔案識別檢查有效性。

## Windows

- WPF／XAML 配合 MVVM；UI 狀態與命令由 ViewModel 管理，背景結果透過 Dispatcher 更新。
- 圖庫必須具備換行格狀虛擬化與容器回收。先驗證自製或既有 VirtualizingPanel，再決定套件；不能把一般 WrapPanel 當成虛擬化完成。
- 掃描與一般 I/O 使用非同步工作、CancellationToken 及有界限佇列。解碼 helper 使用可終止子程序，避免不可取消的 codec 卡住工作槽。
- 先評估 WIC／WPF 圖片能力與必要 codec，實測全部規定格式及無損 WebP 輸出；不能假設使用者已安裝 WebP codec。
- Shell 縮圖只能作為符合規格的替代來源，不能取代完整原圖解碼或保證格式支援。
- 系統回收筒、檔案總管選取檔案、原生資料夾選擇器、DPI、UI Automation 由 Windows 層負責。
- Windows App SDK、WinUI 與跨平台 UI 框架不是本次相依前提。

WPF 的 UI 虛擬化與資料虛擬化不同，容器狀態必須隨資料正確重設。選型時依[官方效能文件](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-controls)驗證。

## Arch Linux

- Qt Quick／QML 負責畫面；C++ 的 QAbstractListModel／樹狀 model 暴露資料。QML 不掃描磁碟，也不執行檔案操作。
- 圖庫使用 GridView，設定有界限的 cacheBuffer 並驗證 reuseItems；pooled／reused 時清除舊選取顯示、圖片與工作訂閱。詳見 [GridView 文件](https://doc.qt.io/qt-6/qml-qtquick-gridview.html)。
- 一般工作由有界限的背景執行器處理；codec 放進 QProcess 管理的可終止 helper。QObject model 更新回到所屬 UI 執行緒。
- 以 QImageReader 和必要圖片外掛解碼；實測格式、動畫辨識、縮小解碼能力與資源限制。[QImageReader 文件](https://doc.qt.io/qt-6/qimagereader.html)是 API 參考。
- 無損 WebP 編碼器在第一階段驗證；不能只以 quality 數值推定無損。
- 原生資料夾選擇器、檔案管理器、回收筒、Wayland／X11、輸入法及輔助工具由 Linux 層負責。
- Linux 目標是 Arch x86_64。KDE Plasma／Wayland 作為首要驗證環境，X11 另驗證；Qt Quick 不等於 Arch 有專屬控制項。

## 不新增的範圍

保持本機資料夾掃描、記憶體投影與 JSON 設定。SQLite、持續檔案監看、雲端、影片、動畫播放、相簿與標籤都不是重寫前提。未來需要時再更新產品規格。

.NET SDK、Qt 小版本、codec 套件、測試套件與最低 Windows 版本，於各平台第一階段記錄選擇、相依鎖定及最小驗證結果。這些工程選擇不能改變既有功能規格。
