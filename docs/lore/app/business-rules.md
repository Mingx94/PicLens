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

## App icons use Avalonia Fluent geometry

`code:` `PicLens/Views/MainView.axaml` -> `StreamGeometry` icon resources · `updated:` `2026-06-28` · `status:` `active`

PicLens uses Avalonia Fluent theme, so in-app toolbar, tile fallback, and viewer icons should use Avalonia Fluent icon geometry through `PathIcon`/`StreamGeometry`. Keep this as embedded geometry in the Avalonia app layer instead of adding an icon-font or SVG package; add a dependency only when a needed icon cannot be covered by the Fluent geometry set.
