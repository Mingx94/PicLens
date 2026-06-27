# Portable Release

PicLens release target 是 framework-dependent no-install folder，可複製到已安裝必要 .NET runtime 的 Windows 或 Linux 機器上。

## 建置

從 repository root 執行：

Windows：

```powershell
.\scripts\Release.ps1
```

Linux：

```bash
bash ./scripts/Release.sh
```

預設 output：

```text
artifacts/portable/PicLens-win-x64/
artifacts/portable/PicLens-linux-x64/
```

Executable 位於：

```text
artifacts/portable/PicLens-win-x64/PicLens.exe
artifacts/portable/PicLens-linux-x64/PicLens
```

## 選項

```powershell
.\scripts\Release.ps1 -SkipTests
.\scripts\Release.ps1 -RuntimeIdentifier win-arm64 -Platform ARM64
.\scripts\Release.ps1 -RuntimeIdentifier win-x86 -Platform x86
```

```bash
bash ./scripts/Release.sh --skip-tests
```

## 注意事項

這不是 single-file executable。請保留 executable 旁邊的 DLL、runtimeconfig、deps、Avalonia assets 與 app assets。

不要只散佈 executable；請散佈完整資料夾。

預設 output 是 framework-dependent。目標 machines 必須已安裝 .NET Runtime 10。

## Script 行為

1. 使用 repo-local `NuGet.Config` restore Core、Infrastructure 與 ViewModels test projects。
2. 除非傳入 `-SkipTests` / `--skip-tests`，否則執行 Core、Infrastructure 與 ViewModels tests。
3. Restore selected RID 的 app。
4. Publish framework-dependent portable output：
   - `--self-contained false`
   - `PublishSelfContained=false`
   - `PublishSingleFile=false`
   - `PublishTrimmed=false`
   - `SelfContained=false`
   - `DebugType=None`
   - `DebugSymbols=false`
5. 驗證 Windows `PicLens.exe` 或 Linux `PicLens` executable 存在。
6. 回報 file count、total bytes 與 executable SHA256。
