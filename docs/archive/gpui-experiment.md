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

## Gaps vs product-spec (still open)

- Grid gallery with real decoded image tiles (async thumbs in UI)
- Drop-target batch rename UX
- Full folder-tree expand/collapse depth
- MSI/DEB packaging for the Rust binary
- Viewer pixel canvas pan/zoom (logic exists; display is path/status based for now)

Close gaps in layers. Keep the app building after each layer.
