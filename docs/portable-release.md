# Portable Release

ImageViewerWin 目前不是為 Microsoft Store 或 MSIX distribution 準備。Release target 是 framework-dependent no-install folder，可複製到已安裝必要 runtimes 的機器上，並透過 `ImageViewerWin.exe` 啟動。

## 建置

從 repository root 執行：

```powershell
.\Release.ps1
```

預設 output：

```text
artifacts/portable/ImageViewerWin-win-x64/
```

Executable 位於：

```text
artifacts/portable/ImageViewerWin-win-x64/ImageViewerWin.exe
```

## 選項

```powershell
.\Release.ps1 -SkipTests
.\Release.ps1 -RuntimeIdentifier win-arm64 -Platform ARM64
.\Release.ps1 -RuntimeIdentifier win-x86 -Platform x86
```

## 注意事項

這不是 single-file executable。WinUI unpackaged output 必須保留 `ImageViewerWin.exe` 旁邊的 DLL、PRI、WinUI 與 runtime files。

不要只散佈 `ImageViewerWin.exe`；請散佈完整資料夾。

預設 output 是 framework-dependent。目標 machines 必須已安裝：

- Windows App Runtime 1.8
- .NET Runtime 10
- .NET Windows Desktop Runtime 10

## Script 行為

1. 使用 repo-local `NuGet.Config` restore Core、Application、Infrastructure 與 ViewModels test projects。
2. 除非傳入 `-SkipTests`，否則執行 Core、Application、Infrastructure 與 ViewModels tests。
3. Restore selected Windows RID 的 app。
4. 使用下列設定 publish：
   - `WindowsPackageType=None`
   - `WindowsAppSDKSelfContained=false`
   - `--self-contained false`
   - `PublishSelfContained=false`
   - `PublishSingleFile=false`
   - `SelfContained=false`
   - `DebugType=None`
   - `DebugSymbols=false`
5. 驗證 `ImageViewerWin.exe` 存在。
6. 回報 file count、total bytes 與 executable SHA256。
