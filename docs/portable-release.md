# Portable Release

## Qt 6 Windows portable

Qt migration 的 Windows portable folder 是 self-contained Qt runtime bundle，不需要安裝 Qt 或 .NET。先完成 Release build，再從 repository root 執行：

```powershell
cmake --preset release --fresh
cmake --build --preset release --target piclens
pwsh -File qt/scripts/build-portable.ps1
```

Repository primary wrapper 已切到相同 Qt pipeline：

```powershell
dotnet run Tasks.cs release
```

預設 output：

```text
artifacts/qt-portable/PicLens-win-x64/
```

Script 會呼叫與目前 Qt toolchain 同一套的 `windeployqt`，部署 Qt DLL、Qt Quick imports、platform/image plugins、MinGW runtime，再以 PE dependency closure 補齊 MSYS2 共用的 ICU、HarfBuzz、compression 等 runtime DLL；同時加入 relative `qt.conf`、PicLens MIT、Qt 與 Noto font license texts。最後會把 PATH 收斂到 Windows system directories，從完成的資料夾執行 isolated `qoffscreen` process並驗證真實 exit code。整個 `PicLens-win-x64` 資料夾才是可散佈單位；不可只複製 `PicLens.exe`。

Windows MSI candidate、本機 lifecycle 與 Windows 2025 hosted same-version install/upgrade/launch/uninstall 已通過。MSVC portable evidence 為 1,407 files / 169,910,166 bytes，`PicLens.exe` SHA-256 `8DBB2B8DF82F6B174CD5425E373526A0C750A957CF9AC6144F3EEAE12FA5C9E0`；公開 release 前仍需簽章與最終 redistribution review。

Linux Qt portable 由 Linux host 執行：

```bash
bash qt/scripts/build-linux-portable.sh
```

Script 會明確關閉 system-package/system-Qt modes，跑 Release CTest、CMake install/generated Qt QML deployment script、Qt source license copy，以及清空環境後的 platform smoke（優先 offscreen；官方 archive 只有 xcb 時在 Xvfb 下使用 xcb），預設輸出到 `artifacts/qt-portable/PicLens-linux-x64/`。因此即使同一 build tree 曾用於 CPack，也不會把 portable desktop files 寫到 `/usr/share`。Ubuntu 24.04 hosted evidence 為 175 files / 153,114,407 bytes，並通過 DEB lifecycle。

Qt 的 Linux deployment 使用 Qt 6.5+ 官方 CMake deployment API；CMake install rule 會部署 executable、QML imports、Qt runtime/plugins、relative RPATH、desktop entry、icon 與 notices，再由 CPack 重用同一 install tree。

## Legacy Avalonia portable

PicLens release target 是 framework-dependent no-install folder，可複製到已安裝必要 .NET runtime 的 Windows 或主流 Linux 機器上。

## 建置

從 repository root 執行：

```shell
dotnet run Tasks.cs legacy-release
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
dotnet run Tasks.cs -- legacy-release --runtime win-arm64 --platform ARM64
dotnet run Tasks.cs -- legacy-release --runtime win-x86 --platform x86
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
