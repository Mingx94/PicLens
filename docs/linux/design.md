# Linux／Arch 視覺與元件

共用操作、版面與可操作性要求見[設計系統](../design/system.md)。

## 視覺方向

採中性 Zinc 灰階。白色或近黑背景、細邊框、低彩度次要操作，讓圖片成為主角。主要操作使用黑白反差，危險操作使用紅色。淺色與深色共用同一組語意角色。

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

檢視器畫布固定為近黑色，控制列使用對應的深色文字與背景配對。

## 尺寸與字型

Linux 設計基準為間距 4、8、12、16、24 邏輯單位；一般控制項高 36、小型控制項高 28、圖示 16。控制項圓角 6，卡片與浮層圓角 10。邊框寬 1，焦點另外繪製 2 點外框。

字型維持 Noto Sans CJK TC。內文與按鈕為 14，小字為 12，標題為 24。Lucide SVG 與原有品牌圖示繼續使用。

## 平台版面

- 側欄預設 208、可調範圍 160～300，可收合。
- 主內容水平邊距 20、精簡版 16；垂直邊距 16。
- 800 邏輯單位以下採精簡配置；工具列可用寬度不足 820 時，搜尋與篩選分列。
