# PicLens

PicLens 是 Windows-only 的 Avalonia / MVVM 圖片整理與檢視 app。

## 目前狀態

- Avalonia desktop app shell，使用 Fluent theme、Inter font 與 CommunityToolkit.Mvvm。
- Main window 支援明確資料夾選取、上一個資料夾還原、資料夾掃描、縮圖載入、選取狀態、context menu actions，以及保守的檔案操作。
- 主視窗內嵌 image viewer 支援 previous/next、zoom、pan、keyboard navigation、Escape close，以及 animated-image unsupported feedback。
- `src/PicLens.Presentation` 保留 UI-agnostic ViewModels 與 dialog/logger presentation contracts。
- `src/PicLens.Core` 保留純 product rules 與 service contracts。
- `src/PicLens.Infrastructure` 負責 settings persistence、scanning、thumbnails、recycle-bin trash、rename execution 與 file logging。
- Core、Infrastructure 與 ViewModel behavior 由 xUnit tests 覆蓋；FlaUI smoke tests 覆蓋主要 UI flows。

## Solution

```text
PicLens.slnx
PicLens/                            Avalonia desktop app、AXAML views、assets、window setup
src/PicLens.Core/                   Pure models、service contracts 與 deterministic rules
src/PicLens.Infrastructure/         JSON、filesystem、thumbnail、recycle-bin 與 logging services
src/PicLens.Presentation/           UI-agnostic ViewModels 與 presentation services
tests/PicLens.Core.Tests/           xUnit domain tests
tests/PicLens.Infrastructure.Tests/ xUnit infrastructure tests
tests/PicLens.ViewModels.Tests/     xUnit ViewModel tests
tests/PicLens.Ui.Tests/             FlaUI smoke tests
```

## Build And Test

```powershell
.\Test.ps1
dotnet build .\PicLens.slnx -p:Platform=x64
.\BuildAndRun.ps1 .\PicLens\PicLens.csproj
```

Opt-in UI smoke tests：

```powershell
.\tools\RunUiTests.ps1
```

## Portable Release

```powershell
.\Release.ps1
```

Output：

```text
artifacts/portable/PicLens-win-x64/PicLens.exe
```

請保留完整 folder；這是 framework-dependent portable output，不是 single-file exe。

## Docs

從 [docs/README.md](docs/README.md) 開始。
