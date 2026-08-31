# Performance

Performance claims require a Release build, an isolated profile, and a representative image library. Debug timings, a short launch smoke, or a small unit test are not release evidence.

## Current safeguards

- The gallery uses GPUI `list` virtualization.
- Folder scans and thumbnail decoding run on the background executor.
- Thumbnail requests are limited to a bounded visible range and avoid duplicate pending work.
- One owned background task in the main process prunes old PNG cache files at startup and checks for new writes every five seconds. Decoder workers do not scan the cache. Clean intervals skip directory reads. Each pass keeps the newest 2,000 entries from its snapshot; writes between passes can temporarily exceed that target.
- Viewer images use safe 1024-pixel PNG previews. The background executor decodes these into BGRA pixels before handing them to GPUI. Sharp pixels paint at full opacity without another fade or asynchronous PNG resource load.
- While the viewer covers the gallery, gallery thumbnail work is canceled and paused. It resumes on close. The viewer owns one request and reuses an in-flight adjacent prefetch when navigation selects it. After the current preview is ready, it prefetches only the next and previous static images, one at a time. It keeps at most three decoded previews (12 MiB of pixel data), validates source cache keys before reuse, and evicts GPU atlas entries when previews leave the cache or the viewer closes.
- Task results update live GPUI state through the application context.

These mechanisms reduce obvious blocking and unbounded work. They do not define a measured latency, memory, throughput, or frame-time guarantee.

## Measurement rules

```powershell
cargo build -p piclens-gpui --release --locked
$env:PICLENS_DATA_ROOT = "F:\PicLens\artifacts\gpui-performance"
cargo run -p piclens-gpui --release -- --folder <representative-folder>
```

Record the commit, locked GPUI revision, OS, CPU/GPU, storage type, image count and formats, cold or warm cache state, window size, and display scale. Exercise startup, first useful gallery content, sustained scrolling, search, folder navigation, viewer open, and shutdown. Capture latency, peak memory, CPU, and frame behavior with an external profiler until the app has its own metrics.

Run paint measurements with the app window visible. A hidden Windows launch can complete decoding without painting; null paint metrics from such a run are not evidence of meeting the target. A fresh PicLens profile makes the application cache cold, but does not flush the OS file cache. Restarting with the same profile tests the warm disk cache, not the in-process pixel cache.

Metrics schema 2 defines:

- `viewerPreviewReadyMilliseconds`: first successful selection to decoded safe preview pixels. Schema 1 stopped at PNG file readiness; do not compare the two as the same measurement.
- `viewerSharpPaintMilliseconds`: first successful selection to its first full-opacity sharp paint submission, after GPUI accepts the image in its sprite atlas. This includes preview production, pixel decode, scheduling, and the `paint_image` call. It does not measure GPU completion or OS compositor presentation.
- `viewerSharpPaintMaxMilliseconds`, `viewerSharpPaintCount`, and `viewerSharpTargetMisses`: maximum, selection count, and count over the approved `viewerSharpTargetMilliseconds` value of 500. Repaints of one selection do not increment these counts. A missing paint is not a pass.
- `viewerOpenMilliseconds`: process metrics startup to viewer open, unchanged.

The viewer has a 500ms target for its existing sharp preview quality. Report cold and warm cache results separately with the hardware and fixture. The target is not a universal guarantee for arbitrary files or storage. The app records misses but does not fail its exit code; `thresholdGateEnabled` remains false. Gallery latency, scrolling, and memory still have no approved numerical gate. Historical Qt measurements under `docs/archive/performance/` do not prove this checkout.
