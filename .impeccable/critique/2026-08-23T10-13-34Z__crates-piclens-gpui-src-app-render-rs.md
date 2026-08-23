---
target: PicLens 主工作區
total_score: 25
max_score: 40
na_heuristics: 
p0_count: 0
p1_count: 3
timestamp: 2026-08-23T10-13-34Z
slug: crates-piclens-gpui-src-app-render-rs
---
Method: dual-agent (A: `/root/critique_design` · B: `/root/critique_detector`)

## Design Health Score

| # | Heuristic | Score | Key issue |
|---|---|---:|---|
| 1 | 系統狀態可見性 | 3 | 固定狀態列與 busy 狀態完整，但批次失敗只剩總數。 |
| 2 | 符合真實世界 | 3 | 資料夾與回收筒語彙自然；「依目標重新命名」需要解碼。 |
| 3 | 使用者控制與自由 | 3 | Escape、焦點還原與歷史導覽良好；app 內沒有 undo。 |
| 4 | 一致性與標準 | 3 | 元件與語彙一致；排序以單一按鈕循環四態，不易直接選擇。 |
| 5 | 錯誤預防 | 2 | 衝突不覆寫、拖放有預覽；多選回收筒仍可直接執行。 |
| 6 | 辨識優於回憶 | 2 | 主要控制可見，但快捷鍵、多選規則與排序循環依賴記憶。 |
| 7 | 彈性與效率 | 3 | 鍵盤、批次、右鍵與雙模式齊全；快捷鍵難發現。 |
| 8 | 美感與極簡 | 3 | 圖片是主角；右上控制區與選取動作會形成同權重噪音。 |
| 9 | 錯誤辨識與復原 | 2 | 訊息清楚，但沒有失敗檔名、原因、重試或結果明細。 |
| 10 | 說明與文件 | 1 | 只有 tooltip 與零散提示，沒有工作流程或快捷鍵說明入口。 |
| **總分** | | **25/40** | **Acceptable；基礎可用，成熟度不足。** |

## Design specificity verdict

**設計審查：結構專屬 PicLens，視覺語言仍可互換。** 資料夾樹、圖庫、選取、批次整理與內嵌檢視器形成清楚的圖片管理工作模型；但淺灰表面、藍色 accent、圓角 outline button 與檔案管理器版型，仍像一套可套到任何檔案工具的通用皮膚。現在的產品個性主要由照片內容與功能組合提供，不是介面本身。

**自動偵測：0 findings，但這是 false-clean。** `detect.mjs` 不掃描 `.rs`，所以 `[]` 不能視為 GPUI UI 通過檢查。原生 UIA 反而找到 6 個未命名按鈕；其中頂列 icon-only 控制與底部縮圖控制都受影響。

**Visual overlay：不適用。** GPUI 是原生視窗，沒有 DOM 或可注入的網頁路由，因此沒有 `[Human]` browser overlay。兩次原生檢查分別看到已載入 44 項的實際圖庫，以及隔離 profile 的 0 項空狀態。

## Overall impression

這是一個冷靜、實用、圖片優先的工作區。最大的機會不是增加裝飾，而是把「整理得安心」做成清楚、可存取、可復原的體驗；目前瀏覽比檔案操作成熟。

## What’s working

- 主架構正確：command bar、folder tree、library、status bar 與 viewer layer 各自有明確責任。
- 圖片確實主導畫面；大縮圖、低彩度 chrome、深色 viewer 都讓內容先被看見。
- 專業效率扎實：搜尋、方向鍵、Home/End、F2、Delete、批次操作與 viewer 快捷鍵都有實作。

## Priority issues

1. **[P1] 批次失敗沒有可操作的逐項結果**
   - **Why it matters：** 使用者只知道「失敗 37」，不知道是哪 37 個、為何失敗、下一步做什麼。
   - **Fix：** 保留摘要，但提供可展開的結果面板，列出檔名、結果、原因與「在檔案管理器顯示」；混合結果不要只用一行狀態取代。
   - **Suggested command：** `/impeccable harden`

2. **[P1] 可及性語意與視覺狀態不足**
   - **Why it matters：** 原生 UIA 有 6 個未命名按鈕；多處小型 muted text 對比偏低，tile 選取主要靠藍色邊框與底色。
   - **Fix：** 為所有 icon-only button 補可及名稱；加入獨立 focus ring 與非色彩選取標記；提高 `text_xs` 的文字色對比，並把 high-contrast palette 接到 OS 偏好。
   - **Suggested command：** `/impeccable audit`

3. **[P1] 檔案操作的範圍與風險沒有被清楚表達**
   - **Why it matters：** 「目前結果」批次操作與「目前選取」操作同時存在；多選回收筒可直接執行。使用者很容易誤判作用範圍。
   - **Fix：** 明確標示「對 44 個目前結果」或「對 3 個選取項目」；多選回收筒加入含數量的確認，單張可維持快速路徑或提供 app 內 undo。
   - **Suggested command：** `/impeccable clarify`

4. **[P2] Header 的控制密度與恢復路徑失衡**
   - **Why it matters：** 瀏覽設定、選取狀態與檔案操作集中在右上角；搜尋無結果時，文字叫使用者清除搜尋，主 CTA 卻叫他開啟另一個資料夾。
   - **Fix：** 把瀏覽設定與選取動作分層；排序改為可直接選擇的 menu；搜尋空狀態主 CTA 改成「清除搜尋」，次要動作才是切換子資料夾或開啟資料夾。
   - **Suggested command：** `/impeccable layout`

5. **[P2] 最小視窗與固定寬度 dialog 不相容**
   - **Why it matters：** 視窗最小寬度 480 px，但批次重新命名 dialog 固定 520 px，會在已承諾的最小尺寸裁切或貼邊。
   - **Fix：** 使用 `min(520px, available width - safe margin)` 的原生等價布局，並在 480×320 驗證 rename、drop preview 與 viewer。
   - **Suggested command：** `/impeccable adapt`

## Persona red flags

- **Alex（高效率使用者）：** 快捷鍵多，但畫面沒有捷徑提示；排序需循環四態；「批次操作」作用於目前結果，容易被誤讀成作用於 selection。
- **Jordan（初次使用者）：** 「最後一張為目標」在建立選取順序後才被解釋；搜尋無結果的文字與 CTA 衝突；多選方式缺少就地提示。
- **Sam（依賴可及性）：** UIA 出現未命名按鈕；gallery tile 未呈現明確逐項 focus 語意；小型 muted text、純色選取與未接 OS 的 high-contrast palette 形成連續障礙。

## Minor observations

- 110–180 ms 的淡入與小位移很克制，適合圖片工作區。
- 底部狀態列同時放狀態、縮圖尺寸、項目數、選取數與快捷鍵，資訊價值高，但目前視覺權重太平均。
- 資料夾卡片與圖片卡片使用相同面積，會讓資料夾在高密度圖庫中佔用過多首屏空間。
- 目前界面很乾淨，但「乾淨」尚未轉化為強烈的 PicLens 品牌記憶。

## Questions to consider

- PicLens 的核心承諾要更偏「看得快」，還是「整理得安心」？
- 「目前結果」與「目前選取」是否應成為兩個視覺上不可混淆的操作範圍？
- 如果一次處理 5,000 張圖片並失敗 37 張，一行計數是否能算工作完成？
