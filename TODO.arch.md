# Arch Linux 待辦

本檔只列尚未完成的實作與驗收，並保留原有案例編號。已完成項目與證據見 [Arch 驗證紀錄](docs/linux/arch-validation.md)。

依[產品規格](docs/product/product-spec.md)、[驗收對照](docs/product/acceptance.md)及[執行時不變條件](docs/engineering/runtime-invariants.md)執行。實作完成但缺少指定平台或素材證據的項目仍列為待辦。

## A2 — 設定、掃描與資料夾導覽

- [ ] A2.2 在原生 folder picker／桌面對話框驗證選取、取消、啟動還原與空狀態；確認只有 picker 更新持久化路徑及樹 root。

## A7 — 視覺、輸入與 Arch 桌面整合

- [ ] A7.3 在 KDE Plasma／Wayland 驗證鍵盤、繁中輸入法、焦點、pointer grab／取消、選單、dialog、原生 picker、reveal 與回收筒。
- [ ] A7.4 另在原生 X11 工作階段驗證啟動、輸入、DPI 與檔案操作整合；記錄桌面、Qt plugin 與依賴版本。
- [ ] A7.5 完成 200% 縮放、完整 Qt accessibility 名稱／角色／狀態及可用原生輔助工具的驗證。100%／150% 與明暗模式已有證據。

## A8 — 效能、整合與 CI

- [ ] A8.2 代表性混合圖庫量測 Release 冷暖快取，核對完整原圖 500ms 目標、超標與未完成樣本，不使用圖片載入 callback 代替繪製。
