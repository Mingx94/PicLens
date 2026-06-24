---
area: imaging
kind: pitfalls
---

# imaging — Pitfalls

## Profile WinRT thumbnails from a Windows TFM host

`code:` `src/PicLens.Infrastructure/Services/ThumbnailService.cs` -> `ThumbnailService.GetOrCreateThumbnailAsync` · `updated:` `2026-06-24` · `status:` `active`

Thumbnail profiling should run from a `net10.0-windows10.0.26100.0` test or runner that references the Infrastructure project. Loading the built assembly directly from PowerShell can produce false all-null thumbnail results because the WinRT-backed storage/decoder path is not being exercised under the same Windows target framework host.

<!--
Each pitfall is a `## heading` + a one-line meta + the body:

## Short title of the gotcha

`code:` `path/to/file.ext` -> `symbol` · `updated:` `YYYY-MM-DD` · `status:` `active`

What breaks, why, and what to do instead.
-->
