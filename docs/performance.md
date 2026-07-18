# Qt Release performance evidence

Performance claims require Release builds and representative libraries. Debug timings、framework impressions 或單一 unit test 不可作為 cutover evidence。

## Reproducible gate

Windows：

```powershell
cmake --build --preset release --target piclens
pwsh -File scripts/measure-performance.ps1 -FolderPath <representative-folder>
```

The script launches the real Qt Quick executable with isolated settings/cache, recursive scanning and offscreen rendering. It performs a cold-cache run followed by a warm-cache run against the same isolated profile, and exercises a down/up virtualized-gallery scroll when the dataset exceeds one viewport. The app writes JSON after the library model is applied and visible thumbnail requests have had a 1.5-second settle window. Current conservative thresholds are:

- scan/model/settle elapsed: at most 5,000 ms;
- peak working set: at most 512 MiB;
- non-empty row/image counts are mandatory.

The JSON also records library-ready and first-thumbnail latency, completed thumbnail requests, thumbnail throughput/concurrency, cache hits, process CPU time/average utilization, logical processor count, Qt Quick graphics API, frame-swap interval p95/p99 and process memory. `windows-release.json` is the cold run and `windows-release-warm.json` is the warm run. Frame intervals are diagnostic until representative interactive baselines justify a numeric release threshold; they are not CPU/GPU render-duration measurements.

The cross-platform workflow creates 10,000 copied PNG paths on a clean Windows runner and runs the same gate. Repeated small valid PNG files exercise path enumeration/model scale without representing heterogeneous decoder cost, so the real-library run remains separately required.

## Current local evidence

2026-07-11 local Windows Release run：

| Metric | Result |
|---|---:|
| Qt/toolchain | Qt 6.11.1, MSYS2 UCRT64 MinGW |
| Dataset | 2,017 recursively scanned images |
| Elapsed through settle window | 1,694 ms |
| Working set | 232,275,968 bytes |
| Peak working set | 232,275,968 bytes |
| Active thumbnail requests at sample | 0 |

Raw output is generated at `artifacts/performance/windows-release.json` and intentionally ignored by git.

## Optimization implementation

The current runtime avoids the largest known application-level hot paths:

- Gallery delegates contain only per-tile interaction and rendering state; context menus and rename/trash dialogs are shared once by the library pane.
- The application uses installed platform fonts with Qt's system font fallback instead of registering three embedded CJK OTF files at startup.
- The inline viewer subtree is instantiated only while the viewer is open.
- `LibraryItemModel` maintains a path-to-row index for O(1) thumbnail delivery and emits selection changes only for affected rows.
- Search input is debounced and search/sort model resets retain valid thumbnail mappings.
- Thumbnail cache size is tracked incrementally. Cache eviction has one non-blocking owner and prunes to 90% of the configured bound, so concurrent thumbnail loads do not queue behind a full cache-directory scan and newly generated thumbnails do not retrigger eviction one file at a time.
- A forced-asynchronous Qt Quick image provider serves generated thumbnails from a 128 MiB bounded decoded-image cache, avoiding the cold-path PNG read/decode round trip while retaining the persistent disk cache.
- Visible thumbnail concurrency scales to half of the available logical processors, bounded to 2-8 workers so decoding can use modern CPUs without starving Qt Quick.
- JPG/WebP batch conversion runs up to four independent targets concurrently. Inputs that resolve to the same output stay ordered, and a 512 MiB estimated decoded-image budget reduces concurrency for very large images.
- The inline viewer requests viewport/DPI-sized images at quantized zoom tiers and caps either decoded dimension at 8,192 pixels, avoiding an unconditional original-size scene-graph texture.
- Release builds enable interprocedural optimization when supported by the active non-MinGW toolchain. MinGW is excluded because Qt-generated QML COFF objects cannot be merged reliably by its LTO linker.

2026-07-18 local concurrency mechanism benchmark on an Intel Core i5-12400 used 8,191 hard-linked paths to the same small PNG. Adaptive thumbnail scheduling selected six workers. Cold/warm runs completed 663/1,042 visible-thumbnail requests in the 1.5-second settle window (400.1/630.8 requests per second), with first-thumbnail latency of 166/156 ms, frame-interval p95 of 10.05/9.99 ms and peak working sets of 180,498,432/208,773,120 bytes. The preceding four-worker sample in the same session completed 480/893 requests with frame-interval p95 of 11.02/11.40 ms and peak working sets of approximately 143/175 MiB. This supports the adaptive-concurrency mechanism while documenting its 34-37 MiB memory cost. Repeated hard links and warm filesystem metadata do not represent heterogeneous decoder or storage performance.

2026-07-11 post-change mechanism smoke on the two image assets in this repository produced cold/warm elapsed times of 1,612/1,624 ms, first thumbnail at 117 ms, two warm-cache hits, Direct3D 11 rendering, 5.99 ms frame-swap interval p95 and a 210,612,224-byte peak working set. This validates the new metrics and warm-cache path only; two images are not representative performance evidence and do not replace the larger results below.

2026-07-12 startup-memory optimization smoke on the same two assets produced cold/warm elapsed times of 1,612/1,593 ms and cold/warm working sets of 59,084,800/58,777,600 bytes with Direct3D 11 rendering. The Release executable decreased from 55,243,537 to 5,327,267 bytes after embedded application fonts were removed. Relative to the preceding two-asset smoke, cold working set decreased by 151,527,424 bytes (71.9%) without increasing the measured settle time. This isolates the startup-memory mechanisms but still does not replace the representative-library gate.

## Hosted Windows evidence

2026-07-11 Windows 2025 / Qt 6.8.3 MSVC run 29147384340：

| Metric | Result |
|---|---:|
| Dataset | 10,000 copied valid PNG paths |
| Elapsed through settle window | 1,899 ms |
| Working set | 226,701,312 bytes |
| Peak working set | 226,701,312 bytes |

The same run built the 1,407-file MSVC portable bundle and passed MSI install/upgrade/launch/uninstall. Both local and hosted Windows results remain below the 5,000 ms / 512 MiB thresholds.

## Remaining performance gates

- Linux Release scan/RSS result on the deployed artifact.
- Interaction check while thumbnails are decoding on a heterogeneous large library.
- A representative scroll frame-interval baseline with enough rows to exercise virtualization.
- Tiled or region-decoded viewing for images whose useful zoom resolution exceeds the bounded 8,192-pixel viewer tier.
- Installer build installed-app measurement to rule out deployment-path regressions.
