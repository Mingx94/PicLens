---
area: app
kind: business-rules
---

# app — Business rules

<!--
Each rule is a `## heading` + a one-line meta + the body. Capture the "why" code can't show.

## Name of the rule

`code:` `path/to/file.ext` -> `symbol` · `updated:` `YYYY-MM-DD` · `status:` `active`

The rule, the reasoning, and edge cases.
-->

## App icons use FluentIcons.Avalonia

`code:` `PicLens/Views/MainView.axaml` -> `FluentIcon` · `code:` `PicLens/PicLens.csproj` -> `FluentIcons.Avalonia` · `updated:` `2026-06-28` · `status:` `active`

PicLens uses the `FluentIcons.Avalonia` package for in-app toolbar, tile fallback, and viewer icons. Use `FluentIcon` in the Avalonia app layer instead of reintroducing embedded `StreamGeometry` resources.

## Windows installer uses Inno Setup

`code:` `scripts/BuildInstaller.ps1` · `code:` `installer/PicLens.iss` · `updated:` `2026-06-28` · `status:` `active`

PicLens uses an Inno Setup `.exe` as the normal Windows installer because it installs like a regular desktop app without MSIX sideloading certificate setup. Keep the portable release separate; installer staging strips `.pdb` debug symbols while portable folders keep them.
