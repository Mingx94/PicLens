# 測試與驗收

## 目前狀態

Windows 已建立 xUnit、codec／服務整合測試及 STA／Dispatcher 入口，命令見 [Windows README](../../apps/windows/README.md)，結果與未驗證事項見 [Windows 驗證紀錄](../engineering/windows-validation.md)。Arch 仍需建立 CTest／Qt Test 與必要的 Qt Quick 測試。

舊 Cargo tests、egui 測試、舊 MSI 與舊效能腳本只驗證舊版。

## 依風險選擇驗證

| 變更 | 最小有效驗證 |
|---|---|
| 文件 | diff、連結、規格與 TODO 對照 |
| 排序、命名計畫、設定轉換 | 純規則案例與錯誤邊界 |
| 掃描、快取、取消、檔案操作 | 隔離檔案整合測試及失敗注入 |
| WPF／Qt model 與互動 | 平台測試及受影響的真實視窗流程 |
| 外觀、DPI、圖片品質 | 原生 renderer 截圖／人工檢查 |
| 封裝與升級 | 乾淨系統的安裝、啟動、升級、解除安裝及 profile 保留 |

不為小型可逆修改新增只重複實作的測試。先做最小驗證，有失敗或不確定性才擴大。編譯、啟動、互動、像素、效能、輔助工具與安裝生命週期分開報告。

## 共用資料

[驗收對照](../product/acceptance.md)為兩版指定相同案例 ID。先實作的平台建立 `test-data/`，至少包含：

- fixture manifest：案例 ID、相對路徑、內容 hash、圖片格式／尺寸與預期結果。
- 純資料案例：自然排序、同名衝突、選取順序、範圍 anchor、重新命名序號、舊設定 JSON。
- 小型合法圖片或可重現產生方式：六種副檔名、靜態／動畫 GIF 和 WebP、透明 PNG、損壞圖片、超限案例。
- 分層資料夾、Unicode／空白名稱、大小寫差異及 OS 特有路徑限制。
- 大型測試集產生方式；不提交使用者圖庫或大量重複二進位檔。

預期結果必須由規格／可查證的舊規則推導，不能把某次執行的輸出直接奉為正確。OS 差異以同案例的 Windows／Arch 預期欄位表達。

## 隔離

設定 `PICLENS_DATA_ROOT` 或 `--data-root` 隔離設定、縮圖與紀錄。所有會轉檔、重新命名、回收的素材另用可丟棄副本；覆寫 data root 並不隔離來源圖片。

系統安裝、解除安裝、顯示設定變更與個人 profile 存取需要對應授權。Computer Use 僅在使用者明確要求時使用；其他情況採程式內診斷／截圖或人工驗收，缺少證據的項目保持待驗證。

## 新版診斷入口

兩版 TODO 都需實作 `--folder`、`--data-root`、`--smoke-ms`、`--viewer`、`--metrics` 與 `--screenshot`，供隔離啟動、可見視窗量測與截圖。參數格式、驗證失敗與輸出位置寫入平台 README。

這些參數目前僅存在於舊版或規劃中，不代表新版已能執行。自動 smoke 不得略過檔案操作確認。

## 驗收紀錄

每個 TODO 完成時，在該檔案的證據表填入項目 ID、commit／dirty state、命令／操作、OS、fixture、結果及證據位置。編譯成功不能勾選互動或效能驗收。

證據可保存在 `artifacts/<platform>/<run>/`，但重要結論需摘要在版本控管內，不能只留本機路徑。個人路徑與素材不要公開。兩版都要逐項核對產品規格全文，不只執行少量代表案例。

Windows 本機結果不算 Arch 結果。Linux 容器建置／無頭測試不算 Wayland／X11 真實桌面驗證。簽章、公開發佈與 hosted lifecycle 也各自留證據。
