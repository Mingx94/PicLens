# GPUI full migration experiment

Branch: `experiment/gpui`

## Purpose

Full product migration of PicLens from Qt 6 / QML to **GPUI + gpui-component**, targeting [product-spec](../product-spec.md) and [runtime-invariants](../runtime-invariants.md) parity.

The Qt tree remains in the repo on this branch for comparison and rollback. It is not the experiment runtime.

## Run

Requirements: **Rust nightly** (see `rust-toolchain.toml`; current Zed GPUI uses unstable APIs), Windows or Linux desktop stack suitable for GPUI.

```powershell
# From repo root (toolchain file selects nightly)
cargo run -p piclens-gpui --release

# Optional folder
cargo run -p piclens-gpui -- --folder D:\Photos
```

## Build layers (AGENTS.md)

Ship a working layer, then add the next:

1. Window + open folder + sorted list (current)
2. Search, sidebar tree, selection
3. Thumbnails (async, visible-only)
4. Viewer (zoom/pan, prev/next)
5. File ops (trash, rename, convert, drop-rename)
6. Settings continuity + packaging cutover

Isolate profile data:

```powershell
$env:PICLENS_DATA_ROOT = "F:\PicLens\artifacts\gpui-profile"
cargo run -p piclens-gpui
```

Settings path remains `piclens-settings.json` under the app data root (compatible field names with the Qt app).

## Layout

| Crate | Role |
|-------|------|
| `crates/piclens-domain` | Pure rules: formats, sort, settings, zoom, rename plans |
| `crates/piclens-infra` | Scan, settings store, trash/reveal, convert, thumbs, log |
| `crates/piclens-gpui` | GPUI shell: library, sidebar, viewer, operations |

Agent skills: `.agents/skills/{gpui,gpui-component,gpui-component-dev}`.

## Status

Experiment implementation of the full feature surface. Packaging (MSI/DEB) and Qt deletion are cutover steps, not required to develop on this branch.

## Qt production

```powershell
cmake --preset debug
cmake --build --preset debug
```
