# Linux／Arch Qt 架構

共用分層、資料流與責任邊界見[共用架構](../engineering/architecture.md)。

- Qt Quick／QML 負責畫面；C++ 的 QAbstractListModel／樹狀 model 暴露資料。QML 不掃描磁碟，也不執行檔案操作。
- 圖庫使用 GridView，設定有界限的 cacheBuffer 並驗證 reuseItems；pooled／reused 時清除舊選取顯示、圖片與工作訂閱。詳見 [GridView 文件](https://doc.qt.io/qt-6/qml-qtquick-gridview.html)。
- 一般工作由有界限的背景執行器處理；codec 放進 QProcess 管理的可終止 helper。QObject model 更新回到所屬 UI 執行緒。
- 以 QImageReader 和必要圖片外掛解碼；實測格式、動畫辨識、縮小解碼能力與資源限制。[QImageReader 文件](https://doc.qt.io/qt-6/qimagereader.html)是 API 參考。
- 無損 WebP 編碼器在第一階段驗證；不能只以 quality 數值推定無損。
- 原生資料夾選擇器、檔案管理器、回收筒、Wayland／X11、輸入法及輔助工具由 Linux 層負責。
- Linux 目標是 Arch x86_64。KDE Plasma／Wayland 作為首要驗證環境，X11 另驗證；Qt Quick 不等於 Arch 有專屬控制項。
