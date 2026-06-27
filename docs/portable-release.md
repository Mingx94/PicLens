# Portable Release

PicLens release target 是 framework-dependent no-install folder，可複製到已安裝必要 .NET runtime 的 Windows 機器上，並透過 `PicLens.exe` 啟動。

## 建置

從 repository root 執行：

```powershell
.\Release.ps1
```

預設 output：

```text
artifacts/portable/PicLens-win-x64/
```

Executable 位於：

```text
artifacts/portable/PicLens-win-x64/PicLens.exe
```

## 選項

```powershell
.\Release.ps1 -SkipTests
.\Release.ps1 -RuntimeIdentifier win-arm64 -Platform ARM64
.\Release.ps1 -RuntimeIdentifier win-x86 -Platform x86
```

## 注意事項

這不是 single-file executable。請保留 `PicLens.exe` 旁邊的 DLL、runtimeconfig、deps、Avalonia assets 與 app assets。

不要只散佈 `PicLens.exe`；請散佈完整資料夾。

預設 output 是 framework-dependent。目標 machines 必須已安裝 .NET Runtime 10。

## Script 行為

1. 使用 repo-local `NuGet.Config` restore Core、Infrastructure 與 ViewModels test projects。
2. 除非傳入 `-SkipTests`，否則執行 Core、Infrastructure 與 ViewModels tests。
3. Restore selected Windows RID 的 app。
4. Publish framework-dependent portable output：
   - `--self-contained false`
   - `PublishSelfContained=false`
   - `PublishSingleFile=false`
   - `PublishTrimmed=false`
   - `SelfContained=false`
   - `DebugType=None`
   - `DebugSymbols=false`
5. 驗證 `PicLens.exe` 存在。
6. 回報 file count、total bytes 與 executable SHA256。
