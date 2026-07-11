# Installer release

The package version comes from root `VERSION` unless explicitly overridden.

## Windows MSI

```powershell
pwsh -NoProfile -File scripts/build-msi.ps1
```

Useful diagnostic options:

```powershell
pwsh -NoProfile -File scripts/build-msi.ps1 -DryRun
pwsh -NoProfile -File scripts/build-msi.ps1 -NoRelease
pwsh -NoProfile -File scripts/build-msi.ps1 -NoClean
pwsh -NoProfile -File scripts/build-msi.ps1 -Version 2.0.0
```

Output: `artifacts/installer/PicLens-win-x64.msi`.

The script logs and times three independent stages: Qt portable payload, WiX build, and MSI database/payload audit. `-NoRelease` requires an existing portable bundle. WiX Toolset currently requires the .NET SDK, but no application runtime or test project uses .NET.

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

Both Linux builders configure a Release build, compile it, run CTest, generate the package, verify its version and installed executable path, copy it into `artifacts/installer`, and print its SHA-256. Common options:

```bash
bash scripts/build-deb.sh --dry-run
bash scripts/build-deb.sh --no-test
bash scripts/build-rpm.sh --no-build
bash scripts/build-rpm.sh --build-dir /tmp/piclens-build --output-dir /tmp/piclens-artifacts
```

`--no-build` requires an already configured and built directory. DEB packages bundle the deployed Qt runtime; RPM packages use Fedora/RHEL system Qt dependencies.

DEB/RPM are generated from the Qt CMake install graph. The old standalone Fedora builder and rollback package path have been removed. Signing remains a release-operations responsibility and requires the appropriate certificate/key outside this repository.

## GitHub Release downloads

Pushing a tag named `v<version>` runs the full Windows, Linux, and Fedora release gates. After all three succeed, the workflow publishes (or updates) the matching GitHub Release with:

- `PicLens-win-x64.msi` and `PicLens-win-x64-portable.zip`;
- `PicLens-linux-x64-portable.tar.gz` and the Debian package;
- the Fedora RPM; and
- `SHA256SUMS.txt` for every uploaded download.

The tag must exactly match the root `VERSION` file (for example, `VERSION=2.0.0` requires tag `v2.0.0`). To rebuild an existing release, run **Qt release gates** with **Run workflow**, enter its existing tag in `release_tag`, and the workflow builds from that tagged source before replacing assets with the same names.
