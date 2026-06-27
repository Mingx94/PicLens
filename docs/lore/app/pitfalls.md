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
