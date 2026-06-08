# 測試

## Unit Tests

執行：

```powershell
dotnet restore .\tests\ImageViewerWin.Core.Tests\ImageViewerWin.Core.Tests.csproj --configfile .\NuGet.Config
dotnet restore .\tests\ImageViewerWin.Application.Tests\ImageViewerWin.Application.Tests.csproj --configfile .\NuGet.Config
dotnet restore .\tests\ImageViewerWin.Infrastructure.Tests\ImageViewerWin.Infrastructure.Tests.csproj --configfile .\NuGet.Config
dotnet restore .\tests\ImageViewerWin.ViewModels.Tests\ImageViewerWin.ViewModels.Tests.csproj --configfile .\NuGet.Config
dotnet test .\tests\ImageViewerWin.Core.Tests\ImageViewerWin.Core.Tests.csproj --no-restore
dotnet test .\tests\ImageViewerWin.Application.Tests\ImageViewerWin.Application.Tests.csproj --no-restore
dotnet test .\tests\ImageViewerWin.Infrastructure.Tests\ImageViewerWin.Infrastructure.Tests.csproj --no-restore
dotnet test .\tests\ImageViewerWin.ViewModels.Tests\ImageViewerWin.ViewModels.Tests.csproj --no-restore
```

目前 coverage 包含：

- `ImageViewerWin.Core`：pure product rules。
- `ImageViewerWin.Application`：deterministic rename planning，包含 drop-target sequence advancement past existing targets。
- `ImageViewerWin.Infrastructure`：JSON settings、direct 與 recursive scanning、canonical directory de-duplication、image data helpers、disk thumbnail cache generation and pruning、conversion、trash 與 rename operations。
- `ImageViewerWin.ViewModels`：startup folder selection flow、sort-without-rescan behavior、contextual selection state、library reload 時的 stale-selection clearing、async thumbnail path updates、thumbnail cancellation、stalled-thumbnail timeout recovery、thumbnail-size persistence、GridView thumbnail event wiring、failure paths 的 diagnostic error logging，以及繁體中文 runtime copy。

## WinUI Build

執行：

```powershell
dotnet restore .\ImageViewerWin\ImageViewerWin.csproj --configfile .\NuGet.Config -r win-x64 /p:Platform=x64
dotnet build .\ImageViewerWin\ImageViewerWin.csproj --no-restore /p:Platform=x64
```

Plugin workflow 可執行：

```powershell
.\BuildAndRun.ps1 .\ImageViewerWin\ImageViewerWin.csproj -SkipRun
```

`BuildAndRun.ps1` 使用 Visual Studio MSBuild；若 NuGet config access 被 restricted sandboxes 擋住，可能需要在 sandbox 外執行。

## Portable Release Verification

執行：

```powershell
.\Release.ps1
```

這會 restore packages、執行 Core、Application、Infrastructure 與 ViewModel tests、publish framework-dependent unpackaged output folder，並驗證 `ImageViewerWin.exe` 存在。

Manual smoke check：

```powershell
.\artifacts\portable\ImageViewerWin-win-x64\ImageViewerWin.exe
```

App 應可不安裝 MSIX package 直接啟動。

## Runtime Crash Diagnostics

處理 WinUI 或 native/XAML crash 時，build 成功還不夠。App build 完後，執行短時間 debug-output launch 並檢查 app log：

```powershell
.\BuildAndRun.ps1 .\ImageViewerWin\ImageViewerWin.csproj
Get-Content "$env:LOCALAPPDATA\ImageViewerWin\Logs\ImageViewerWin.log" -Tail 100
```

可能失敗的 development paths 應透過 app logger 記錄，並包含足夠 context 來辨識失敗的 item、path 與 operation。

若要直接使用 `winapp run`，請讓它指向 build output folder 並自動偵測 output manifest，例如：

```powershell
winapp run .\ImageViewerWin\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64 --debug-output
```

不要把 source `ImageViewerWin\Package.appxmanifest` 手動套到 build output folder；source manifest 尚未展開 `$targetnametoken$` / `$targetentrypoint$`，可能導致早期 `System.TypeInitializationException` / `REGDB_E_CLASSNOTREG (0x80040154)` 假陽性 crash。
