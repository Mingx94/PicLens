# PicLens 元件庫

PicLens 使用 Rust、egui、eframe 與 wgpu。元件庫參考 [shadcn/ui 的語意色彩](https://ui.shadcn.com/docs/theming)與[按鈕變體](https://ui.shadcn.com/docs/components/button)，以原生 egui 實作。以目前產品需要的元件為範圍，不新增 React、Tailwind 或 WebView。

元件原始碼位於 `crates/piclens-desktop/src/components/`。色彩、字型與尺寸由 `theme.rs` 統一管理。畫面組合與 Action 留在 `ui/mod.rs`；元件只回傳 Response，不執行檔案操作。

## 視覺方向

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

`theme::metrics` 定義間距 4、8、12、16、24 logical points；一般控制項高 36、小型控制項高 28、圖示 16。控制項圓角 6，卡片與浮層圓角 10。邊框寬 1，焦點另外繪製 2 點外框。

字型維持 Noto Sans CJK TC。內文與按鈕為 14，小字為 12，標題為 24。Lucide SVG 與原有品牌圖示繼續使用。

## 元件與使用規則

| 模組 | 元件 | 使用規則 |
|---|---|---|
| button.rs | Button | Default、Secondary、Outline、Ghost、Destructive；Default、Small、Icon 三種尺寸 |
| field.rs | Input | 共用邊框、焦點、提示文字、AccessKit 名稱；搜尋可放入 input group |
| field.rs | checkbox、select、slider | 保留 egui 的鍵盤與輸入行為，套用共用樣式 |
| gallery.rs | GalleryTile | 圖片與資料夾共用卡片；保留截斷、選取、拖曳與固定尺寸 |
| surface.rs | surface_frame | Card、Popover、Dialog、Toolbar、Sidebar 表面 |
| surface.rs | badge | 非互動的項目數或補充狀態 |
| surface.rs | dialog | 共用 Modal 外觀，保留 egui 的背景阻擋與關閉語意 |
| feedback.rs | Toast | 不阻擋操作，回傳詳細／關閉意圖；期限由 AppModel 管理 |
| feedback.rs | alert | 畫面內持續顯示的資訊或錯誤 |

主要操作使用 Default，取消使用 Outline，工具列圖示及選單動作使用 Ghost。Destructive 只用於明確的危險操作。停用按鈕不可觸發動作，所有圖示按鈕必須有提示文字與 AccessKit 名稱。

```rust
use piclens_desktop::components::{Button, ButtonVariant, Input};

let response = Button::new("選擇資料夾")
    .icon(piclens_desktop::theme::Icon::FolderOpen)
    .show(ui);

let cancel = Button::new("取消")
    .variant(ButtonVariant::Outline)
    .show(ui);

Input::new(egui::Id::new("search"), "搜尋圖片", &mut query)
    .hint("搜尋名稱或路徑…")
    .width(240.0)
    .show(ui);
```

## 舊 token 對應

| 原名稱 | 新名稱 |
|---|---|
| app_background、content | background |
| command_surface | card |
| tile | muted |
| primary（原本代表文字） | foreground |
| secondary（原本代表輔助文字） | muted_foreground |
| accent（原本代表強調色） | primary |
| selected | accent |
| danger | destructive |

新元件直接使用新名稱。只為目前有使用的元件建立 token；不要在畫面另建一套色盤。

## 版面與操作

- 每次啟動視窗為 1600×1000，最小 800×600。
- 側欄預設 208，允許 160～300，可收合。
- 主內容水平邊距 20，精簡版 16；垂直邊距 16。
- 800 點以下使用精簡版；工具列可用寬度小於 820 時，搜尋與篩選分列。
- 圖庫維持 `ScrollArea::show_rows` 虛擬化。卡片水平內距 8、正方形預覽、置中裁切，保留原圖。
- 搜尋清除、Ctrl+F 全選、鍵盤選取、拖曳重新命名、檔案確認與結果回報維持既有行為。
- 拖曳重新命名完成後以 toast 顯示結果。一般 6 秒、錯誤 12 秒；逐項結果由使用者主動開啟。
- 資料夾選擇器仍使用系統原生 `rfd::FileDialog`。

## 元件展示與驗證

```powershell
cargo run -p piclens-desktop --example component_gallery
cargo run -p piclens-desktop --example component_gallery -- --dark
cargo run -p piclens-desktop --example component_gallery -- --compact
cargo run -p piclens-desktop --example component_gallery -- --dialog
```

展示頁可操作按鈕、表單、對話框與 toast，不掃描使用者圖庫，也不執行檔案操作。加上 `--screenshot <path.png>` 會擷取視窗後結束；輸出目錄需先存在。

`egui_kittest` 驗證鍵盤、焦點、AccessKit、選取、虛擬化與尺寸。實際顏色、字型和陰影另以原生 renderer 截圖檢查。這些證據不代表原生輔助工具、高對比或所有平台都已驗證。
