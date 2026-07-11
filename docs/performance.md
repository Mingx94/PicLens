# Qt Release performance evidence

Performance claims require Release builds and representative libraries. Debug timings、framework impressions 或單一 unit test 不可作為 cutover evidence。

## Reproducible gate

Windows：

```powershell
cmake --build --preset release --target piclens
pwsh -File qt/scripts/measure-performance.ps1 -FolderPath <representative-folder>
```

The script launches the real Qt Quick executable with isolated settings/cache, recursive scanning and offscreen rendering. The app writes JSON after the library model is applied and visible thumbnail requests have had a 1.5-second settle window. Current conservative thresholds are:

- scan/model/settle elapsed: at most 5,000 ms;
- peak working set: at most 512 MiB;
- non-empty row/image counts are mandatory.

The cross-platform workflow creates 10,000 copied PNG paths on a clean Windows runner and runs the same gate. Repeated small valid PNG files exercise path enumeration/model scale without representing heterogeneous decoder cost, so the real-library run remains separately required.

## Current local evidence

2026-07-11 local Windows Release run：

| Metric | Result |
|---|---:|
| Qt/toolchain | Qt 6.11.1, MSYS2 UCRT64 MinGW |
| Dataset | 2,017 recursively scanned images |
| Elapsed through settle window | 1,694 ms |
| Working set | 232,275,968 bytes |
| Peak working set | 232,275,968 bytes |
| Active thumbnail requests at sample | 0 |

Raw output is generated at `artifacts/performance/windows-release.json` and intentionally ignored by git.

## Hosted Windows evidence

2026-07-11 Windows 2025 / Qt 6.8.3 MSVC run 29146770536：

| Metric | Result |
|---|---:|
| Dataset | 10,000 copied valid PNG paths |
| Elapsed through settle window | 2,024 ms |
| Working set | 223,281,152 bytes |
| Peak working set | 223,281,152 bytes |

The same run built the 1,407-file MSVC portable bundle and passed MSI install/upgrade/launch/uninstall. Both local and hosted Windows results remain below the 5,000 ms / 512 MiB thresholds.

## Remaining performance gates

- Linux Release scan/RSS result on the deployed artifact.
- Interaction check while thumbnails are decoding on a heterogeneous large library.
- Installer build installed-app measurement to rule out deployment-path regressions.
