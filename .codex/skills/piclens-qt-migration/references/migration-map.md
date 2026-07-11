# Migration map

## Authority order

Use these sources in descending order when code and prose disagree:

1. Explicit current user decision.
2. `docs/product-spec.md` for product intent.
3. `docs/runtime-contract.md` for committed desktop behavior.
4. Tests for executable characterization.
5. `docs/architecture.md` and implementation for current engineering structure.

Do not infer new product behavior from framework defaults.

## Final cutover layout

The repository root is the Qt/CMake project root:

```text
CMakeLists.txt
CMakePresets.json
src/core/
src/infrastructure/
src/presentation/
src/app/
qml/PicLens/
tests/
scripts/
packaging/
assets/
```

Legacy Avalonia/.NET trees, the early PoC and rollback packaging entry points were removed after explicit authorization. Do not recreate a nested temporary `qt/` project root.

## Slice order

Prefer this dependency order, but take a smaller user-requested slice when it is independently testable:

1. Build foundation: CMake presets, targets, resources, app-data paths, logging, test runner.
2. Pure rules: formats, animation detection, path comparison, sorting, settings merge, zoom math, rename planning.
3. Infrastructure: settings, direct/recursive scanning, canonical-directory deduplication, file operations, OS trash/reveal.
4. Library state: navigation history, sorting, recursive mode, folder tree, status and stale-selection clearing.
5. Thumbnail pipeline: visible requests, cancellation, timeout isolation, disk cache and pruning.
6. Gallery interaction: virtualized mixed folder/image model, selection, context menu, keyboard and mouse navigation.
7. Inline viewer: immutable sequence, image navigation, zoom/pan, animated-image feedback and focus restoration.
8. Drag/drop rename: threshold, preview, target highlighting, autoscroll, confirmation and execution.
9. Release and cutover: portable output, installers, platform smoke, legacy removal and documentation cleanup.

## Existing-to-target mapping

| Existing area | Qt target | Removal gate |
|---|---|---|
| `PicLens.Core` | `src/core` | Equivalent deterministic Qt tests pass |
| `PicLens.Infrastructure` | `src/infrastructure` | Filesystem side effects and diagnostics match on Windows/Linux |
| `PicLens.Presentation` | `src/presentation` | State transitions and 10,000-item behavior are covered |
| AXAML and view code-behind | `qml/PicLens` plus `src/app` adapters | Qt UI tests and real smoke cover the runtime contract |
| Avalonia headless tests | Qt Test and Qt Quick Test | Applicable behaviors have replacements; platform-only gaps are documented |
| `Tasks.cs` release paths | root CMake, `scripts/` and CPack | Portable and installer outputs pass platform smoke |

## Per-slice parity record

For each slice, capture:

- Contract clauses and affected files.
- Test fixtures reused or recreated.
- Qt implementation and automated checks.
- Windows and Linux differences.
- Diagnostics and failure behavior.
- Performance or memory evidence when relevant.
- Remaining legacy callers and the explicit deletion gate.

Keep this record in the implementation plan, issue, or durable migration document chosen by the project. Do not create a new report file for every small edit.
