# Windows 相依與資產

- PicLens：repo 根目錄 MIT LICENSE。
- SkiaSharp 3.119.4：MIT；套件 LICENSE.txt 隨成品附上。
- SkiaSharp.NativeAssets.Win32 3.119.4：Skia 與內含 codec 的聲明見套件 THIRD-PARTY-NOTICES.txt，隨成品附上。
- .NET 10 self-contained runtime：依 runtimeconfig 的實際版本，從 Microsoft.NETCore.App／Microsoft.WindowsDesktop.App runtime 套件複製授權與內含聲明到 licenses。PicLens 授權另命名為根目錄 LICENSE。
- WiX Toolset SDK 6.0.2：僅用於 MSI 建置，不加入 App 執行依賴。
- xUnit、Microsoft.NET.Test.Sdk：僅測試使用，版本見 tests 專案及 packages.lock.json。
- UI 使用系統 Segoe UI／Microsoft JhengHei UI，不散布系統字型。
- 品牌 ICO 沿用 repo assets/AppIcon.ico。應用內圖示使用 Lucide（ISC），來源固定於 b1a94838ac536c1cef5aaa802f78c07b30cac913；SVG 內嵌於 App，授權隨成品附於 licenses/Lucide-LICENSE.txt。

最終封裝應包含 App、worker、Windows x64 codec、.NET runtime 及本清單。程式不使用 Rust。Skia 的解碼用途不取代 WPF 作為 UI／繪圖框架。
