# Windows 待辦

本檔只列尚未完成的實作與驗收，保留原有案例編號以便對照規格。平台說明見[平台文件](docs/windows/README.md)。

依[產品規格](docs/product/product-spec.md)、[驗收對照](docs/product/acceptance.md)及[執行時不變條件](docs/engineering/runtime-invariants.md)執行。實作完成但缺少對應平台證據的項目仍列為待辦；完成並驗證後移出本檔。

## W7 — 視覺、輸入與 Windows 整合

- [ ] W7.3 在互動桌面以實際 IME 與輔助工具驗證輸入組字及 UI Automation 回報；自動化鍵盤、焦點、dialog 與名稱／角色檢查已完成。
- [ ] W7.4 在實際 Windows 高對比及 150%／200% DPI 檢查畫面與原圖比例；100% DPI 的淺深色實機截圖已完成。
- [ ] W7.5 在互動桌面驗證原生 picker、檔案總管 reveal、工作列與 exe 圖示；失敗保留狀態、正常關閉及 `--screenshot` 已完成。

