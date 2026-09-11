# 授權與再散布

## 狀態與來源

PicLens 原始碼沿用根目錄 MIT LICENSE。重寫不自動改變第三方套件、codec、字型或圖片的授權。

Windows 已選定 .NET 10、SkiaSharp 3.119.4、WiX 6.0.2 與 xUnit，實際套件鎖定及授權見 [Windows 清單](../../apps/windows/THIRD-PARTY.md)。封裝複製對應 runtime 與 codec 的授權／聲明。Arch 仍待選定，以 CMake 相依、PKGBUILD、Qt 模組與最終套件為準。Cargo.lock 只描述舊版。

## 每版需要的清單

- 原始碼與所有直接、間接依賴的版本、來源和授權。
- Qt 模組、.NET runtime、影像解碼／編碼函式庫及 helper 的散布方式。
- 字型、Lucide 圖示、AppIcon 和測試圖片的來源與必要聲明。
- 最終套件內包含的檔案，以及由作業系統提供的相依。
- 所需的第三方通知、原始碼提供方式或其他散布要求；按實際選型查證。

既有 Noto Sans CJK TC 的聲明位於 `assets/Fonts/NotoSansCJKtc-OFL.txt`。保留或重新打包字型時，檢查該聲明與實際散布內容。選用 Qt 或 codec 前查閱其官方授權及該版本條款，不把 PicLens 的 MIT 套用到第三方程式。

## 驗收

兩份 TODO 的選型階段先記錄候選依賴，封裝階段再比對最終產物。不得只憑開發機可執行就認定套件可散布。尚未完成的依賴與資產審查列為待辦。
