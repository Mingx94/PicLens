# A-006 解除安裝證據

- 使用者要求：`godev`／`我沒辦法解除安裝`。本輪只修正維護範圍並移除現有安裝。

## 原因

- 產品 `{8379A5C1-AC1F-4A14-B9F1-85D3505B2377}`，4.0.2；Windows Installer `AssignmentType=0`，程式在 `%LOCALAPPDATA%\Programs\PicLens`。

- `artifacts/A-006-uninstall-before.log`：Windows Installer 設定 `MSIINTERNALINSTALLEDPERUSER=1` 並刪除 `ALLUSERS=2`。AppSearch 後 `UseExistingPerUserScope` 又設回 `ALLUSERS=2`，最後 error 1925、exit 1603。

## 現有安裝復原

- 暫時刪除單一 HKCU installScope 標記的兩次嘗試均 exit 1603；各自還原原本 DWord 1。MSI 仍讀到標記，未確認其原因，不作為有效復原方法。

- 管理員執行原 MSI `/x` 雖 exit 0，實際產品狀態仍為 5，檔案與捷徑仍在。因 ALLUSERS 被改寫，記錄呈現錯誤的 Assignment=1，沒有接受這次成功訊息。

- 備份快取 MSI，僅在副本的兩個 sequence、五個舊版範圍動作條件加入 `NOT Installed AND (...)`，更新 PackageCode，保留 ProductCode、版本、payload 與其他資料表。透過正式 `/fv` 更新快取，沒有直接編輯系統快取。

- 第一次 `/fv` 因來源檔名不符而在 SecureRepair 失敗。副本改用原檔名並完成 UAC 後，`artifacts/A-006-recache-final.log` exit 0。

- 隨後以原使用者身分執行 `/x {8379A5C1-AC1F-4A14-B9F1-85D3505B2377} /qn /norestart`：exit 0。`artifacts/A-006-uninstall-final.log` 顯示五個範圍動作均略過。

- 最終實測：`ProductState=-1`；PicLens.exe、開始功能表捷徑與解除安裝登錄項目均不存在；既有 `%LOCALAPPDATA%\PicLens\piclens-settings.json` 的 SHA-256 前後相同。沒有重新安裝 PicLens。

- 原始 MSI：`artifacts/A-006-original.msi`，SHA-256 `1D379196E2557F5EF632200554CA49C5DBD756EF22ECE1D06BEFC8F3BE50623F`。復原副本：`artifacts/A-006-recovery/PicLens-4.0.2-windows-x86_64.msi`，SHA-256 `DAAD36468210661DB9C2D4E20F6B1305E5AAF8850FDEADAFC3A26299BAA9196C`。

## 修正驗證

- `Package.wxs` 三個範圍恢復動作加入 `NOT Installed`。只在新產品安裝／升級時恢復範圍；修復與解除安裝保留 Windows Installer 載入的範圍。

- `packaging/windows/test-authoring.ps1` 修改預期條件後、來源修改前失敗；修改來源後通過。另補 MSIINSTALLPERUSER 動作條件檢查。

- WiX 6.0.2 Release Rebuild 成功，0 warnings/errors；沿用現有 payload `dist/wpf-payload-0dd910e6acee4791953a525cd1bc8006`，沒有改 WPF 執行程式。初次沙箱建置無法讀取使用者 NuGet.Config，改用使用者身分後成功。

- 新 MSI：`dist/A-006-fixed/PicLens-4.0.2-windows-x86_64.msi`，SHA-256 `7E3268AB69C997FFE0B08EBADA3F34D81901C0B2F19419AEC197886BFAE2EB31`。

- 限制：實機證據為修復舊快取後解除個人安裝。新建置 MSI 沒有重新安裝；未主張完整新安裝、升級或所有使用者生命週期通過。未發布新版。
