# 測試

## Unit Tests

日常 unit 與 ViewModel 驗證請執行：

```shell
dotnet run Tasks.cs test
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
- `PicLens.Presentation`：startup folder selection flow、sort-without-rescan behavior、contextual selection state、library reload stale-selection clearing、drop-target rename preview confirmation、large-batch confirmation/cancellation、10,000 item list reset behavior、drag pointer cleanup math、drag preview/autoscroll math、per-item batch failure diagnostic logging、async thumbnail path updates、thumbnail cancellation、stalled-thumbnail timeout recovery、thumbnail-size persistence、failure paths diagnostic error logging，以及繁體中文 runtime copy。

## Qt Migration Core Tests

正式 Qt production tree 使用 checked-in CMake presets。從 `qt/` 執行：

```powershell
cmake --preset debug
cmake --build --preset debug
ctest --preset debug
```

Release parity：

```powershell
cmake --preset release
cmake --build --preset release
ctest --preset release
```

目前 `piclens_core_tests` 以 Qt Test 覆蓋圖片副檔名、folder-first natural sorting、OS path comparison、settings merge、thumbnail-size normalization、zoom math、drag edge-autoscroll math、image sequence value semantics、batch result counts 與 drop-target rename planning。

`piclens_infrastructure_tests` 使用 isolated temporary directories 覆蓋 `PICLENS_DATA_ROOT`、default app-data paths、settings missing/default、invalid JSON quarantine、atomic patch persistence、legacy JSON field compatibility，以及 bounded background file logger flush。同步 settings API 必須由後續 presentation/app worker boundary 呼叫，不可在 GUI thread 執行磁碟 I/O。

`piclens_folder_scanner_tests` 覆蓋 direct/recursive scanning、supported-image filtering、folder-first results、GIF/WebP animation detection、invalid/unreadable animation candidates、Windows locked files、child-folder-only scans、pre-cancellation、missing folders，以及 recursive canonical symlink/junction de-duplication。

`piclens_file_operation_tests` 覆蓋原檔保留、JPG/animation/unsupported/target-exists skips、真實 JPEG encoding、partial-output cleanup、same-basename cleanup、trash failure results、single rename validation、same-name/collision reasons、drop-target sequence gaps、逐項失敗繼續、pre-cancellation，以及 reveal process request。測試透過注入邊界驗證 trash/reveal，不會把測試檔實際送進開發機的回收筒或開啟檔案管理器。

`piclens_presentation_tests` 覆蓋單一 navigation/history owner、back/forward、sort-without-rescan、recursive reload、settings persistence request signals、navigation/reload/sort/recursive/file-operation selection clearing、stale folder scan suppression、掃描中切換排序的 generation race、worker-thread execution、Traditional Chinese error state、stable model roles，以及 10,000-item single model reset。

`piclens_presentation_tests` 另覆蓋一般/Ctrl/Shift ordered image selection、只選圖片的 range 與 Explorer-style 右鍵 scope。`piclens_file_operation_controller_tests` 覆蓋 single-image rename gating、multi-image trash gating、visible-image immutable batch snapshot、保留副檔名、worker-thread execution、cancel suppression、逐項 failure diagnostics、selection clearing 與 refresh。`piclens_app_tests` 再以真實 temporary filesystem 驗證 composed rename 與 JPG conversion 的 side effects、原檔保留及重新掃描結果。

`piclens_platform_file_manager_smoke` 是不加入一般 CTest 的真實桌面 gate；它自行建立 temporary fixture，Windows 上驗證 Explorer `/select` 與 Recycle Bin，Linux 上用於驗證 `xdg-open` 與 `gio trash`。這個 smoke 會啟動桌面 app 並修改 temporary fixture，需明確手動執行。

Windows context-menu real runtime gate 以 UI Automation 右鍵未選圖片，驗證 selection scope、selected visual/status 同步，以及 reveal、rename、trash 三個 menu items 實際出現在 Qt Quick popup；驗證過程只開啟並關閉選單，不執行檔案操作。

`piclens_file_operation_controller_tests` 另覆蓋 ordered drag sources、drop target exclusion、typed preview 必須先於 execution、明確取消不呼叫 service、worker completion refresh 與 selection clearing。Windows Computer Use smoke 以真實 8px-threshold pointer drag 打開繁體中文 rename preview，驗證 source/target filename mapping，然後取消並確認狀態清理；不執行實際圖片重新命名。

`piclens_folder_tree_tests` 覆蓋 root preservation/replacement、current child selection、lazy child loading once、unloaded descendant path reconstruction、failure root retention、stable hierarchical roles，以及 stale root build suppression。

`piclens_app_tests` 以 isolated settings/log paths 與真實 temporary folders 覆蓋 valid last-folder restore、missing-folder picker request、picker root persistence、tree navigation 不覆寫 startup folder、跨 root Back/Forward restoration，以及 sort/recursive worker persistence。

`piclens_thumbnail_tests` 覆蓋 default cache root、bounded PNG dimensions、source preservation、metadata/size cache keys、cache hits、unsupported/missing/animated/canceled inputs、corrupt-cache regeneration，以及 bounded pruning。

`piclens_thumbnail_coordinator_tests` 覆蓋 visible request delivery、animated/duplicate suppression、cancel、size-generation stale suppression，以及四個 stalled decoder timeout 後第五個 visible request 仍可完成。`piclens_app_tests` 另覆蓋 thumbnail model role update、size clear/re-request 與 worker settings persistence。

`piclens_qml_tests` 使用 Qt Quick Test 與 Basic controls style 驗證 production design tokens、reusable toolbar control activation，以及 Back/Forward side-button routing 不接受一般 left button。`piclens_qmllint` 必須零警告；`qml_runtime_smoke` 以 offscreen platform、isolated `--data-root` 和 bounded `--smoke-ms` 建立完整 production scene，並實際掃描含圖片的 fixture folder。Windows 視覺 gate 另以正常 window backend、`--screenshot` 擷取 command bar、folder tree、thumbnail gallery 與 status state。

`piclens_viewer_controller_tests` 覆蓋 immutable sequence、previous/next、transform reset、Core pointer-anchor zoom、pan gate、animated GIF feedback 與 invalid snapshot。`piclens_presentation_tests` 驗證 clicked path 與 selection-order snapshot index；`piclens_app_tests` 驗證 library reload 不會改動已開啟 viewer。真實 Windows renderer gate 使用 `--viewer <path>` 等待 scan-ready 後直接開啟 inline viewer，再以 `--screenshot` 驗證 dark canvas、full image、title、navigation/zoom strip，並檢查 isolated lifecycle log。

Windows Recycle Bin/Explorer 已接上 Qt gallery commands 並通過真實 desktop smoke；Linux `gio trash`/`xdg-open` adapter 已實作，仍待 Linux desktop runner 驗證。

在 Avalonia 與 Qt coexistence 期間，Qt tests 不取代相關 legacy characterization tests；宣告 slice 完成前需同時執行適用的 .NET 與 Qt suites。

`.github/workflows/qt-migration.yml` 是 clean-runner release gate 定義：Windows 2025 runner 使用 MSVC Qt 6.8.3 跑 Release CTest、10,000-image performance、sanitized portable smoke、forced WiX rebuild、MSI database audit，以及同版 replacement install/upgrade/launch/uninstall/profile-preservation lifecycle；Ubuntu 24.04 runner 跑 Release CTest、Qt CMake deploy、Xvfb/xcb sanitized portable smoke，以及 Xvfb/DBus 下的真實 `gio trash` / `xdg-open` adapter smoke；Fedora 44 跑 system Qt/RPM lifecycle。2026-07-11 run 29147384340 三條 job 全部通過，並由主 `Tasks.cs installer` Linux path 產生 DEB；後續發行仍應要求目前 commit 的 workflow success。

本機已使用官方 `actionlint` 1.7.12 驗證 workflow 的 YAML、expressions、runner labels 與 action schema；這只證明 workflow 定義有效，不取代 hosted runner 執行。

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
dotnet build PicLens/PicLens.csproj -p:Platform=x64
```

