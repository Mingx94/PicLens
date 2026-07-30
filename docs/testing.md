# Testing

## Local suites

```powershell
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

## Windows release gate

```powershell
pwsh -NoProfile -File scripts/run-windows-cutover-gate.ps1 `
  -PerformanceFolder <representative-folder>
```

The local gate validates Release build/tests, deployed portable smoke, performance thresholds against the caller-provided representative folder, and data continuity. The hosted Windows workflow separately creates the 10,000-path scale fixture. The performance-only command and dataset requirements are documented in [performance.md](performance.md).

## Package lifecycle

```powershell
pwsh -NoProfile -File scripts/test-msi-lifecycle.ps1 `
  -PreviousMsiPath <previous.msi> -ConfirmSystemChanges

# Or run it as the final, explicitly enabled build-msi stage:
pwsh -NoProfile -File scripts/build-msi.ps1 `
  -RunLifecycleTest -ConfirmSystemChanges `
  -PreviousMsiPath <previous.msi>
```

```bash
bash scripts/test-linux-package-lifecycle.sh --deb <package.deb>
bash scripts/test-linux-package-lifecycle.sh --rpm <package.rpm>
```

Lifecycle gates install, launch, replace/upgrade where applicable, remove, and verify isolated profile preservation. They are never run by a normal MSI build; the integrated form still requires both `-RunLifecycleTest` and `-ConfirmSystemChanges` because it modifies the runner OS.

## CI

`.github/workflows/release.yml` 目前只由 `v*` tag push 或手動 `workflow_dispatch` 觸發；一般 branch push 與 pull request 不會自動執行這組 gates。非 release 變更在合併前仍需依修改範圍完成本機驗證。

Workflow 會執行 Windows 2025、Ubuntu 24.04 與 Fedora 44 jobs。Windows job 包含 Release CTest、10,000-image performance gate、portable、MSI 與 lifecycle；Ubuntu job 包含 portable、desktop adapters、DEB 與 lifecycle；Fedora job 包含 RPM build 與 lifecycle。符合版本規則的 tag 在三個平台全部成功後才發布 GitHub Release assets。

同一個 ref 的新 run 會取消較舊的 run，避免重複或過期的長時間工作。實際 trigger、runner 與 stage 以 `.github/workflows/release.yml` 為準。
