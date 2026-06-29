# Portable Release

PicLens release target 是 framework-dependent no-install folder，可複製到已安裝必要 .NET runtime 的 Windows 或 Linux 機器上。

## 建置

從 repository root 執行：

```shell
dotnet run Tasks.cs release
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

```shell
dotnet run Tasks.cs -- release --runtime win-arm64 --platform ARM64
dotnet run Tasks.cs -- release --runtime win-x86 --platform x86
```

## 注意事項

這不是 single-file executable。請保留 executable 旁邊的 DLL、runtimeconfig、deps、Avalonia assets 與 app assets。

不要只散佈 executable；請散佈完整資料夾。

預設 output 是 framework-dependent。目標 machines 必須已安裝 .NET Runtime 10。

## Script 行為

Release builds do not run tests. Run `dotnet run Tasks.cs test` separately before release.

1. Restore selected RID 的 app。
2. Publish framework-dependent portable output：
   - `--self-contained false`
   - `PublishSelfContained=false`
   - `PublishSingleFile=false`
   - Release 時 `PublishReadyToRun=true`
   - `PublishTrimmed=false`
   - `SelfContained=false`
   - `DebugType=None`
   - `DebugSymbols=false`
3. 驗證 Windows `PicLens.exe` 或 Linux `PicLens` executable 存在。
4. 回報 file count、total bytes 與 executable SHA256。