## Avalonia Headless UI Smoke Tests

Headless UI smoke tests 是 opt-in，不會控制真實滑鼠鍵盤。執行：

```shell
dotnet run Tasks.cs ui-test
```

這會執行 `tests\PicLens.Ui.Tests`，以 `Avalonia.Headless.XUnit` 在 process 內建立 `MainWindow`。測試會設定 isolated `PICLENS_DATA_ROOT`，因此 settings、thumbnail cache 與 ERROR LOG 不會覆蓋使用者的 local app data。

目前 UI smoke coverage 包含：

- 驗證 empty-state 主要 controls：title bar、folder navigation command bar、library command bar、folder tree、library grid、status bar、thumbnail size slider、empty state action。
- 驗證排序與更多圖庫動作 menu bindings。
- 以 seeded gallery 建立 main window，驗證資料夾選擇器 last-folder restore、folder tree、library grid、root image tiles、direct child folder tile 與 status feedback。
- 驗證資料夾歷史 back/forward buttons 會在 root 與 direct child folder 之間導覽。
- 驗證排序 command、含子資料夾 toggle、recursive image visibility，以及 settings persistence。
- 驗證 ItemsRepeater tile selection、Ctrl multi-select，以及 Enter 開啟 inline viewer。

Headless 不驗證 Windows UI Automation、native window focus、真實右鍵選單或平台檔案對話；這些若需要覆蓋，應另加少量真實 E2E/手動 smoke。

