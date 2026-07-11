# Testing

## Local suites

```powershell
cd qt
cmake --preset debug
cmake --build --preset debug
ctest --preset debug --output-on-failure

cmake --preset release
cmake --build --preset release
ctest --preset release --output-on-failure
```

CTest discovers 15 gates covering product rules, filesystem scanning, persistence, logging, thumbnail cache/bounds, file operations, platform adapters, presentation controllers, application composition and QML components.

## Isolation

Runtime tests and scripts set `PICLENS_DATA_ROOT` to a disposable directory. Settings, thumbnail cache and logs must never target the real user profile unless the user explicitly authorizes a copied-profile verification. File mutation tests operate only inside temporary workspaces.

## Windows cutover gate

```powershell
pwsh -NoProfile -File scripts/run-windows-cutover-gate.ps1
```

This validates Release build/tests, deployed portable smoke, performance thresholds and data continuity. The performance-only command is documented in [performance.md](performance.md).

## Package lifecycle

```powershell
pwsh -NoProfile -File scripts/test-msi-lifecycle.ps1 `
  -PreviousMsiPath <previous.msi> -ConfirmSystemChanges
```

```bash
bash scripts/test-linux-package-lifecycle.sh --deb <package.deb>
bash scripts/test-linux-package-lifecycle.sh --rpm <package.rpm>
```

Lifecycle gates install, launch, replace/upgrade where applicable, remove, and verify isolated profile preservation. They are intentionally separate from unit tests because they modify the runner OS.

## CI

`.github/workflows/release.yml` runs Windows 2025, Ubuntu 24.04 and Fedora 44 jobs. Each slow stage has a descriptive name; the 10,000-image fixture and measurement are separate bounded steps, while MSI construction emits timed log groups for portable build, WiX build and database audit. This makes a timeout attributable instead of appearing as one opaque task.

Feature branches are validated by the pull-request event; direct push validation is limited to `main`. Workflow concurrency automatically cancels an older run for the same PR/ref when a newer commit arrives, preventing duplicate or stale long-running jobs.
