# 測試

## Unit Tests

執行：

```powershell
dotnet restore .\tests\PicLens.Core.Tests\PicLens.Core.Tests.csproj --configfile .\NuGet.Config
dotnet restore .\tests\PicLens.Application.Tests\PicLens.Application.Tests.csproj --configfile .\NuGet.Config
dotnet restore .\tests\PicLens.Infrastructure.Tests\PicLens.Infrastructure.Tests.csproj --configfile .\NuGet.Config
dotnet restore .\tests\PicLens.ViewModels.Tests\PicLens.ViewModels.Tests.csproj --configfile .\NuGet.Config
dotnet test .\tests\PicLens.Core.Tests\PicLens.Core.Tests.csproj --no-restore
dotnet test .\tests\PicLens.Application.Tests\PicLens.Application.Tests.csproj --no-restore
dotnet test .\tests\PicLens.Infrastructure.Tests\PicLens.Infrastructure.Tests.csproj --no-restore
dotnet test .\tests\PicLens.ViewModels.Tests\PicLens.ViewModels.Tests.csproj --no-restore
```

目前 coverage 包含：

- `PicLens.Core`：pure product rules。
- `PicLens.Application`：deterministic rename planning，包含 drop-target sequence basename occupancy 與缺號補齊。
- `PicLens.Infrastructure`：JSON settings、direct 與 recursive scanning、canonical directory de-duplication、image data helpers、disk thumbnail cache generation and pruning、conversion、trash 與 rename operations。
- `PicLens.ViewModels`：startup folder selection flow、sort-without-rescan behavior、contextual selection state、library reload 時的 stale-selection clearing、drop-target rename preview confirmation、drag pointer cleanup wiring、drag preview overlay wiring、drop target highlight binding、per-item batch failure diagnostic logging、async thumbnail path updates、thumbnail cancellation、stalled-thumbnail timeout recovery、thumbnail-size persistence、GridView thumbnail event wiring、failure paths 的 diagnostic error logging，以及繁體中文 runtime copy。

## WinUI Build

執行：

```powershell
dotnet restore .\PicLens\PicLens.csproj --configfile .\NuGet.Config -r win-x64 /p:Platform=x64
dotnet build .\PicLens\PicLens.csproj --no-restore /p:Platform=x64
```

Plugin workflow 可執行：

```powershell
.\BuildAndRun.ps1 .\PicLens\PicLens.csproj -SkipRun
```

`BuildAndRun.ps1` 使用 Visual Studio MSBuild；若 NuGet config access 被 restricted sandboxes 擋住，可能需要在 sandbox 外執行。

## Watch And Relaunch

非 Visual Studio 開發 UI 時，可使用 watch runner 在存檔後看見變化：

```powershell
.\WatchAndRun.ps1 .\PicLens\PicLens.csproj
```

這不是 XAML Hot Reload。`WatchAndRun.ps1` 會監看 `.cs`、`.xaml`、`.csproj`、manifest、assets 與相關設定檔；變更穩定後先透過 `BuildAndRun.ps1 -SkipRun` rebuild，build 成功才停止既有 `PicLens` process 並以 `winapp run --detach` relaunch。build 失敗時會保留舊 app，避免開發中斷在沒有可比較畫面的狀態。

ERROR LOG 位置：

```text
logs\watch-run\watch-run-*.log
```

常用選項：

```powershell
.\WatchAndRun.ps1 .\PicLens\PicLens.csproj -NoInitialRun
.\WatchAndRun.ps1 .\PicLens\PicLens.csproj -DebounceMilliseconds 1500
.\WatchAndRun.ps1 .\PicLens\PicLens.csproj -RunOnce -SkipLaunch
```

## Portable Release Verification

執行：

```powershell
.\Release.ps1
```

這會 restore packages、執行 Core、Application、Infrastructure 與 ViewModel tests、publish framework-dependent unpackaged output folder，並驗證 `PicLens.exe` 存在。

Manual smoke check：

```powershell
.\artifacts\portable\PicLens-win-x64\PicLens.exe
```

App 應可不安裝 MSIX package 直接啟動。

## Runtime Crash Diagnostics

處理 WinUI 或 native/XAML crash 時，build 成功還不夠。App build 完後，執行短時間 debug-output launch 並檢查 app log：

```powershell
.\BuildAndRun.ps1 .\PicLens\PicLens.csproj
Get-Content "$env:LOCALAPPDATA\PicLens\Logs\PicLens.log" -Tail 100
```

可能失敗的 development paths 應透過 app logger 記錄，並包含足夠 context 來辨識失敗的 item、path 與 operation。

若要直接使用 `winapp run`，請讓它指向 build output folder 並自動偵測 output manifest，例如：

```powershell
winapp run .\PicLens\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64 --debug-output
```

不要把 source `PicLens\Package.appxmanifest` 手動套到 build output folder；source manifest 尚未展開 `$targetnametoken$` / `$targetentrypoint$`，可能導致早期 `System.TypeInitializationException` / `REGDB_E_CLASSNOTREG (0x80040154)` 假陽性 crash。
