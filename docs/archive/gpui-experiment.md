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
- Keyboard actions in `crates/piclens-gpui/src/actions.rs` (status bar shows a short hint)

### Keyboard (`PicLens` context)

| Keys | Action |
|------|--------|
| `Ctrl+O` | Open folder |
| `F5` / `Ctrl+R` | Refresh |
| `Alt+←` / `Alt+→` / `Backspace` | Folder history |
| `Ctrl+B` | Toggle sidebar |
| `Ctrl+1` / `Ctrl+2` | Toggle grid/list |
| `Ctrl+S` | Cycle sort |
| `Ctrl+Shift+S` | Toggle include subfolders |
| `Ctrl+F` / `/` | Focus search |
| `↑` `↓` `←` `→` | Move selection; viewer fit-zoom uses `←` `→` for images |
| `Enter` / `Space` | Open viewer or enter folder |
| `Esc` | Close overlay/viewer, else clear selection/search |
| `Ctrl+A` | Select all visible images |
| `Delete` | Trash selection (or current viewer image) |
| `F2` | Rename |
| `Ctrl+Shift+R` | Drop-rename plan |
| `Ctrl+J` / `Ctrl+W` | Convert visible to JPG / WebP |
| `Ctrl+Shift+C` | Same-basename cleanup |
| `Ctrl+Shift+E` | Reveal in file manager |
| `=` / `-` / `0` | Viewer zoom (or gallery thumb size) |
| `PageUp` / `PageDown` | Viewer prev/next |

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
