# Verification and parity gates

## Test layers

| Layer | Purpose |
|---|---|
| Pure Qt Test | Deterministic core rules and boundary cases |
| Infrastructure Qt Test | Temporary filesystem, settings, cache, logs, and file-operation side effects |
| Presentation Qt Test | State transitions, signals, histories, selection, commands, and failure results |
| Qt Quick Test | QML component states, bindings, keyboard/pointer behavior where reliable |
| Real runtime smoke | Renderer, native dialogs, focus, context menus, platform trash/reveal, and deployment |

Do not test QML by reading source text. Assert observable behavior or rendered/runtime state.

## Coexistence gate

While legacy code exists, run the relevant existing checks before removing or changing its behavior:

```powershell
dotnet run Tasks.cs test
dotnet run Tasks.cs ui-test
```

Use the smallest applicable subset during iteration, then the complete relevant suite before declaring a slice migrated.

## Qt build gate

Prefer checked-in CMake presets once the production `qt/` tree exists. Until then, the expected shape is:

```powershell
cmake -S qt -B qt/build -G Ninja -DCMAKE_BUILD_TYPE=Debug
cmake --build qt/build
ctest --test-dir qt/build --output-on-failure
cmake --build qt/build --target <qml-lint-target>
```

Also configure and build Release before performance conclusions or packaging work. Build on Windows and Linux rather than assuming cross-platform compilation proves runtime support.

## Contract parity checklist

For an affected slice, verify applicable items from:

- Supported formats and animated GIF/WebP rejection.
- Last selected folder semantics and navigation history.
- Folder-first natural sorting and recursive scanning deduplication.
- Selection scope, stale-selection clearing, and context command gating.
- Conservative rename, conversion, trash, and collision behavior.
- Immutable viewer sequence, zoom/pan math, Escape, and focus restoration.
- Visible-only thumbnails, cancellation, concurrency, timeout recovery, disk cache, and pruning.
- Traditional Chinese status, warning, and failure copy.
- ERROR logging with useful source, target, and reason context.

## Performance evidence

- Measure Release builds only after warm-up behavior is understood.
- Use representative folders, including 10,000 mixed items and several large or malformed images.
- Record machine, OS, Qt version, renderer backend, dataset shape, and cold/warm cache state.
- Measure scan completion, first visible thumbnails, interaction stalls, peak/steady memory, and cache growth.
- Exercise rapid navigation, repeated thumbnail resizing, long scrolling, and viewer open/close cycles.
- Confirm obsolete work does not continue producing visible updates.
- Compare with the existing Avalonia build when claiming a regression or improvement.

The current product specification does not define numeric FPS or load-time acceptance thresholds. Establish and document a baseline before introducing a hard gate; do not invent one inside an implementation change.

## Completion standard

A slice is complete only when:

- Its contract behavior is implemented or explicitly deferred with user agreement.
- Automated coverage exists at appropriate layers.
- Debug and Release builds succeed.
- QML lint is clean for affected modules.
- A short real runtime launch has no relevant warnings or ERROR log entries.
- Windows/Linux differences are verified or called out.
- Legacy dependencies and their deletion gate are known.
