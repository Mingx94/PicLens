# GPUI migration branch

Branch: `experiment/gpui`

## Purpose

Full migration of PicLens to **GPUI + gpui-component**, targeting [product-spec](../product-spec.md) and [runtime-invariants](../runtime-invariants.md).

Qt sources are **removed on this branch**. Recover Qt from `main` if needed.

## Run

Rust nightly (`rust-toolchain.toml`):

```powershell
cargo run -p piclens-gpui --release
cargo run -p piclens-gpui -- --folder D:\Photos
```

## Crates

| Crate | Role |
|-------|------|
| `piclens-domain` | formats, sort, settings, zoom, rename plans |
| `piclens-infra` | scan, settings store, trash/reveal, convert, thumbs, log |
| `piclens-gpui` | library UI, sidebar, viewer, file ops |

## Current UI

- Grid / list gallery with async disk-cache thumbnails (`ensure_thumbnail` + `img`)
- Viewer shows real image via `img` (zoom scales layout size; pan still light)
- Drop-rename plan/confirm: multi-select sources then last item as target → preview → apply
- Search, selection, sidebar children, file ops (trash, convert, rename, cleanup)

## Stability notes

- Thumbs are **not** scheduled from `render` (avoids RefCell / stack faults).
- Decode uses content-type guessing + panic isolation; corrupt files warn and skip.
- Viewer only feeds `img()` with successfully decoded PNG cache paths.
- Smoke: `--smoke-ms N` quits after N ms for automated launch checks.

## Gaps vs product-spec (non-goals for packaging cutover)

- Visible-only viewport culling
- Pointer pan / wheel zoom anchor polish
- Deep folder-tree expand
- Native OS drag-drop rename
- MSI/DEB packaging

Close gaps in layers. Keep the app building after each layer.
