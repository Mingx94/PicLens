# Release and packaging

Root `VERSION` is the formal release version authority. Each release uses the UTC release date and two-digit serial format `YY.MMDD.NN`, starting at `01` each day. Scripts、CMake install rules、WiX and `.github/workflows/release.yml` are the executable authorities; this document explains how to invoke them and which operations modify the host system.

The Windows MSI keeps the release version in its filename, while its internal `ProductVersion` uses the legal numeric mapping `YY.MM.((DD - 1) * 1440 + 1340 + NN)`. This reserves the last 100 build values of each day for serials `01` through `99`, preserves upgrades from the previous timestamp-based MSI and stays within Windows Installer limits.

## Windows portable

```powershell
pwsh -NoProfile -File scripts/build-portable.ps1
```

Output: `artifacts/qt-portable/PicLens-win-x64/`.

The script uses an existing Release build, verifies the native application icon, runs the matching deployment tooling available to the script, retains the Basic controls style, copies required licenses/assets and performs an isolated offscreen smoke. The test-only `qoffscreen` plugin is removed after smoke and is not distributed. Distribute the complete directory, not only `PicLens.exe`.

The current script does not claim a general scan for absolute build-machine paths embedded inside artifact contents.

`-SkipSmoke` builds the portable payload without its packaged smoke. Release automation uses it for packaging-only jobs.

## Windows MSI

```powershell
pwsh -NoProfile -File scripts/build-msi.ps1
```

Output: `artifacts/installer/PicLens-<version>-win-x64.msi`.

By default the script rebuilds the Release executable, creates the portable payload, builds the MSI and audits an administrative image against that payload by relative path, byte length and SHA-256. WiX requires the .NET SDK, but the PicLens application runtime does not use .NET.

Diagnostic options:

```powershell
pwsh -NoProfile -File scripts/build-msi.ps1 -DryRun
pwsh -NoProfile -File scripts/build-msi.ps1 -NoRelease
pwsh -NoProfile -File scripts/build-msi.ps1 -NoClean
```

`-NoRelease` requires an existing portable payload. The optional `-Version` parameter is reserved for local WiX rehearsal; it must not be used to publish a release because it can label the MSI independently from the executable built from `VERSION`.

`-SkipSmoke -SkipAudit` keeps a packaging-only CI job to build work; local release validation should retain the defaults.

### Signing

To sign the executable before packaging and the MSI after packaging:

```powershell
pwsh -NoProfile -File scripts/build-msi.ps1 -Sign `
  -CertificateThumbprint <thumbprint> `
  -TimestampUrl http://timestamp.digicert.com
```

The repository workflow currently does not pass `-Sign`. Artifacts published directly by that workflow are unsigned unless an external release-operations stage signs and re-audits them before publication.

### MSI lifecycle

Lifecycle testing installs and uninstalls PicLens and requires an elevated PowerShell process:

```powershell
pwsh -NoProfile -File scripts/build-msi.ps1 `
  -RunLifecycleTest -ConfirmSystemChanges `
  -PreviousMsiPath <previous.msi>
```

Run this only on a disposable or explicitly authorized Windows environment.

## Linux portable

```bash
bash scripts/build-linux-portable.sh
```

Output: `artifacts/qt-portable/PicLens-linux-x64/`.

The script configures the isolated `build/linux-portable-release` tree, builds/tests, deploys required libraries/plugins and performs a sanitized smoke. The output is a complete directory, not a single-file executable. `--build-dir` and `--output-dir` provide explicit overrides; the legacy `PICLENS_QT_BUILD_DIR` and `PICLENS_QT_OUTPUT_DIR` environment defaults remain supported. `--no-test --skip-smoke` produces the portable artifact without test or smoke execution for packaging-only CI.

## Debian / Ubuntu DEB

```bash
bash scripts/build-deb.sh
```

Output: `artifacts/installer/piclens_<version>_<architecture>.deb`.

## Fedora / RHEL RPM

```bash
bash scripts/build-rpm.sh
```

Output: `artifacts/installer/piclens-<version>-<release>.<architecture>.rpm`.

The Linux package wrappers use the shared `build-linux-package.sh`, run the configured build and tests unless explicitly disabled, generate a package through CPack, verify package metadata and print SHA-256. DEB defaults to `build/linux-deb-release`; RPM defaults to `build/fedora-rpm-release`, so portable、DEB and RPM no longer exchange policy through one CMake cache.

Each package run clears its own `package-output/<kind>` staging directory and requires exactly one generated package. It writes `piclens-deb-manifest.json` or `piclens-rpm-manifest.json` beside the copied artifact with schema version、package version、architecture、filename、byte count and SHA-256. Lifecycle and publish jobs resolve the exact package from that manifest instead of choosing by mtime or a broad wildcard.

Bundled Linux releases configure `PICLENS_REQUIRE_BUNDLED_LICENSES=ON`. Configuration fails unless matching Qt base、declarative、imageformats and libwebp license sources can be installed from the Qt installation or `PICLENS_QT_SOURCE_ROOT`. System-Qt RPM builds keep this requirement disabled because Fedora packages own those runtime licenses.

```bash
bash scripts/build-deb.sh --dry-run
bash scripts/build-deb.sh --no-test
bash scripts/build-rpm.sh --no-build
bash scripts/build-rpm.sh --build-dir /tmp/piclens-build --output-dir /tmp/piclens-artifacts
```

`--no-build` trusts an existing configured build tree and should only be used when its package mode and source revision are known. DEB bundles the deployed Qt runtime; RPM uses Fedora/RHEL system Qt dependencies.

Linux lifecycle scripts install and remove packages and should run only in disposable containers/VMs or an explicitly authorized host:

```bash
bash scripts/test-linux-package-lifecycle.sh --deb <package.deb>
bash scripts/test-linux-package-lifecycle.sh --rpm <package.rpm>
```

## GitHub Release

Changing `VERSION` alone does not start a release. A tag named `v<version>` runs Windows、Ubuntu and Fedora release gates. The tag must exactly match `VERSION`.

A preflight job validates `VERSION`, the requested tag and the exact checkout ref before any platform build starts. Manual runs without `release_tag` execute gates against the selected commit but do not publish. A successful tagged release publishes the Windows MSI and portable archive, Linux portable archive, DEB, RPM and `SHA256SUMS.txt`. To rebuild an existing tag, run **Qt release gates** manually with that tag in `release_tag`.

The workflow implementation is authoritative for exact runner versions, filenames and publication behavior. Current release automation still has known cleanup work around build-tree isolation, exact artifact manifests, signing and fail-closed license-source validation; do not infer stronger guarantees than the scripts actually enforce.

## Licensing

Every release candidate must be reviewed against [Licensing and redistribution](licensing.md). Root `LICENSE` and `THIRD_PARTY_NOTICES.txt` remain separate legal payloads and must not be removed merely to reduce duplication.
