---
area: imaging
kind: pitfalls
---

# imaging — Pitfalls

## Profile WinRT thumbnails from a Windows TFM host

`code:` `src/PicLens.Infrastructure/Services/ThumbnailService.cs` -> `ThumbnailService.GetOrCreateThumbnailAsync` · `updated:` `2026-06-27` · `status:` `resolved`

This applied when thumbnails used WinRT APIs and the Infrastructure project targeted Windows. The service now uses SkiaSharp under `net10.0`, so thumbnail profiling no longer needs a Windows TFM host.

## Do not use Avalonia Bitmap.Save for JPG conversion

`code:` `src/PicLens.Infrastructure/Services/FileOperationService.cs` -> `EncodeAsJpegAsync` · `updated:` `2026-06-27` · `status:` `active`

Avalonia `Bitmap.Save` is useful for app-layer image display and thumbnail-style PNG output, but it is not the right contract for PicLens JPG conversion. Use SkiaSharp encoding for JPG so the converter controls the encoded format on both Windows and Linux.

<!--
Each pitfall is a `## heading` + a one-line meta + the body:

## Short title of the gotcha

`code:` `path/to/file.ext` -> `symbol` · `updated:` `YYYY-MM-DD` · `status:` `active`

What breaks, why, and what to do instead.
-->
