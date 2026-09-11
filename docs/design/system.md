# 設計系統

兩版保留操作資訊層級，分別用 WPF／XAML 和 Qt Quick／QML 實作。使用者已允許 Windows 重新設計；Windows 實作使用下節的暖灰／森林綠方向。其餘基準保留供 Arch 使用。

色彩、字型、間距與尺寸集中在各平台的資源系統；Windows 用 ResourceDictionary，Arch 用統一的 QML theme。共用語意角色，不共用控制項程式碼。

## 視覺方向

### Windows WPF 實作

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

側欄寬 220，窄視窗縮為 170，支援收合。工具列依空間換行，縮圖維持固定正方形預覽，不拉寬填滿整列。可用 `--components` 檢查元件。高對比改用 Windows 系統色；實際 150%／200% DPI、高對比與輔助工具驗證仍見 Windows TODO。

### Arch／原有基準

採中性 Zinc 灰階。白色或近黑背景、細邊框、低彩度次要操作，讓圖片成為主角。主要操作使用黑白反差，危險操作使用紅色。淺色、深色與 Windows 高對比共用同一組語意角色。

## 色彩 token

| Token | 淺色 | 深色 | 用途 |
|---|---|---|---|
| background | #FFFFFF | #09090B | 主背景與輸入欄 |
| foreground | #18181B | #FAFAFA | 主要文字 |
| card / popover | #FFFFFF | #18181B | 卡片、選單、對話框、toast |
| sidebar | #FAFAFA | #18181B | 資料夾側欄 |
| muted / accent | #F4F4F5 | #27272A | 次要表面、滑入與選取背景 |
| muted_foreground | #71717A | #A1A1AA | 輔助文字 |
| primary | #18181B | #FAFAFA | 主要按鈕背景 |
| primary_foreground | #FAFAFA | #18181B | 主要按鈕文字與圖示 |
| accent_foreground | #18181B | #FAFAFA | 滑入與選取文字 |
| destructive | #B91C1C | #FCA5A5 | 危險操作與錯誤 |
| destructive_foreground | #FFFFFF | #450A0A | 危險按鈕文字 |
| border | #E4E4E7 | #3F3F46 | 邊框與分隔線 |
| input | #D4D4D8 | #52525B | 輸入欄邊框 |
| ring | #71717A | #A1A1AA | 鍵盤焦點外框 |

檢視器畫布固定為近黑色，控制列使用對應的深色文字與背景配對。Windows 高對比模式使用系統色，包含選取背景與選取文字，不以固定黑白色蓋過使用者設定。

## 共用尺寸

共用設計基準為間距 4、8、12、16、24 邏輯單位；一般控制項高 36、小型控制項高 28、圖示 16。控制項圓角 6，卡片與浮層圓角 10。邊框寬 1，焦點另外繪製 2 點外框。

字型維持 Noto Sans CJK TC。內文與按鈕為 14，小字為 12，標題為 24。Lucide SVG 與原有品牌圖示繼續使用。

## 平台元件責任

| 元件 | 共同行為 | Windows | Arch |
|---|---|---|---|
| 按鈕與輸入 | 主要、次要、取消、危險、停用、焦點 | WPF Style／ControlTemplate | Qt Quick Controls style |
| 圖庫卡片 | 資料夾／圖片、選取、截斷、拖放提示 | 虛擬化 panel 與 DataTemplate | GridView delegate |
| 資料夾樹 | root 不可收合、後代可展開 | 虛擬化 TreeView 或等效控制項 | Qt model 與樹狀 view |
| 選單與對話框 | 清楚作用範圍、取消不改檔、焦點返回 | WPF 選單／dialog | Qt Quick 選單／dialog |
| 結果通知 | toast、詳情入口、主動關閉 | WPF 畫面通知 | QML 畫面通知 |
| 資料夾選擇器 | 使用系統對話框、取消保持狀態 | Windows 原生介面 | Qt 原生 dialog／桌面整合 |

主要操作有明確視覺權重；危險樣式只用於危險操作。選取與錯誤不可只靠顏色，保留外框、圖示或文字。圖示按鈕有提示與輔助工具名稱。

## 版面基準

- 啟動 1600×1000、最小 800×600；視窗尺寸使用平台邏輯單位，超過工作區時需確認可操作性。
- 側欄預設 208、可調範圍 160～300，可收合。
- 主內容水平邊距 20、精簡版 16；垂直邊距 16。
- 800 邏輯單位以下採精簡配置；工具列可用寬度不足 820 時，搜尋與篩選分列。
- 圖庫僅格狀，正方形預覽、置中裁切，不改動原檔；縮圖大小沿用設定契約。
- 圖片與資料夾共用清楚的卡片邊界，圖庫捲軸不遮住卡片內容。
- Viewer 名稱放在主 app bar，畫布保留導覽、縮放控制與圖片。
- 拖放重新命名結果用 toast，一般 6 秒、失敗 12 秒，詳細結果由使用者主動開啟。

## 原生互動與驗證

遵守各平台焦點、鍵盤、輸入法、pointer capture、系統對話框與輔助工具行為。自訂 Style 不可移除可操作性。Windows 高對比使用系統色；Arch 分別檢查桌面主題與 Qt accessibility。

實作時各建一個不讀使用者圖庫、也不修改檔案的元件展示入口。檢查淺色／深色、窄視窗、對話框、toast、長檔名、Unicode 與停用狀態。

原生截圖才能佐證顏色、字型、圖片比例與 DPI。WPF／Qt 無頭測試不能證明像素結果。Computer Use 僅在使用者明確要求時使用；其餘依[測試指南](../guides/testing.md)保留可重現證據。
