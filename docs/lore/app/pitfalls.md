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

## Avalonia layout containers need explicit UIA exposure for FlaUI locators

`code:` `PicLens/Views/MainView.axaml` -> `AppTitleBar` · `updated:` `2026-06-28` · `status:` `active`

FlaUI can test PicLens on Windows through the platform UI Automation tree, but Avalonia layout containers such as `Border` may stay out of the Control view even when they have an `AutomationProperties.AutomationId`. When a smoke test must locate a non-interactive container, set `AutomationProperties.AccessibilityView="Control"` and `AutomationProperties.ControlTypeOverride="Pane"` on that container instead of assuming the AutomationId alone is enough.

## Avalonia devtools attach should be opt-in during startup work

`code:` `PicLens/Program.cs` -> `BuildAvaloniaApp` · `updated:` `2026-06-28` · `status:` `active`

Avalonia Developer Tools can throw "Developer tools have already been attached" when the app is launched in a context that already attached diagnostics. Keep Debug devtools opt-in for PicLens startup/performance checks instead of attaching on every Debug launch.
