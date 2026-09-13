# Windows 封裝與發布

共用授權、發布與驗證原則見[發布指南](../guides/release.md)。

## 版本與產物

版本以 [Directory.Build.props](../../apps/windows/Directory.Build.props) 的 `Version` 為準；發布 tag 為 `windows/v<version>`。封裝目標為 self-contained x64，輸出 MSI、portable ZIP 與各自 SHA-256。`<version>` 請替換為該次發布的實際值。

在 repo 根目錄執行 `./packaging/windows/build.ps1`。建置與執行指令見 [Windows README](../../apps/windows/README.md)。

Windows MSI 保留 UpgradeCode `{4B3899A4-2E9E-4B4F-9CF5-36F8D8D6767D}`。新安裝可選目前使用者或所有使用者，預設為目前使用者；既有 MSI 是所有使用者安裝，升級時必須沿用該範圍。新 Windows 安裝版本需可從既有版本升級；WiX UpgradeCode、產品識別及版本排序在封裝階段檢查，不能因新框架就從不相容的安裝版本重新開始。

## GitHub Actions

[windows-native.yml](../../.github/workflows/windows-native.yml) 只由 `windows/v*` tag 觸發；單一 job 核對 annotated tag 與版本、建置 MSI／ZIP、發布 GitHub Release。PR／main 推送不觸發，不自動執行功能、桌面或安裝測試。

## 套件與手動驗證

- 使用 WPF Release 輸出與必要解碼 helper。
- 選定 .NET self-contained 或 framework-dependent，文件說明離線機器的需求；portable 名稱不能掩蓋缺少 runtime。
- 包含圖示、字型及必要 codec、第三方授權與 SHA-256。
- 驗證兩種安裝範圍的程式路徑、開始功能表捷徑、捷徑元件標記、安裝範圍標記、工作列與執行檔圖示，以及無 console 的正常啟動。
- MSI 分別驗證目前使用者及所有使用者的乾淨安裝、修復、解除安裝與 profile 保留；舊版升級／替換使用所有使用者範圍，ZIP 另外驗證解壓啟動。

Windows 生命週期腳本是 `packaging/windows/test-lifecycle.ps1`。須傳入同版的 `-MsiPath`、`-ZipPath` 與 `-InstallScope perUser|perMachine`，並在乾淨且已授權的 Windows 環境加上 `-ConfirmSystemChanges`。腳本會驗證對應範圍的安裝位置、捷徑、兩種登錄標記、啟動、修復、解除安裝、profile 保留及 ZIP 解壓啟動；`-PreviousMsiPath` 只搭配 `perMachine`，可再加入舊版升級測試。未提供舊 MSI 時，升級結果會明確記為 `not-tested`。此腳本保留供手動驗證，不由 Windows 發布 workflow 自動執行。
