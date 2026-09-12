# Windows／WPF 架構

共用分層、資料流與責任邊界見[共用架構](../engineering/architecture.md)。

- WPF／XAML 配合 MVVM；UI 狀態與命令由 ViewModel 管理，背景結果透過 Dispatcher 更新。
- 圖庫必須具備換行格狀虛擬化與容器回收。先驗證自製或既有 VirtualizingPanel，再決定套件；不能把一般 WrapPanel 當成虛擬化完成。
- 掃描與一般 I/O 使用非同步工作、CancellationToken 及有界限佇列。解碼 helper 使用可終止子程序，避免不可取消的 codec 卡住工作槽。
- 先評估 WIC／WPF 圖片能力與必要 codec，實測全部規定格式及無損 WebP 輸出；不能假設使用者已安裝 WebP codec。
- Shell 縮圖只能作為符合規格的替代來源，不能取代完整原圖解碼或保證格式支援。
- 系統回收筒、檔案總管選取檔案、原生資料夾選擇器、DPI、UI Automation 由 Windows 層負責。
- Windows App SDK、WinUI 與跨平台 UI 框架不是本次相依前提。

WPF 的 UI 虛擬化與資料虛擬化不同，容器狀態必須隨資料正確重設。選型時依[官方效能文件](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-controls)驗證。
