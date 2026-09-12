# Windows 視覺與元件

共用操作、版面與可操作性要求見[設計系統](../design/system.md)。

下拉選單（含展開清單）、核取方塊與水平縮圖滑桿的樣式集中於 `Themes/SelectionControls.xaml`，沿用 Card、Line、Accent 與選取色。下拉選單採 7 DIP 圓角；核取方塊支援未勾選、已勾選與部分勾選；滑桿採 4 DIP 軌道與 16 DIP 圓形滑塊。三者提供鍵盤焦點與停用狀態，保留 WPF 原生操作行為。

應用內圖示統一使用 `assets/Icons/Lucide` 的固定版本 SVG，透過 `Controls/LucideIcon.cs` 轉為可快取的 WPF 向量。新增圖示應使用同一套資產，並加入 `IconKind`；不以 Unicode 符號代替操作圖示。工具列用 18 DIP、選單用 16 DIP、縮圖預留圖示用 46 DIP。圖示繼承 Foreground，隨明暗主題與系統高對比色更新；圖示按鈕保留提示與輔助工具名稱。Windows 原生控制項的勾選與展開符號由系統繪製。

主畫面使用暖灰底、白卡片與森林綠主色，Viewer 保持深色畫布。Segoe UI 搭配 Microsoft JhengHei UI 使用 Windows 字型，不額外安裝或封裝字型。樣式與色彩集中在 `apps/windows/src/PicLens.App/App.xaml` 的 Application ResourceDictionary；主題切換由 `App.xaml.cs` 更新語意資源。

| 語意 | 淺色 | 深色 |
|---|---|---|
| Surface | #F6F5F1 | #202726 |
| Card | #FFFFFF | #28312F |
| Ink | #243333 | #ECF0E9 |
| MutedInk | #687471 | #ABB8B2 |
| Line | #DDDFD8 | #424D47 |
| Accent | #245F51 | #9BD1B8 |
| Selected | #E1EDE7 | #354F43 |

側欄寬 220，窄視窗縮為 170，支援收合。工具列依空間換行，縮圖維持固定正方形預覽，不拉寬填滿整列。可用 `--components` 檢查元件。高對比改用 Windows 系統色；實際 150%／200% DPI、高對比與輔助工具驗證已完成，證據見[平台文件](README.md)。

圖庫、清單與資料夾樹的捲軸樣式集中於 `Themes/ScrollControls.xaml`，使用 Surface、MutedInk 與 Accent 主題色，支援垂直／水平翻頁與拖曳。
