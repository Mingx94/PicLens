# Qt Migration

PicLens 正以可驗證的垂直切片從 Avalonia/.NET 遷移到 Qt 6、C++20 與 Qt Quick。遷移期間完整 Avalonia app 維持可建置、可測試；`poc/qtquick` 只保留為 API 與效能實驗，正式 production code 位於 `qt/`。

## Target layout

```text
qt/
  CMakeLists.txt
  CMakePresets.json
  src/core/
  src/infrastructure/        persistence、scanner、file/platform operations
  src/presentation/          library/folder-tree models and async state
  src/app/                   startup、service composition、production executable
  qml/PicLens/               Qt Quick shell、theme 與 reusable controls
  tests/
```

## Slice status

| Slice | Status | Evidence / remaining gate |
|---|---|---|
| CMake, presets, static core target, Qt Test/CTest | Complete | Debug and Release configure/build/CTest pass on Windows Qt 6.11.1 |
| Supported image extension gate | Complete | Qt parity data covers JPG/JPEG/PNG/BMP/WebP/GIF and unsupported inputs |
| Folder-first natural sorting | Complete | Numeric order and leading-zero Explorer-style cases pass |
| OS path equality and basename collision rules | Complete | Windows case-insensitive behavior and target conflicts pass |
| Settings merge and thumbnail-size normalization | Complete | Existing xUnit cases are represented in Qt Test |
| Zoom clamp, reset, and pointer-anchor math | Complete | Qt numeric parity tests pass |
| Drop-target rename validation and planning | Complete | Extension preservation, target exclusion, basename reservation, gap compaction, and skip behavior pass |
| Image sequence and batch result value models | Complete | Value semantics and result counts pass |
| App-data paths and `PICLENS_DATA_ROOT` | Complete | Platform default paths and isolated override pass |
| JSON settings persistence | Complete | Missing/default, corrupt quarantine, atomic update, normalization, and legacy-field compatibility pass |
| Bounded background file logger | Complete | ERROR context, fixed timestamp, high-volume enqueue, and destructor flush pass |
| Animated GIF/WebP detection | Complete | Decoder-validated GIF and RIFF `ANMF` WebP fixtures pass; unreadable candidates remain non-animated |
| Direct and recursive folder scanner | Complete | Supported filtering, folder-first direct results, cancellation, missing folders, locked files, and child-folder-only scans pass |
| Canonical directory de-duplication | Complete | Recursive symlink/junction alias parity passes |
| JPG conversion and same-basename cleanup | Complete | Original preservation, animation/format skips, collision handling, real JPEG output, partial-output cleanup, and per-item continuation pass |
| Single and drop-target batch rename execution | Complete | Same-name/collision reasons, selection order, sequence gaps, missing-source continuation, and filesystem side effects pass |
| OS trash and reveal adapters | Windows complete; Linux runtime gate pending | Windows command composition plus real Explorer selection and Recycle Bin smoke pass with a temporary fixture; Linux uses `gio trash` and `xdg-open` but still requires desktop smoke |
| Bounded thumbnail disk cache | Complete | Metadata/size SHA-256 keys, scaled auto-transform decode, PNG output, cache hit/corruption recovery, cancellation, and pruning pass |
| Visible thumbnail request pipeline | Complete | Logical concurrency 4, physical timeout isolation, duplicate suppression, cancellation, size generation, stale-result rejection, model role updates, and diagnostics pass |
| Library model, history, sorting, recursive scan state | Complete | Typed roles, bounded worker scans, generation/cancellation guards, selection invalidation, sort-without-rescan, and 10,000-item single reset pass |
| Lazy folder-tree model | Complete | Root preservation/replacement, deep selection, one-shot lazy child loads, failure retention, and stale-root suppression pass |
| Startup settings and service composition | Complete | Worker-thread settings load/update, last-folder restore, picker request, picker-only persistence, diagnostics, and history tree-root restoration pass |
| Qt Quick app shell | Complete | `PicLens.exe`、typed C++ registration、native folder picker、command/status bars、lazy TreeView、reusable GridView delegates、thumbnail lifecycle、embedded fonts/icons、empty/loading/error states all pass QML lint, Qt Quick Test, offscreen CTest, and real Windows visual smoke |
| Search、grid/list、sidebar and accessibility parity | Windows implementation complete | Search filters name/path without rescan and scopes visible operations/viewer; grid/list rows、sidebar、empty/retry actions、folder/context keyboard paths and explicit Accessible metadata are implemented and test/compile/real-renderer clean; authorized Windows UIA tree read identifies search/clear/folder/tree/view/gallery controls and focus |
| Gallery selection and context commands | Windows complete; Linux adapter gates passed | Single/Ctrl/Shift ordered image selection, Space-key selection, Explorer-style right-click scope, rename/trash/reveal/convert/cleanup commands, dialogs, cancellation, worker execution, diagnostics, refresh clearing, real filesystem rename/JPEG conversion, Windows context-menu UI automation, Explorer selection, Recycle Bin smoke, Ubuntu `gio trash`/`xdg-open`, and Linux package launch pass |
| Inline image viewer | Complete on Windows | Immutable image sequence, Enter/double-click open, in-window dark overlay, full-image loading, previous/next, wheel/toolbar zoom, pointer/keyboard pan, transform reset, animated GIF/WebP feedback, Escape close/focus restoration, lifecycle/error logging, Qt tests, and real Windows renderer screenshot pass |
| Internal drag/drop rename | Complete on Windows | 8px threshold, ordered multi-selection sources, pointer-following preview, target highlight, 33ms edge autoscroll, cancel/capture cleanup, deterministic typed preview, explicit confirmation, worker execution, per-item diagnostics, refresh clearing, Qt tests, and real Windows pointer smoke pass |
| Keyboard and mouse input parity | Complete on Windows | Space/Ctrl/Shift selection, Enter viewer, Escape focus restoration, viewer arrow/pan/wheel input, and isolated browser-style Back/Forward side-button routing are implemented and lint/test clean |
| Windows portable release | Clean-runner candidate passed | `windeployqt` produces a relative-`qt.conf` Qt/QML/image/platform bundle with MIT/Qt/Noto licenses; local MSYS2 builds add recursive PE closure; sanitized runtime and normal qwindows visual smoke pass; Windows 2025 MSVC artifact has 1,407 files / 169,910,166 bytes and launches in installer lifecycle; signing remains |
| Windows MSI installer | Local and hosted lifecycle passed | Forced WiX rebuild consumes the Qt portable payload; WiX MajorUpgrade/HKLM key path and database audit pass; local fresh and `1.1.9 → 1.2.0` upgrade pass; hosted same-version replacement installs both ProductCodes, launches both packages, verifies hash/shortcut/registration/profile preservation, and uninstalls cleanly |
| Linux DEB/RPM installers | Ubuntu/Fedora hosted lifecycle passed | Ubuntu DEB uses deployed Qt/xcb under Xvfb and Fedora 44 RPM uses distro system Qt/RPM dependencies; app remains under `/opt/piclens` while desktop/icon install to `/usr/share`; both install, launch, remove and preserve isolated profiles |
| Cross-platform release CI | Passed on 2026-07-11 | `qt-migration.yml` run 29146770536 builds/tests Windows 2025 MSVC, Ubuntu 24.04 and Fedora 44; produces portable/MSI/DEB/RPM artifacts; 10,000-image Windows performance, sanitized deployment, Windows upgrade lifecycle, Linux package lifecycle, and Ubuntu `gio trash` / `xdg-open` adapter smoke all pass |
| Qt redistribution inventory | MIT license integrated; legal review pending | Dynamic linking, Qt modules/plugins, MSYS2 shared-runtime closure, PicLens MIT license, Noto OFL, notice locations, and toolchain variants are recorded; final release inventory and legal review remain release gates |
| Settings/log/cache continuity | Windows real-profile copy and cross-platform package lifecycle passed | Avalonia and Qt share platform app-data paths and JSON field/enum contract; both sides load the other's schema; authorized packaged-Qt smoke against a copy of the real profile preserved source data; hosted MSI/DEB/RPM install/remove preserves isolated profiles byte-for-byte |
| Release performance/memory | Windows local and clean-runner gates passed | Local Qt Quick Release scans 2,017 recursive images in 1,694 ms at 232,275,968-byte peak; Windows 2025 MSVC scans 10,000 copied PNG paths in 2,024 ms at 223,281,152-byte peak under the 5 s/512 MiB gate; Linux numeric measurement and heterogeneous interaction evidence remain |

## Current verification

From `qt/`:

```powershell
cmake --preset debug
cmake --build --preset debug
ctest --preset debug
cmake --preset release
cmake --build --preset release
ctest --preset release
```

Legacy coexistence gate from repository root:

```powershell
dotnet run Tasks.cs test
```

## Next slice

Close the remaining release/cutover gates:

- Capture a numeric Linux Release scan/RSS result and heterogeneous thumbnail-decoding interaction evidence.
- Complete final redistribution/legal review and code signing for public release artifacts.
- Obtain explicit destructive legacy-removal approval, then remove Avalonia/.NET in a focused change and rerun Qt/release gates.

Do not remove legacy Core, Infrastructure, Presentation, or Avalonia UI yet. Windows/Ubuntu/Fedora clean-runner proof、PicLens MIT license、authorized real-profile copy smoke 與 local/hosted installer lifecycle have passed；explicit destructive cutover approval remains mandatory.
