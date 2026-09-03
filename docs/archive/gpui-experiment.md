# GPUI migration snapshot

> Archived migration notes. Use [current documentation](../README.md) for commands and release status.

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

- Native File / Edit / View menus and image context menus (right-click)
- Bundled Noto Sans CJK TC fonts; theme is a GPUI global
- Shell render split under `crates/piclens-gpui/src/app/` (`gallery`, `shell`, `overlays`, `render`)
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

## Frozen v0.1.2 behavior baseline

This baseline was reconstructed on 2026-09-03. The migration baseline is annotated tag `v0.1.2`, commit `c50c5fe4`, with locked GPUI revision `c7537bdf463a998e7ec636adff33b198891e69ed`. It was rebuilt in a detached worktree without restoring GPUI to the current branch. This command passed 50 tests with zero failures:

```powershell
cargo test -p piclens-gpui --locked
```

| Area | Recorded GPUI behavior | Reproducible evidence |
| --- | --- | --- |
| Startup and folder selection | `--folder` opens a temporary folder without changing the saved startup authority. The folder picker replaces the tree root; tree navigation does not. | Visible Release smoke plus `folder_tree::picker_replaces_tree_navigation_does_not`. |
| Search | Search filters the loaded projection by name or path. `--search jpg` projected 129 of 207 images in the Release run. | Release metrics and `cli::parses_supported_overrides`. |
| Sort | `Ctrl+S` cycles name ascending, name descending, modified ascending, and modified descending. Direct menu actions select the same four states. | `list_sorter` natural-name and modified-time tests plus the action bindings. |
| Selection | Pointer, keyboard, Ctrl, Shift, and Ctrl+Shift share selected paths, order, and anchor. | `pointer_keyboard_and_accessibility_share_selection_state` and the selection interaction tests. |
| Viewer | Enter or Space opens the selected image. Arrow or Page keys navigate the immutable sequence. Zoom, preview loading, adjacent prefetch, stale-result rejection, and texture release use the recorded v0.1.2 paths. | Viewer unit and GPUI tests plus the Release navigation workload below. |
| Rename | `F2` requires exactly one selected image, opens the rename dialog, then calls the cancellable single-image rename path. | Action binding, `commit_rename`, and domain filename／rename-plan tests. |
| Trash | Delete opens explicit confirmation for the current selection or Viewer image. Escape cancels without changing the source files. | `trash_escape_cancels_without_modifying_files`. This is not native Recycle Bin success evidence. |
| JPEG conversion | `Ctrl+J` converts the current visible results through `convert_to_jpg_cancellable`; 50 or more items require confirmation, and Escape cancels without changing source files. | `conversion_confirmation_threshold_starts_at_fifty` and `large_conversions_escape_without_modifying_files`. This is a behavior contract, not a codec output smoke. |

The file-operation rows record the exact UI route and safety contract at the migration point. They do not claim a successful native Recycle Bin, rename, or conversion lifecycle. Those platform checks remain separate from this historical comparison baseline.

### Reproduced Release metrics

The visible Windows Release runs used `D:\____iiirs`: 207 images and 184.17 MiB, containing 129 JPG, 53 WebP, and 25 PNG files. The manifest SHA-256 was `e1eb73ad365cf1f222f5ad73259a5c0c1198423a230fcdccb4fe86e640c6f13c`; it hashes sorted records of relative path, byte length, and file SHA-256. All runs used isolated GPUI profiles, a 1280×800 window, and display scale 1.0.

| Viewer Release run | Library ready | Viewer open | First sharp paint | Maximum sharp paint | Painted / checked | Unpainted | Over 500ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Cold GPUI profile | 449ms | 491ms | 123ms | 123ms | 129 / 129 | 0 | 0 |
| Warm GPUI profile | 417ms | 455ms | 17ms | 22ms | 129 / 129 | 0 | 0 |

The Viewer workload completed in both runs. The screenshots showed the expected Gallery and Viewer surfaces, and the app logs contained no warning or error. GPUI wrote `window not found` to stderr after each Viewer smoke closed; this happened after workload completion and metrics output, so it remains a shutdown diagnostic rather than a missing paint.

The Gallery runs loaded and searched the same library, produced thumbnails, and saved valid screenshots. They did not produce a continuous-scroll result: `continuousScrollMilliseconds` stayed `null`, and the log had a start event without a completion event. A second cold run without screenshot capture and with a 12-second deadline had the same result. Source inspection indicates that the representative library starts enough thumbnail tasks for `async_tasks` trimming to drop the earlier scroll task. Changing that code would no longer measure the frozen `v0.1.2` baseline. Therefore the historical Gallery performance checklist remains incomplete.

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
