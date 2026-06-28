---
area: app
kind: pitfalls
---

# app — Pitfalls

<!--
Each pitfall is a `## heading` + a one-line meta + the body:

## Short title of the gotcha

`code:` `path/to/file.ext` -> `symbol` · `updated:` `YYYY-MM-DD` · `status:` `active`

What breaks, why, and what to do instead.
-->

## Avalonia image paths need an app-layer converter

`code:` `PicLens/Views/MainView.axaml` -> `Image.Source` · `code:` `PicLens/Converters/ImagePathConverter.cs` -> `ImagePathConverter` · `updated:` `2026-06-27` · `status:` `active`

In this app, binding a `string` file path directly to Avalonia `Image.Source` left thumbnails and the inline viewer blank even though thumbnail cache files were generated. Keep presentation view models UI-agnostic and convert paths to `Bitmap` in the Avalonia app layer instead.

## Core domain rules must not P/Invoke Windows shell APIs

`code:` `src/PicLens.Core/Domain/ListItemSorter.cs` -> `ListItemSorter` · `code:` `src/PicLens.Core/Domain/FileRenamePlanner.cs` -> `ValidateImageFileName` · `updated:` `2026-06-27` · `status:` `active`

PicLens targets Linux as well as Windows, so Core domain behavior must be deterministic without Windows-only APIs or OS-specific filename character sets. A direct `StrCmpLogicalW` call fails on Linux because `shlwapi.dll` is unavailable, and `Path.GetInvalidFileNameChars()` allows names such as `bad:name.jpg` on Linux even though those names are unsafe for PicLens' cross-platform contract. Keep natural sorting and conservative filename validation implemented in managed Core code.

## FileShare locks are not a portable failure simulation

`code:` `tests/PicLens.Infrastructure.Tests/JsonSettingsStoreTests.cs` -> `UpdateAsync_does_not_overwrite_existing_settings_when_read_failure_cannot_be_quarantined` · `updated:` `2026-06-27` · `status:` `active`

Windows file sharing modes can make a second open or move fail while a file is locked with `FileShare.None`. Linux does not enforce the same mandatory locking semantics for this test case, so cross-platform filesystem failure tests should use Unix permissions on Linux and Windows file locks only on Windows.

## Former FlaUI locators needed explicit UIA exposure

`code:` `PicLens/Views/MainView.axaml` -> `AppTitleBar` · `updated:` `2026-06-28` · `status:` `resolved`

The former FlaUI smoke tests ran through the Windows UI Automation tree, where Avalonia layout containers such as `Border` could stay out of the Control view even when they had an `AutomationProperties.AutomationId`. PicLens now uses Avalonia Headless UI smoke tests, so this is no longer a current test constraint, but keep the lesson if UIA-based E2E tests return.

## Headless flyout items may not inherit DataContext until opened

`code:` `tests/PicLens.Ui.Tests/MainWindowSmokeTests.cs` -> `ExecuteMenuItem` · `code:` `PicLens/Views/MainView.axaml` -> `TitleBarSortMenuButton` · `updated:` `2026-06-28` · `status:` `active`

Avalonia Headless tests can inspect `MenuFlyout.Items` without opening the popup, but those detached `MenuItem`s may not have inherited the button's DataContext yet. When a headless smoke test needs to trigger a flyout command without testing popup mechanics, use the menu item's literal `CommandParameter` and execute the owning ViewModel command directly.

## Avalonia devtools attach should be opt-in during startup work

`code:` `PicLens/Program.cs` -> `BuildAvaloniaApp` · `updated:` `2026-06-28` · `status:` `active`

Avalonia Developer Tools can throw "Developer tools have already been attached" when the app is launched in a context that already attached diagnostics. Keep Debug devtools opt-in for PicLens startup/performance checks instead of attaching on every Debug launch.

## ItemsRepeater needs its separate Avalonia package and selection plan

`code:` `PicLens/PicLens.csproj` -> `Avalonia.Controls.ItemsRepeater` · `code:` `PicLens/Views/MainView.axaml` -> `LibraryRepeater` · `updated:` `2026-06-28` · `status:` `active`

Avalonia docs mention `ItemsRepeater` and `UniformGridLayout` for large custom grids, but PicLens' installed `Avalonia` 12.0.5 package does not expose those types by itself. The docs sample compiles after adding the separate `Avalonia.Controls.ItemsRepeater` package; `ItemsRepeater` resolves from `Avalonia.Controls`, while `UniformGridLayout` resolves from `Avalonia.Layout` in that assembly. Do not replace `ListBox` outright without a selection/drag plan because `ItemsRepeater` does not provide the existing `ListBox` selection behavior.

## Former ListBox thumbnail grid was UI-materialization bound before thumbnails started

`code:` `PicLens/Views/MainView.axaml` -> `LibraryRepeater` · `code:` `PicLens/Views/MainView.axaml.cs` -> `LibraryTile_Loaded` · `updated:` `2026-06-28` · `status:` `resolved`

Profiling a fresh 2000-BMP folder load showed the ViewModel/service path completing in about 155 ms, but the former `ListBox` + `WrapPanel` materialized all 2000 tile controls before visible thumbnail requests began. Replacing that grid with `ItemsRepeater` fixed the first-load materialization bottleneck while keeping thumbnail work gated to visible tiles.

## Former MSIX packaging needed source visual assets and trusted certificate roots

`code:` `Tasks.cs` -> `Installer` · `code:` `PicLens/PicLens.csproj` -> `AvaloniaResource Include="Assets\*.png"` · `updated:` `2026-06-29` · `status:` `resolved`

The former MSIX installer path needed PNG logo assets for shell identity and required self-signed dev certificates to be trusted in `Cert:\LocalMachine\Root`; user-level trust still produced `0x800B0109` in Windows App Installer. PicLens switched to Inno Setup for the normal Windows installer to avoid that sideloading certificate friction.
