# Performance

Performance claims require a Release build, an isolated profile, and a representative image library. Debug timings, a short launch smoke, or a small unit test are not release evidence.

## Current safeguards

- The gallery uses GPUI `list` virtualization.
- Folder scans and thumbnail decoding run on the background executor.
- Thumbnail requests are limited to a bounded visible range and avoid duplicate pending work.
- The disk thumbnail cache has a bounded entry count and prunes old PNG files.
- Viewer images use cached thumbnails with a larger requested size instead of decoding during render.
- Task results update live GPUI state through the application context.

These mechanisms reduce obvious blocking and unbounded work. They do not define a measured latency, memory, throughput, or frame-time guarantee.

## Measurement rules

```powershell
cargo build -p piclens-gpui --release --locked
$env:PICLENS_DATA_ROOT = "F:\PicLens\artifacts\gpui-performance"
cargo run -p piclens-gpui --release -- --folder <representative-folder>
```

Record the commit, locked GPUI revision, OS, CPU/GPU, storage type, image count and formats, cold or warm cache state, window size, and display scale. Exercise startup, first useful gallery content, sustained scrolling, search, folder navigation, viewer open, and shutdown. Capture latency, peak memory, CPU, and frame behavior with an external profiler until the app has its own metrics.

There is no automated GPUI performance gate or accepted threshold yet. Historical Qt measurements under `docs/archive/performance/` do not prove this checkout.