## Portable Release Verification

完整 Windows local cutover gate（不安裝 MSI、不使用 Computer Use）：

```powershell
pwsh -NoProfile -File qt/scripts/run-windows-cutover-gate.ps1 `
  -PerformanceFolder "D:\representative-image-library"
```

此 gate 依序驗證 Qt Debug/Release、legacy rollback tests、legacy UI smoke、代表性效能、
Qt portable、MSI database 與 packaged profile continuity，並寫出
`artifacts/cutover/windows-local-gate.json`。Elevated MSI lifecycle、UIA tree 與 hosted
Windows/Linux evidence 刻意維持獨立 gate，不能被這份 JSON 取代。

執行：

```shell
dotnet run Tasks.cs release
```

主 Release 命令會建立 Qt self-contained portable bundle，並執行 Qt Release CTest、deployment 與 isolated offscreen smoke。舊 Avalonia tests 仍需另跑 `dotnet run Tasks.cs test`，直到 cutover gate 完成。

Manual smoke check：

```powershell
.\artifacts\qt-portable\PicLens-win-x64\PicLens.exe
```

```bash
./artifacts/qt-portable/PicLens-linux-x64/PicLens
```

App 應可不安裝直接啟動。

## Runtime Crash Diagnostics

處理 desktop runtime 或 native/XAML crash 時，build 成功還不夠。App build 完後，執行短時間 launch 並檢查 app log：

```powershell
dotnet run --project PicLens/PicLens.csproj -p:Platform=x64
Get-Content "$env:LOCALAPPDATA\PicLens\Logs\PicLens.log" -Tail 100
```

Linux：

```bash
dotnet run --project PicLens/PicLens.csproj -p:Platform=x64
tail -n 100 "${XDG_DATA_HOME:-$HOME/.local/share}/PicLens/Logs/PicLens.log"
```

可能失敗的 development paths 應透過 app logger 記錄，並包含足夠 context 來辨識失敗的 item、path 與 operation。App logger 使用 bounded best-effort queue；錯誤風暴下可能丟棄較舊 log，排查時請保留重現步驟與最新 ERROR context。

## Recent Verification

持續更新的 migration evidence 放在下列文件，避免 runtime contract 變成測試報告：

- [Qt parity audit](qt-parity-audit.md)
- [Qt Release performance evidence](performance.md)
- [Qt migration gates](qt-migration.md)
