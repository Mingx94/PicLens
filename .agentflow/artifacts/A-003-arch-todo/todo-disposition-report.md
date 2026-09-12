* _2026-09-13 00:00:18 (gpt-5.6-sol/low)_

# Arch TODO disposition

基準 commit：`59582516b738a8c9279a2b6a3efdf7441d8b4378`。本次只整理 TODO 與持久驗證文件。沒有修改原始碼、測試或設定，也沒有 commit、push、發布或外部訊息。

## 已移除

- A0.3、A0.4、A0.5、A0.6：共用案例與平台差異、README、CLI／log／隔離啟動、CTest／QML 入口及原生 Wayland smoke 均有證據。
- A1.1、A1.3、A1.4、A1.5：格式、10,000 GridView、scene graph／tile 所有權及 helper 清理均已驗證。
- A2.1、A2.3、A2.6：設定延續、掃描／取消／symlink、queued delivery 與 stale result 拒收已有測試。
- A3.1、A3.2、A3.3、A3.5、A3.6：正式 model、圖庫資訊、搜尋、右鍵 scope 與 delegate reuse 已由 QML journey 驗證。
- A4.2、A4.3、A4.4、A4.5、A4.6：排程、去重、identity、cache／prune、混合 stalled／good 工作及清理均有證據。
- A5.1、A5.2、A5.3、A5.4、A5.5、A5.6：Viewer snapshot、焦點、縮放／anchor、fallback、tile、預載與資源釋放均有原生 probe。
- A6.1、A6.2、A6.3、A6.4、A6.5、A6.8、A6.9、A6.10：保守計畫、轉換、清理、改名、實際 trash、序號、逐項結果與 shutdown 均有隔離案例。規格不要求另加 JPG quality 100 的 bit-level metadata 證明。
- A7.1、A7.6：元件展示、繁中、明暗畫面、screenshot、真實 renderer、desktop metadata／圖示及正常關閉已有證據。
- A9.6：使用說明、實際套件相依、動態相依及隨附授權檔已核對。未獲授權的 tag／hosted release／AUR 不是無條件動作。

## 保留

- A2.2：缺原生 picker／桌面對話框操作證據。
- A2.5：缺完整樹展開／按需載入、歷史、側鍵與 refresh runtime 證據。
- A6.6：缺原生檔案管理員 Reveal 證據。
- A6.7：缺實際 native DropArea drop 與 compositor capture-lost。
- A7.2：compositor 未提供要求的 1600×1000 實際 geometry，完整 DPI 比例亦未證實。
- A7.3：缺 KDE Plasma／Wayland、繁中 IME、picker 與完整桌面整合。
- A7.4：缺原生 X11 工作階段。
- A7.5：缺完整 DPI 與原生 accessibility 工具證據。
- A8.1：additive diagnostics 尚未完成，不能先行關閉。
- A8.2：synthetic 兩張圖片不是代表性 Release 混合圖庫。
- A8.3：最終 Release 10,000 項 CPU、RSS／峰值與 shutdown 尚未執行。
- A8.4：仍有 picker／Reveal、樹／側鍵、native drop、KDE／IME、X11、DPI／accessibility 互動缺口。
- A9.3：一般主機 makepkg 不是乾淨 Arch，也未證明無額外 plugin／cache。
- A9.4：未執行安裝、升級、解除安裝與 profile lifecycle。
- A10.3：未在只有必要工具鏈的乾淨 OS 完成建置、測試與封裝。

## 文件與證據

- 持久摘要：`docs/linux/arch-validation.md`。
- 執行方式與 metrics schema：`apps/linux/README.md`。
- 相依與授權：`apps/linux/THIRD-PARTY.md`。
- 主機證據：`/home/michael/Work/PicLens/.agentflow/artifacts/A-003-arch-todo/evidence/`。本文只引用保存的觀察，不把初始 baseline 結果誤作修訂後結果。

限制仍包括 KDE、原生 X11、IME、accessibility、完整 DPI、乾淨 OS、安裝生命週期與代表性效能。clone 不是 OS confinement；provider／網路憑證限制仍存在。

Self-check: Remove only proven TODO work and preserve missing native environments and evidence.
