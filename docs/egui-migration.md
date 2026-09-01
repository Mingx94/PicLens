# egui migration design

Status: accepted for incremental implementation on 2026-09-01.

This document defines the active GPUI-to-egui migration. It does not change the product contract. [Product specification](product-spec.md) and [runtime invariants](runtime-invariants.md) remain authoritative.

## Target structure

```text
crates/piclens-domain/     product rules and framework-light models
crates/piclens-infra/      filesystem, settings, image and OS adapters
crates/piclens-desktop/    egui/eframe shell and composition root
crates/piclens-gpui/       old shell, retained until parity and cutover
```

The target dependency direction is `piclens-desktop -> piclens-infra -> piclens-domain`. UI framework types must not enter domain or infrastructure public contracts unless an adapter cannot reasonably avoid them.

## Runtime flow

PicLens adopts the event-driven pattern described by Fastpotify's [architecture overview](https://github.com/crmne/fastpotify#how-it-is-built). Its relevant source examples are [model.rs](https://github.com/crmne/fastpotify/blob/main/src/model.rs), [backend.rs](https://github.com/crmne/fastpotify/blob/main/src/backend.rs), and [ui/mod.rs](https://github.com/crmne/fastpotify/blob/main/src/ui/mod.rs).

```text
egui view -> Action -> App reducer -> Command -> background backend
                   ^                         |
                   +--------- Event <-------+
```

- A view reads model state and appends `Action` values. It does not perform filesystem access, image decode, process launch, or persistent writes.
- The App applies actions after drawing. An action can update local state, open a dialog, or emit a backend command.
- A `Command` contains all identity needed to reject late work. It uses a monotonically increasing generation for collection-wide replacement and a request ID for individual work.
- The backend owns blocking work and bounded queues. It sends `Event` values to the App and calls `egui::Context::request_repaint()` after each successful send.
- The App drains events in `eframe::App::logic`, validates generation and request identity, and then updates model state.
- Idle workers block on channel receive. The UI requests timed repaint only for a real deadline, animation, or unfinished operation.

## Decisions

### Frontend crate

The frontend is named `piclens-desktop`, not `piclens-egui`. The crate is a desktop product shell; egui remains an implementation detail. It is a workspace member during migration, but `piclens-gpui` remains the default member until cutover.

### egui version and renderer

The first implementation pins egui and eframe 0.36.1. eframe 0.36 uses separate `App::logic` and `App::ui` phases and makes wgpu its default renderer. PicLens enables only wgpu, AccessKit, Wayland, and X11 support. Glow is not enabled. Windows and Linux runtime verification is still required before the renderer decision is final.

### Background execution

The initial backend uses blocking standard-library channels and explicitly owned threads. PicLens work is primarily filesystem traversal, image decode, and helper processes. Tokio is not added without a concrete async service that benefits from it.

The production backend will reuse the existing cancellation and bounded decoder rules. It may own a small coordinator thread plus the existing bounded workers. It must not create one unbounded thread per thumbnail or file.

### State ownership

| Owner | State |
|---|---|
| `model.rs` | Page, loaded data, selection, dialogs, status, viewer snapshot |
| `app/` | Action reducer, generation counters, request identity, frame lifecycle |
| `backend.rs` | Command/event channels, worker ownership, shutdown |
| `images.rs` | Thumbnail keys, image-loader entries, texture lifetime |
| `ui/` | Immediate layout, hover response and per-frame action collection |
| `piclens-infra` | Scan, settings JSON, cache files, decode, trash, reveal and conversion |
| `piclens-domain` | Sort, path, zoom, rename and file-operation rules |

Persistent product state must not be hidden in egui widget memory. Short-lived widget state can use stable egui IDs.

## Migration safety

- `piclens-gpui` remains runnable until the egui package passes parity review.
- Both frontends read the same normalized settings schema and data-root override.
- Only one frontend runs against a profile during a smoke or migration test.
- Tests that rename, trash, or convert use a disposable copied fixture.
- GPUI code is removed only after package scripts, CI, documentation, and release metadata point to `piclens-desktop`.

## Feature parity inventory

| Surface | Required behavior | Main authority | Target owner |
|---|---|---|---|
| Startup | Restore last picker folder; empty state when unavailable | Product specification | App + infra settings |
| Folder navigation | Stable tree root, history, side buttons, refresh | Product specification | Model + gallery UI |
| Gallery | Fixed grid, visible-only tiles, 10,000-item projection | Runtime invariants | Gallery UI + backend |
| Search and sort | In-memory search, four sort modes, natural order | Product specification | Domain + model |
| Selection | Click, Ctrl, Shift, Ctrl+Shift, order and anchor | Runtime invariants | Model + gallery actions |
| Thumbnails | Bounded workers, cancellation, timeout and cache pruning | Runtime invariants | Backend + image loader |
| Viewer | Immutable sequence, navigation, zoom, pan and focus return | Runtime invariants | Viewer model + UI |
| Viewer previews | One active job, adjacent preload, three textures/12 MiB | Runtime invariants | Backend + image loader |
| Animation formats | Identify GIF/WebP animation; show unsupported state | Product specification | Infra + gallery/viewer UI |
| Context actions | Reveal, rename and trash with correct selection scope | Product specification | Actions + dialogs |
| Conversion | JPG, lossless WebP and same-basename cleanup | Runtime invariants | Infra + backend |
| Batch results | Continue on failure and show per-item results | Product specification | Backend + result dialog |
| Drag rename | Threshold, preview, target, sequence plan and confirm | Runtime invariants | Gallery UI + domain |
| Settings | Atomic compatible JSON, quarantine corrupt input | Runtime invariants | Infra settings |
| Diagnostics | Context-rich startup, navigation, image and file-op logs | Runtime invariants | App + backend + infra |
| Accessibility | Names, roles, states, actions and focus restoration | Product/testing docs | All UI modules |
| Packaging | MSI, portable, DEB and RPM paths | Release docs | Scripts + CI |

## Evidence still required

- A repeatable GPUI behavior baseline for the main product flow.
- GPUI Release measurements for gallery and viewer workloads.
- A wgpu startup and image-texture smoke on clean Windows, Ubuntu, and Fedora environments.
- Real accessibility and input checks. Headless render tests do not replace these checks.

## Local implementation evidence

On 2026-09-01, `piclens-desktop` passed its scoped check, seven unit/headless tests, and clippy with warnings denied. An isolated wgpu app smoke reached App creation and the background smoke deadline. The background desktop session did not schedule another eframe pass for the requested viewport close, so the smoke harness used its logged two-second fallback and exited with status 0. This proves startup and the background deadline path, but it is not evidence of graceful close or clean Windows/Linux compositor behavior. Those exit and platform checks remain open.
