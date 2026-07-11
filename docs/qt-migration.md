# Qt migration record

PicLens production runtime is Qt 6 + C++20 + Qt Quick. The authorized cutover removed the former Avalonia/.NET application, Core/Infrastructure/Presentation projects, .NET test suites, prototype, rollback commands and legacy Fedora packaging builder.

## Final ownership

| Surface | Qt owner | Verification |
|---|---|---|
| Domain rules and models | `src/core` | Qt Test |
| Filesystem, persistence, logging, thumbnails and OS adapters | `src/infrastructure` | Qt Test plus platform smoke |
| Library, folder tree, file operations and viewer state | `src/presentation` | Qt Test |
| Composition and runtime diagnostics | `src/app` | app-controller and runtime-data tests |
| Visual shell and interaction | `qml/PicLens` | Quick Test plus deployed smoke |
| Windows portable/MSI | `scripts`, `installer/` | payload audit and lifecycle |
| Linux portable/DEB/RPM | CMake/CPack and `scripts` | Ubuntu/Fedora lifecycle |

## Evidence

- Local Windows Debug and Release CTest: 15/15 passed.
- Local representative-library Release gate: 2,017 images, 1,694 ms, peak working set 232,275,968 bytes.
- Hosted Windows 2025 run `29147384340`: 10,000 images, 1,899 ms, peak working set 226,701,312 bytes; portable and MSI lifecycle passed.
- The same hosted workflow passed Ubuntu 24.04 portable/DEB lifecycle and Fedora 44 RPM lifecycle, including Qt platform trash/reveal adapters.
- Authorized copied-profile verification loaded the existing JSON contract and preserved the source profile unchanged.
- Root license is MIT; Qt and embedded font notices are included in release payloads.

## Cutover decision

Destructive legacy removal was explicitly authorized on 2026-07-11. Framework-neutral assets moved to root `assets/`; production paths no longer depend on the deleted application tree. WiX remains because it packages the Qt Windows payload, while the old standalone Fedora builder was removed. Linux packages use CPack.

## Release operations remaining outside migration

- Apply platform signing when release certificates/keys are available.
- Continue collecting Linux numeric performance and heterogeneous-decoder interaction evidence as regression data.
- Re-run the full clean-runner workflow for every release-affecting change.
