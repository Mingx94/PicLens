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

## Build And Test

```powershell
dotnet restore .\tests\PicLens.Core.Tests\PicLens.Core.Tests.csproj --configfile .\NuGet.Config
dotnet restore .\tests\PicLens.Application.Tests\PicLens.Application.Tests.csproj --configfile .\NuGet.Config
dotnet restore .\tests\PicLens.Infrastructure.Tests\PicLens.Infrastructure.Tests.csproj --configfile .\NuGet.Config
dotnet restore .\tests\PicLens.ViewModels.Tests\PicLens.ViewModels.Tests.csproj --configfile .\NuGet.Config
dotnet restore .\PicLens\PicLens.csproj --configfile .\NuGet.Config -r win-x64 /p:Platform=x64
dotnet test .\tests\PicLens.Core.Tests\PicLens.Core.Tests.csproj --no-restore
dotnet test .\tests\PicLens.Application.Tests\PicLens.Application.Tests.csproj --no-restore
dotnet test .\tests\PicLens.Infrastructure.Tests\PicLens.Infrastructure.Tests.csproj --no-restore
dotnet test .\tests\PicLens.ViewModels.Tests\PicLens.ViewModels.Tests.csproj --no-restore
dotnet build .\PicLens\PicLens.csproj --no-restore /p:Platform=x64
.\BuildAndRun.ps1 .\PicLens\PicLens.csproj
```

Build-only verification 可使用：

```powershell
.\BuildAndRun.ps1 .\PicLens\PicLens.csproj -SkipRun
```

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
