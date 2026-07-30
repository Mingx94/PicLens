# 2026-07 Qt cutover record

This is a historical record, not a current status page or backlog. PicLens production moved to Qt 6、C++20 and Qt Quick, and the authorized cutover removed the former Avalonia/.NET runtime, test suites, prototype, rollback commands and legacy Fedora packaging builder.

## Final ownership established by the cutover

| Surface | Qt owner | Verification at completion |
|---|---|---|
| Domain rules and models | `src/core` | Qt Test |
| Filesystem, persistence, logging, thumbnails and OS adapters | `src/infrastructure` | Qt Test plus platform smoke |
| Library, folder tree, file operations and viewer state | `src/presentation` | Qt Test |
| Composition and runtime diagnostics | `src/app` | app-controller and runtime-data tests |
| Visual shell and interaction | `qml/PicLens` | Quick Test plus deployed smoke |
| Windows portable/MSI | `scripts`, `installer/` | payload audit and lifecycle |
| Linux portable/DEB/RPM | CMake/CPack and `scripts` | Ubuntu/Fedora lifecycle |

## Contract audit at completion

| Product contract | Production owner | Result at cutover |
|---|---|---|
| Folder scan, recursive mode, format filtering and sort | Core + Infrastructure | Implemented; unit/integration gates passed |
| Search, grid/list, selection and thumbnail sizing | Presentation + QML | Implemented; controller and Quick Test gates passed |
| Lazy folder tree and navigation history | Presentation + QML | Implemented; model/controller gates passed |
| Bounded thumbnail decode, cache and stale-request rejection | Infrastructure + Presentation | Implemented; concurrency/cache gates passed |
| Rename, trash, reveal and drag/drop | Infrastructure + Presentation + QML | Implemented; Windows and Linux adapter gates passed |
| Inline viewer, zoom, pan and input parity | Core + Presentation + QML | Implemented; controller/QML/runtime gates passed |
| Settings, logging and profile continuity | Infrastructure | Implemented; persistence and copied-profile gates passed |
| Portable deployment | Qt scripts | Windows and Ubuntu clean-runner gates passed |
| MSI / DEB / RPM lifecycle | WiX + CPack | Windows, Ubuntu and Fedora lifecycle gates passed |
| Licensing | Root MIT + third-party notices | Payload audit passed |
| Large-library performance | App diagnostics + performance script | Local representative and hosted Windows scale gates passed |

No production contract retained a legacy runtime owner. Historical schema names remained only where tests protected existing user data compatibility.

## Recorded evidence

- Local Windows Debug and Release CTest: 15/15 passed at the recorded cutover revision.
- Local representative-library Release gate: 2,017 images, 1,694 ms, peak working set 232,275,968 bytes.
- Hosted Windows 2025 run `29147384340`: 10,000 images, 1,899 ms, peak working set 226,701,312 bytes; portable and MSI lifecycle passed.
- The same hosted workflow passed Ubuntu 24.04 portable/DEB lifecycle and Fedora 44 RPM lifecycle, including platform trash/reveal adapters.
- Authorized copied-profile verification loaded the historical JSON contract and preserved the source profile unchanged.
- Release payloads included the applicable PicLens、Qt and third-party notices at the time of the audit.

These results describe the historical revision only. They do not prove the current checkout passes the same gates.

## Destructive-removal decision

Destructive legacy removal was explicitly authorized on 2026-07-11. Framework-neutral assets moved to root `assets/`; WiX remained because it packages the Qt Windows payload, while Linux packages moved to CPack.

Current behavior and procedures belong to [runtime invariants](../runtime-invariants.md), [testing](../testing.md), [release](../release.md), [performance](../performance.md) and [licensing](../licensing.md).
