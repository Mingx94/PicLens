# 測試

## Unit Tests

日常 unit 與 ViewModel 驗證請執行：

Windows：

```powershell
.\scripts\Test.ps1
```

Linux：

```bash
bash ./scripts/Test.sh
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
- UI runtime contract 由 FlaUI smoke tests 覆蓋，例如主要 AutomationId、menu/dialog、selection、inline viewer、settings persistence 與實際互動結果。
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

Visual Studio 開發時開啟 `PicLens.slnx`，solution platform 選 `x64`。Solution 會載入 app、src 與 tests projects；`PicLens.ViewModels.Tests` 與 `PicLens.Ui.Tests` 預設不參與 solution build，完整驗證請使用本文件列出的 scripts。

Build-only helper：

Windows：

```powershell
.\scripts\BuildAndRun.ps1 .\PicLens\PicLens.csproj -SkipRun
```

Linux：

```bash
bash ./scripts/BuildAndRun.sh ./PicLens/PicLens.csproj --skip-run
```

## FlaUI UI Smoke Tests

FlaUI 測試是 opt-in。執行：

```powershell
.\scripts\RunUiTests.ps1
```

這會先產生 Windows framework-dependent portable output，再執行 `tests\PicLens.Ui.Tests`。測試啟動 app 時會設定 isolated `PICLENS_DATA_ROOT`，因此 settings、thumbnail cache 與 ERROR LOG 都會寫到測試 artifact 資料夾，不會覆蓋使用者的 local app data。

目前 UI smoke coverage 包含：

- 啟動 published Windows `PicLens.exe` 並等待 main window。
- 驗證 empty-state 主要 AutomationId：title bar、folder navigation command bar、library command bar、folder tree、library grid、status bar、thumbnail size slider、empty state action。
- 開啟排序與更多圖庫動作 menus，確認預期 menu items 存在。
- 以 seeded gallery 啟動 app，驗證 last-folder restore、folder tree、library grid、root image tiles、direct child folder tile 與 status feedback。
- 驗證資料夾歷史 back/forward buttons 會在 root 與 direct child folder 之間導覽。
- 驗證排序 menu、含子資料夾 toggle、recursive image visibility，以及 settings persistence。
- 驗證左鍵 image selection 不顯示底部 action bar，右鍵 image context menu 會顯示 rename/trash actions。
- 驗證 rename dialog 可取消，且不會修改原始檔案。
- 驗證 trash confirmation dialog 可取消，且不會移動原始檔案。
- 驗證 clear same-basename confirmation dialog 可取消，且不會移動原始檔案。
- 驗證 thumbnail size slider persistence。
- 驗證 inline viewer smoke：double click 在主視窗內開啟 viewer、title update、previous/next、zoom controls、viewer image controls，以及 Escape close。

失敗時會把 PID、data root、seeded library root、ERROR LOG、screenshot 與 UIA tree dump 寫到：

```text
artifacts\ui-tests\
```

## Portable Release Verification

執行：

Windows：

```powershell
.\scripts\Release.ps1
```

Linux：

```bash
bash ./scripts/Release.sh --skip-tests
```

這會先呼叫該平台的 test script 執行 Core、Infrastructure 與 ViewModel tests，再 restore/publish framework-dependent portable output folder，並驗證 executable 存在。

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
.\scripts\BuildAndRun.ps1 .\PicLens\PicLens.csproj
Get-Content "$env:LOCALAPPDATA\PicLens\Logs\PicLens.log" -Tail 100
```

Linux：

```bash
bash ./scripts/BuildAndRun.sh ./PicLens/PicLens.csproj
tail -n 100 "${XDG_DATA_HOME:-$HOME/.local/share}/PicLens/Logs/PicLens.log"
```

可能失敗的 development paths 應透過 app logger 記錄，並包含足夠 context 來辨識失敗的 item、path 與 operation。App logger 使用 bounded best-effort queue；錯誤風暴下可能丟棄較舊 log，排查時請保留重現步驟與最新 ERROR context。

## Recent Verification

單次驗證結果放在 `docs/verification/`，避免 runtime contract 變成測試報告。

- [2026-06-24 runtime verification](verification/2026-06-24.md)
