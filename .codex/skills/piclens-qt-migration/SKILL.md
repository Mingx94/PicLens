---
name: piclens-qt-migration
description: Migrate PicLens from Avalonia/.NET to Qt 6, C++20, and Qt Quick while preserving documented product behavior, performance safeguards, tests, Windows/Linux support, and release outputs. Use for migration planning, porting a feature slice, implementing or reviewing C++/QML architecture, replacing Avalonia services or UI, adding Qt tests, benchmarking migration parity, changing build or packaging, and removing legacy .NET code at cutover.
---

# PicLens Qt Migration

Migrate PicLens through testable vertical slices. Preserve the product contract; do not translate Avalonia classes or AXAML mechanically.

## Establish the source of truth

1. Read the nearest `AGENTS.md` and `docs/README.md`.
2. Read `docs/product-spec.md`, `docs/runtime-contract.md`, `docs/architecture.md`, `docs/testing.md`, and `DESIGN.md` before changing behavior or UI.
3. Read the release documents before modifying deployment or packaging.
4. Inspect the current worktree and preserve unrelated user changes.
5. Check the installed Qt version and use current official `doc.qt.io` documentation for version-sensitive Qt, QML, CMake, deployment, and platform APIs.

## Route the task

- Read [references/migration-map.md](references/migration-map.md) when choosing a slice, mapping existing code, or deciding what can be removed.
- Read [references/qt-architecture.md](references/qt-architecture.md) for any C++, QML, threading, model/view, image, or filesystem implementation.
- Read [references/verification.md](references/verification.md) when adding tests, measuring parity, or deciding whether a slice is complete.
- Read [references/release-cutover.md](references/release-cutover.md) for installation, portable builds, platform integration, licensing, or final legacy removal.

## Migrate one vertical slice

1. Name the user-visible behavior and its authoritative product/runtime requirements.
2. Inventory the corresponding domain, infrastructure, presentation, Avalonia view, tests, and release dependencies.
3. Record a compact parity checklist with supported, intentionally deferred, and blocked items. Do not silently narrow behavior.
4. Add or retain characterization tests before porting any ambiguous rule.
5. Implement the smallest production Qt slice. Treat `poc/qtquick` as evidence and reusable experiments, not the production architecture.
6. Add Qt-side automated coverage at the lowest useful layer.
7. Keep the Avalonia app and its tests working until the Qt replacement for that slice passes its parity gate.
8. Update engineering documents when commands, layout, architecture, or coverage change. Update product/runtime documents only for an intentional product decision.
9. Run the proportionate legacy and Qt verification matrix.

Do not delete a legacy component merely because its Qt replacement compiles. Delete it only when its behavior, tests, diagnostics, platform integration, and packaging consumers have moved.

## Preserve boundaries

- Keep domain rules deterministic and independent of Qt Quick/QML.
- Keep filesystem, settings, thumbnails, logging, trash, reveal, and process integration outside QML.
- Expose UI state through typed C++ objects and `QAbstractItemModel`; keep QML declarative.
- Keep one owner for navigation, selection, viewer sequence, and file-operation state.
- Make cross-thread results generation-aware and deliver UI-bound changes on the owning thread.
- Preserve conservative file operations: never replace trash with permanent deletion and never overwrite collisions.
- Preserve Traditional Chinese (Taiwan) runtime copy unless the product specification changes.

## Guard performance

- Never decode full images on the GUI thread for the thumbnail grid.
- Virtualize and reuse delegates; request thumbnails only for visible or buffered still-image tiles.
- Bound concurrency, support cancellation, prevent stalled decoders from starving later requests, and bound the disk cache.
- Prevent stale scan or thumbnail results from overwriting newer navigation state.
- Compare Release builds on representative large folders. Do not claim improvement from framework choice alone.

## Verify and report

Build and test both implementations while they coexist. Run QML lint and a short real runtime launch in addition to compilation. Inspect logs for runtime failures.

Lead the report with the migrated behavior and parity status. List checks performed, remaining legacy dependencies, deliberate deviations, and the next safe slice. Never report the full migration complete while required runtime or release behavior remains on the legacy path.
