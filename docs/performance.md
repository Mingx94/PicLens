# Performance

Performance claims require a Release build, an isolated profile, and a representative image library. Debug timings, a short launch smoke, or a small unit test are not release evidence.

Development runs use optimized `image`, JPEG, WebP, and PNG/DEFLATE codec dependencies so a normal `cargo run` does not spend seconds in unoptimized pixel loops. Application code retains its normal debug profile and assertions. Record `buildProfile` from the metrics; a Release result does not establish Debug performance.

## Current safeguards

- The gallery uses `egui::ScrollArea::show_rows` virtualization.
- Folder scans and thumbnail decoding run on owned background workers.
- Thumbnail requests are limited to a bounded visible range and avoid duplicate pending work.
- One owned background task in the main process prunes old PNG cache files at startup and checks for new writes every five seconds. Decoder workers do not scan the cache. Clean intervals skip directory reads. Each pass keeps the newest 2,000 entries from its snapshot; writes between passes can temporarily exceed that target.
- Viewer images use safe 1024-pixel PNG previews. Background workers decode these into pixel buffers before handing them to egui textures. Sharp pixels paint at full opacity without another fade or UI-thread PNG decode.
- While the viewer covers the gallery, gallery thumbnail work is canceled and paused. It resumes on close. The viewer owns one request and reuses an in-flight adjacent prefetch when navigation selects it. After the current preview is ready, it prefetches only the next and previous static images, one at a time. It keeps at most three decoded previews (12 MiB of pixel data), validates source cache keys before reuse, and evicts GPU atlas entries when previews leave the cache or the viewer closes.
- Workers return `Event` values through a bounded channel and request an egui repaint after delivery.

These mechanisms reduce obvious blocking and unbounded work. They do not define a measured latency, memory, throughput, or frame-time guarantee.

## Measurement rules

```powershell
cargo build -p piclens-desktop --release --locked
$env:PICLENS_DATA_ROOT = "F:\PicLens\artifacts\desktop-performance"
cargo run -p piclens-desktop --release -- --folder <representative-folder>
```

Record the commit, locked egui/eframe versions, OS, CPU/GPU, storage type, image count and formats, cold or warm cache state, window size, and display scale. Exercise startup, first useful gallery content, sustained scrolling, search, folder navigation, viewer open, and shutdown. The built-in schema 2 metrics capture process CPU, working set, library, thumbnail, search, scroll, and viewer timings. Use an external profiler for GPU and frame behavior.

Run paint measurements with the app window visible. A hidden Windows launch can complete decoding without painting; null paint metrics from such a run are not evidence of meeting the target. A fresh PicLens profile makes the application cache cold, but does not flush the OS file cache. Restarting with the same profile tests the warm disk cache, not the in-process pixel cache.

Metrics schema 2 defines:

- `viewerPreviewReadyMilliseconds`: first successful selection to decoded safe preview pixels. Schema 1 stopped at PNG file readiness; do not compare the two as the same measurement.
- `viewerSharpPaintMilliseconds`: first successful selection to its first full-opacity sharp paint submission, after egui accepts the preview texture for that frame. This includes preview production, pixel decode, scheduling, and paint submission. It does not measure GPU completion or OS compositor presentation.
- `viewerSharpPaintMaxMilliseconds`, `viewerSharpPaintCount`, and `viewerSharpTargetMisses`: maximum, selection count, and count over the approved `viewerSharpTargetMilliseconds` value of 500. Repaints of one selection do not increment these counts. A missing paint is not a pass.
- `viewerOpenMilliseconds`: process metrics startup to viewer open, unchanged.

`piclens-desktop` 也會寫入 `frontEnd: "eframe-egui-wgpu"`。其 sharp-paint 時間止於 egui painter 接受 texture 的該次 frame。

The viewer has a 500ms target for its existing sharp preview quality. Report cold and warm cache results separately with the hardware and fixture. The target is not a universal guarantee for arbitrary files or storage. The app records misses but does not fail its exit code; `thresholdGateEnabled` remains false. Gallery latency, scrolling, and memory still have no approved numerical gate. Historical Qt measurements under `docs/archive/performance/` do not prove this checkout.

## Continuous viewer navigation

Single-image launches do not validate repeated navigation. Add `--performance-viewer` with `--viewer`, `--metrics`, and a visible window to step forward and backward in the same viewer. The workload holds each selection for 650ms, takes up to 64 steps in each direction, and checks the initial and final selections too. It uses the viewer controls' navigation method; it does not inject OS keyboard or mouse input.

使用相同 workload：

```powershell
cargo build -p piclens-desktop --release --locked
target\release\piclens-desktop.exe --folder <representative-folder> --viewer <image-in-folder> --performance-viewer --metrics <output.json> --smoke-ms 35000 --data-root <isolated-profile>
```

For a 23-image library, allow at least 35 seconds (`--smoke-ms 35000`) and require the completion log, 47 `viewerNavigationCheckedSelections`, 47 `viewerSharpPaintCount`, zero `viewerNavigationUnpaintedSelections`, and zero `viewerSharpTargetMisses`. A selection still showing its placeholder is a failure even if other images painted quickly. `viewerSharpPaintSamplesMilliseconds` retains the first 256 timings for distributions. Run Debug and Release separately if both are used; use isolated cold and warm profiles and verify source hashes.

## egui Viewer representative evidence — 2026-09-02

The Windows Release run used the user-supplied `D:\____iiirs` library at base commit `b53db313` plus the current working-tree metrics changes. The visible 1280×800 window used display scale 1.0. The library contained 207 images and 184.17 MiB: 129 JPG, 53 WebP, and 25 PNG. Its sorted file-name, byte-length, and file-content manifest SHA-256 was `bdf3b5f003273c1dfa41724fac585b41308b5822e8d10d0f0d73d084696eaa2c`.

The host used Windows 11 Home 10.0.26200 build 26200, an Intel Core i5-12400 with 6 cores and 12 logical processors, and an NVIDIA GeForce RTX 3060 Ti with driver 32.0.16.1656. Source images were on a 1 TB Crucial MX500 SATA SSD. The isolated PicLens profile and preview cache were on the 50 GiB file-backed virtual F drive.

| Release run | Library ready | Viewer open | First sharp paint | Maximum sharp paint | Painted / checked | Unpainted | Over 500ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Cold PicLens profile | 425ms | 425ms | 146ms | 146ms | 129 / 129 | 0 | 0 |
| Warm PicLens profile | 36ms | 36ms | 12ms | 12ms | 129 / 129 | 0 | 0 |

Both runs completed the same Viewer workload: up to 64 steps forward and backward plus the initial and final selections. The cold run used a fresh PicLens profile; it did not flush the Windows filesystem cache. The warm run reused that profile. Both app logs contained the workload completion event and no unpainted-selection warning. `thresholdGateEnabled` remained false.
