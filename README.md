# PicLens

PicLens 是原生 WinUI 3 / MVVM 圖片整理與檢視 app。

## 目前狀態

- WinUI 3 / MVVM app shell，使用 CommunityToolkit.Mvvm。
- Main window 支援明確資料夾選取、上一個資料夾還原、資料夾掃描、縮圖載入、選取狀態、contextual selected-image actions，以及保守的檔案操作。
- Secondary image viewer window 支援 previous/next、zoom、pan、fullscreen、keyboard navigation，以及 animated-image unsupported feedback。
- Core domain project 保留不依賴 WinUI 的 product rules。
- Application 與 Infrastructure service projects 負責 settings persistence、scanning、image loading、JPG conversion、recycle-bin trash 與 rename planning。
- Core、Application、Infrastructure 與 ViewModel behavior 由 xUnit tests 覆蓋。

## Solution

```text
PicLens.slnx
PicLens/                         WinUI 3 app
src/PicLens.Application/         Service contracts 與 deterministic planning
src/PicLens.Core/                Pure models 與 domain rules
src/PicLens.Infrastructure/      JSON、filesystem、image 與 recycle-bin services
tests/PicLens.Application.Tests/ xUnit application tests
tests/PicLens.Core.Tests/        xUnit domain tests
tests/PicLens.Infrastructure.Tests/ xUnit infrastructure tests
tests/PicLens.ViewModels.Tests/  xUnit ViewModel 與 localization tests
```

## Visual Studio Development

使用 Visual Studio 開發時，請開啟 `PicLens.slnx`，solution platform 選 `x64`。`PicLens` WinUI app project 已在 solution 中設定 x86/x64/ARM64 platform mapping 與 deploy metadata，Visual Studio 可以用一般的 build / deploy / debug 流程開發。

`PicLens.ViewModels.Tests` 會參考 WinUI app project；為避免 Visual Studio/MSBuild 在 solution build 中同時觸發兩次 XAML compiler，這個 test project 會載入在 solution 內，但不參與預設 solution build。需要跑 ViewModel tests 時請獨立執行：

```powershell
dotnet test .\tests\PicLens.ViewModels.Tests\PicLens.ViewModels.Tests.csproj -p:Platform=x64
```

## Build And Test

日常 unit 與 ViewModel 驗證：

```powershell
.\Test.ps1
```

`dotnet test .\PicLens.slnx` 不是完整 unit 驗證；`PicLens.ViewModels.Tests` 會參考 WinUI app project，為避免 solution build 同時直接建 app 又透過 test project 間接建 app 造成 XAML compiler output 衝突，solution 會載入但不預設建置這個 test project。

逐專案手動驗證：

```powershell
dotnet restore .\tests\PicLens.Core.Tests\PicLens.Core.Tests.csproj --configfile .\NuGet.Config
dotnet restore .\tests\PicLens.Application.Tests\PicLens.Application.Tests.csproj --configfile .\NuGet.Config
dotnet restore .\tests\PicLens.Infrastructure.Tests\PicLens.Infrastructure.Tests.csproj --configfile .\NuGet.Config
dotnet restore .\tests\PicLens.ViewModels.Tests\PicLens.ViewModels.Tests.csproj --configfile .\NuGet.Config /p:Platform=x64
dotnet restore .\PicLens\PicLens.csproj --configfile .\NuGet.Config -r win-x64 /p:Platform=x64
dotnet test .\tests\PicLens.Core.Tests\PicLens.Core.Tests.csproj --no-restore
dotnet test .\tests\PicLens.Application.Tests\PicLens.Application.Tests.csproj --no-restore
dotnet test .\tests\PicLens.Infrastructure.Tests\PicLens.Infrastructure.Tests.csproj --no-restore
dotnet test .\tests\PicLens.ViewModels.Tests\PicLens.ViewModels.Tests.csproj --no-restore -p:Platform=x64
dotnet build .\PicLens.slnx -p:Platform=x64
dotnet build .\PicLens\PicLens.csproj --no-restore /p:Platform=x64
.\BuildAndRun.ps1 .\PicLens\PicLens.csproj
```

Opt-in FlaUI UI smoke：

```powershell
.\tools\RunUiTests.ps1
```

這組 smoke 會用 isolated data root 跑 empty state、seeded gallery、selection action bar、thumbnail size persistence 與 secondary viewer 核心流程。

Build-only verification 可使用：

```powershell
.\BuildAndRun.ps1 .\PicLens\PicLens.csproj -SkipRun
```

開發 UI 時可用 watch runner；它不是 Hot Reload，而是在 `.cs`、`.xaml`、manifest、assets 等檔案存檔後關掉舊 app，並交給 `BuildAndRun.ps1 -Detach` rebuild/relaunch：

```powershell
.\WatchAndRun.ps1 .\PicLens\PicLens.csproj
```

watch runner 的 build / launch ERROR LOG 會寫到 `logs\watch-run\`。

## Portable Release

建置免安裝 app folder：

```powershell
.\Release.ps1
```

Output：

```text
artifacts/portable/PicLens-win-x64/PicLens.exe
```

請保留完整 folder；這是 WinUI framework-dependent unpackaged layout，不是 single-file exe。

## Docs

從 [docs/README.md](docs/README.md) 開始。
