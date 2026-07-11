# Qt architecture and implementation rules

## Target stack

- Use C++20, Qt 6.5 or newer, CMake, Qt Quick, and Qt Quick Controls 2.
- Verify against the installed Qt version and official Qt documentation before using version-sensitive APIs.
- Prefer Qt Test and Qt Quick Test before adding another test framework.
- Keep Qt Widgets out of the main shell unless a platform integration has no suitable Qt Quick path and the exception is documented.

## Dependency direction

```text
QML views -> presentation models/controllers -> core contracts/rules
                                          -> infrastructure adapters
```

- `core` must not depend on Qt Quick, QML, GUI controls, filesystem dialogs, or platform windows.
- `infrastructure` owns filesystem, JSON, cache, logging, process and OS integration.
- `presentation` owns observable application state, commands, histories, selections and immutable viewer sequences.
- `app` composes services, registers QML types/providers and owns platform lifecycle glue.
- QML formats and binds state; it does not scan directories, mutate files, persist settings, or implement domain rules.

## C++ and QML surface

- Expose collections with `QAbstractListModel` or `QAbstractItemModel` and stable role names.
- Prefer registered QML types, required properties, and explicit ownership over broad context properties.
- Add `pragma ComponentBehavior: Bound` where delegates intentionally use outer IDs; keep `qmllint` clean.
- Keep delegate-local transient visual state minimal. Reset retained state on reuse.
- Represent commands as invokable methods or typed controller objects with clear enabled, busy, and result state.
- Do not expose mutable raw pointers or thread-owned objects to QML.
- Define QObject ownership. Avoid parentless long-lived objects and engine/provider lifetime ambiguity.

## Concurrency and cancellation

- Keep GUI-owned QObjects on the GUI thread.
- Run scanning, image decode, hashing and file batches in bounded worker pools.
- Pass immutable values into workers. Return results through queued signals, watchers, or queued invocations.
- Attach a generation or request identifier to navigation scans and thumbnail work; ignore stale results.
- Cancellation must stop future scheduling and suppress obsolete UI updates even when a decoder cannot be interrupted.
- A single slow decode must not serialize all later thumbnail requests. Bound worker concurrency and enforce the runtime timeout contract above the decoder.

## Gallery and thumbnails

- Use a virtualized `GridView` or equivalent with delegate reuse enabled.
- Keep source file paths, identity, type, and selection in the model, not in delegate-local state.
- Request still-image thumbnails when tiles become visible or materialized; release or cancel when they leave the retained viewport.
- Use `QQuickAsyncImageProvider` or an equivalent async texture path backed by a bounded pool.
- Decode near the requested device-pixel size and apply EXIF orientation.
- Keep generated thumbnails in the product-specified bounded disk cache. Include source identity and modification data in cache keys.
- Treat decode failures as per-item failures with logging and a usable placeholder.
- Never attempt animated GIF/WebP playback until product and runtime contracts change.

## Navigation and selection

- Keep one presentation owner for the current folder and back/forward history; mouse side buttons call the same operations.
- Keep one selection owner. QML delegates render that state and forward gestures; they do not maintain an independent selection set.
- Clear selection on every contract-defined invalidation path.
- Implement Explorer-like right-click scope before opening the image context menu.
- Keep the viewer navigation list immutable for the lifetime of the open viewer.

## File and platform integration

- Use canonical-path identity for recursive scan deduplication while preserving display paths.
- Match Windows case-insensitive and Linux case-sensitive path behavior defined by the contract.
- Send Windows removals to Recycle Bin and Linux removals through a real trash API such as GIO; never fall back to permanent deletion.
- Reveal and folder-dialog behavior may use platform adapters, but expose one presentation contract.
- Log source, target, operation, and reason for individual failures without logging sensitive file contents.

## Visual implementation

- Read `DESIGN.md` for every Qt Quick UI task.
- Recreate semantic tokens as shared QML singletons or components rather than scattering literals.
- Preserve compact desktop density, image-first hierarchy, Traditional Chinese copy, keyboard access, visible focus, and non-color state cues.
- Implement all applicable default, hover, pressed, focused, selected, disabled, loading, empty, and error states.
- Prefer restrained motion and direct zoom, pan, selection, and drag feedback.
