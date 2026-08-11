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

## Gaps vs product-spec (still open)

- Visible-only thumb scheduling (current queue is visible list order, not true viewport culling)
- Pointer pan in viewer; wheel zoom anchor
- Full folder-tree expand/collapse depth
- Native drag-and-drop rename (selection-based plan is the interim UX)
- MSI/DEB packaging for the Rust binary

Close gaps in layers. Keep the app building after each layer.
