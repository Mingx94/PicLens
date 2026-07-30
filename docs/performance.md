# Performance

Performance claims require Release builds and representative libraries. Debug timings、framework impressions、兩張圖片 smoke 或單一 unit test 不可作為 release evidence。

## Reproducible Windows gate

```powershell
cmake --build --preset release --target piclens
pwsh -File scripts/measure-performance.ps1 `
  -FolderPath <representative-folder>
```

The script launches the real Qt Quick executable with isolated settings/cache, recursive scanning and offscreen rendering. It performs cold-cache and warm-cache runs against the same isolated profile and exercises virtualized-gallery scrolling when the dataset exceeds one viewport.

Current conservative thresholds:

- scan/model/settle elapsed: at most 5,000 ms;
- peak working set: at most 512 MiB;
- non-empty row/image counts are mandatory.

Generated JSON includes library-ready and first-thumbnail latency, completed thumbnail requests, throughput/concurrency, cache hits, CPU, logical processor count, graphics API, frame-swap intervals and memory. `windows-release.json` is the cold run and `windows-release-warm.json` is the warm run.

Frame intervals remain diagnostic until a representative interactive baseline defines a release threshold; they are not CPU/GPU render-duration measurements.

## Dataset rules

- Local release evidence uses an authorized representative library and records image count、storage characteristics、build/toolchain and date.
- The hosted Windows workflow creates 10,000 copied valid PNG paths to exercise path enumeration and model scale.
- Repeated small files or hard links do not represent heterogeneous decoder/storage cost and do not replace a real-library run.
- Raw output under `artifacts/performance/` is intentionally ignored; durable claims must link to an immutable CI run or archived report.

## Existing performance mechanisms

- Virtualized gallery delegates keep shared menus/dialogs outside each tile.
- Installed platform fonts avoid embedding large CJK font files at startup.
- Viewer QML is instantiated only while open.
- `LibraryItemModel` indexes path identity and limits role notifications to affected rows.
- Search is debounced and preserves valid thumbnail mappings across projection changes.
- Thumbnail cache capacity is tracked incrementally with bounded pruning.
- A bounded decoded-image cache and asynchronous image provider avoid repeated cold-path PNG decode.
- Visible-thumbnail concurrency scales within configured bounds.
- JPG/WebP conversions use bounded worker and decoded-memory budgets.
- Viewer decode requests use viewport/DPI-sized quantized tiers with a bounded dimension.
- Supported Release toolchains enable interprocedural optimization where compatible.

## Evidence and backlog

Historical July 2026 measurements are archived in [2026-07 performance evidence](archive/performance/2026-07.md). Archive values do not prove the current checkout.

Outstanding performance work should be tracked in the issue tracker rather than duplicated as an unchecked list in this document. This file remains the authority for measurement method and thresholds.
