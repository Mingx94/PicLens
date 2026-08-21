---
name: animation-performance
description: Diagnose and improve GPUI animation frame performance in desktop apps. Use for stutter, dropped frames, excess rerendering, long frame steps, idle frame loops, heavy per-frame work, or motion that degrades with large image collections.
metadata:
  short-description: Keep GPUI motion within its frame budget
---

# GPUI Animation Performance

Profile the real app with production-like images and release-like settings. A clean desktop on one machine is not proof.

## Diagnose in order

1. Confirm that frames are requested only while presentation changes.
2. Inspect the animation callback for file I/O, decoding, parsing, sleeps, locks, logging, and unbounded allocation.
3. Check whether one animated value rerenders a large view or list that can be isolated.
4. Check that long stalls are capped or subdivided so spring integration stays stable.
5. Verify image decode, thumbnails, and other CPU-heavy work use background execution and update live entities safely.
6. Check that lists use existing virtualization and that stable geometry or assets are not rebuilt each frame.
7. Test resize, high DPI, inactive/reactivated windows, blur/material paths, and 60/120 Hz displays when available.

Do not import browser rules such as "transform is always GPU-only" into GPUI. Measure the pinned renderer and target platform. Prefer simple presentation changes and bounded paint work, but base findings on profiles or clear code evidence.

Animation must remain cancellable and input-responsive under load. Read [the GPUI motion reference](../build-gpui-apps/references/motion-input.md) and [the async performance reference](../build-gpui-apps/references/async-performance.md).
