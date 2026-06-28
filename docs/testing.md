# 測試

## Unit Tests

日常 unit 與 ViewModel 驗證請執行：

```bash
dotnet run --file Tasks.cs -- test
```

這會依序 restore/test Core、Infrastructure 與 ViewModels test projects。ViewModel tests target `net10.0` 並引用 `PicLens.Presentation`，不依賴 app project。

逐專案手動執行：

```powershell
dotnet restore .\tests\PicLens.Core.Tests\PicLens.Core.Tests.csproj --configfile .\NuGet.Config
dotnet restore .\tests\PicLens.Infrastructure.Tests\PicLens.Infrastructure.Tests.csproj --configfile .\NuGet.Config
dotnet restore .\tests\PicLens.ViewModels.Tests\PicLens.ViewModels.Tests.csproj --configfile .\NuGet.Config
dotnet test .\tests\PicLens.Core.Tests\PicLens.Core.Tests.csproj --no-restore
dotnet test .\tests\PicLens.Infrastructure.Tests\PicLens.Infrastructure.Tests.csproj --no-restore
dotnet test .\tests\PicLens.ViewModels.Tests\PicLens.ViewModels.Tests.csproj --no-restore
```

測試分層原則：

- Unit tests 驗證 domain、infrastructure 與 ViewModel 的可觀察行為。
- ViewModel tests 可以驗證 runtime copy、狀態轉換、命令結果、ERROR LOG context 與檔案系統 side effects。
- 不用 unit tests 讀取 `PicLens\*.axaml` 或 `PicLens\*.cs` 來 assert binding、event handler、layout spacing、control tree 或 code snippet。
- UI runtime contract 由 Avalonia Headless smoke tests 覆蓋，例如主要 control tree、menu bindings、tile selection、inline viewer、settings persistence 與 simulated input 結果。
- Manifest XML、暫存測試檔與 log 檔屬於資料 contract，可在 unit tests 讀取並驗證。

目前 coverage 包含：

- `PicLens.Core`：pure product rules 與 deterministic rename planning。
- `PicLens.Infrastructure`：JSON settings、direct/recursive scanning、canonical directory de-duplication、disk thumbnail cache generation and pruning、trash、rename operations 與 file app logger。
- `PicLens.Presentation`：startup folder selection flow、sort-without-rescan behavior、contextual selection state、library reload stale-selection clearing、drop-target rename preview confirmation、drag pointer cleanup math、drag preview/autoscroll math、per-item batch failure diagnostic logging、async thumbnail path updates、thumbnail cancellation、stalled-thumbnail timeout recovery、thumbnail-size persistence、failure paths diagnostic error logging，以及繁體中文 runtime copy。

## Build

Windows：

```powershell
dotnet build .\PicLens.slnx -p:Platform=x64
dotnet restore .\PicLens\PicLens.csproj --configfile .\NuGet.Config -r win-x64 /p:Platform=x64
dotnet build .\PicLens\PicLens.csproj --no-restore /p:Platform=x64
```

Linux：

```bash
dotnet build ./PicLens.slnx -p:Platform=x64
dotnet restore ./PicLens/PicLens.csproj --configfile ./NuGet.Config -r linux-x64 /p:Platform=x64
dotnet build ./PicLens/PicLens.csproj --no-restore /p:Platform=x64
```

Visual Studio 開發時開啟 `PicLens.slnx`，solution platform 選 `x64`。Solution 會載入 app、src 與 tests projects；`PicLens.ViewModels.Tests` 與 `PicLens.Ui.Tests` 預設不參與 solution build，完整驗證請使用本文件列出的 file-based scripts。

Build-only helper：

```bash
dotnet run --file Tasks.cs -- run PicLens/PicLens.csproj --skip-run
```

## Avalonia Headless UI Smoke Tests

Headless UI smoke tests 是 opt-in，不會控制真實滑鼠鍵盤。執行：

```bash
dotnet run --file Tasks.cs -- ui-test
```

這會執行 `tests\PicLens.Ui.Tests`，以 `Avalonia.Headless.XUnit` 在 process 內建立 `MainWindow`。測試會設定 isolated `PICLENS_DATA_ROOT`，因此 settings、thumbnail cache 與 ERROR LOG 不會覆蓋使用者的 local app data。

目前 UI smoke coverage 包含：

- 驗證 empty-state 主要 controls：title bar、folder navigation command bar、library command bar、folder tree、library grid、status bar、thumbnail size slider、empty state action。
- 驗證排序與更多圖庫動作 menu bindings。
- 以 seeded gallery 建立 main window，驗證 last-folder restore、folder tree、library grid、root image tiles、direct child folder tile 與 status feedback。
- 驗證資料夾歷史 back/forward buttons 會在 root 與 direct child folder 之間導覽。
- 驗證排序 command、含子資料夾 toggle、recursive image visibility，以及 settings persistence。
- 驗證 ItemsRepeater tile selection、Ctrl multi-select，以及 Enter 開啟 inline viewer。

Headless 不驗證 Windows UI Automation、native window focus、真實右鍵選單或平台檔案對話；這些若需要覆蓋，應另加少量真實 E2E/手動 smoke。

## Portable Release Verification

執行：

```bash
dotnet run --file Tasks.cs -- release
```

Release 不會執行 tests；需要驗證時先跑 `dotnet run --file Tasks.cs -- test`。Release 只 restore/publish framework-dependent portable output folder，並驗證 executable 存在。

Manual smoke check：

```powershell
.\artifacts\portable\PicLens-win-x64\PicLens.exe
```

```bash
./artifacts/portable/PicLens-linux-x64/PicLens
```

App 應可不安裝直接啟動。

## Runtime Crash Diagnostics

處理 desktop runtime 或 native/XAML crash 時，build 成功還不夠。App build 完後，執行短時間 launch 並檢查 app log：

```powershell
dotnet run --file Tasks.cs -- run PicLens/PicLens.csproj
Get-Content "$env:LOCALAPPDATA\PicLens\Logs\PicLens.log" -Tail 100
```

Linux：

```bash
dotnet run --file Tasks.cs -- run PicLens/PicLens.csproj
tail -n 100 "${XDG_DATA_HOME:-$HOME/.local/share}/PicLens/Logs/PicLens.log"
```

可能失敗的 development paths 應透過 app logger 記錄，並包含足夠 context 來辨識失敗的 item、path 與 operation。App logger 使用 bounded best-effort queue；錯誤風暴下可能丟棄較舊 log，排查時請保留重現步驟與最新 ERROR context。

## Recent Verification

單次驗證結果放在 `docs/verification/`，避免 runtime contract 變成測試報告。

- [2026-06-24 runtime verification](verification/2026-06-24.md)
