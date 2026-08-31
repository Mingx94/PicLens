# Release and packaging

## Version and trigger

The root `Cargo.toml` field `[workspace.package].version` is the only version authority. Every workspace crate inherits it. A release tag must use `v<version>`, for example `v0.1.0`.

Pushing a matching tag starts `.github/workflows/release.yml`. A manual run can rebuild an existing tag. The workflow checks that the tag is annotated and matches the `piclens-gpui` Cargo version before it builds anything.

## Build workflow

The only GitHub Actions workflow builds the Windows x86_64 MSI and portable ZIP on Windows 2025. It uses the nightly toolchain pinned in `rust-toolchain.toml`. The MSI build script runs:

```text
cargo build --release --locked -p piclens-gpui
```

The portable ZIP uses the same executable. There is no branch or pull-request CI. The workflow does not run format, test, Clippy, or package lifecycle checks. Run the checks in [Testing](testing.md) locally before release.

Linux package scripts remain available for local use, but GitHub Actions does not build, test, or publish Linux packages.

## Build an installer on your PC

Use Windows x64 with Rust, the MSVC C++ build tools, and a .NET SDK. The script uses the repository's pinned Rust toolchain. The first build needs network access to restore Rust dependencies and the WiX SDK; no separate WiX installation is needed.

Run this command in PowerShell from any directory:

```powershell
& F:\PicLens\scripts\build-msi.ps1
```

Change the path if your checkout is elsewhere. The script reads the version from Cargo, builds the release executable, and writes these files under the repository's `dist` directory:

- `PicLens-<version>-windows-x86_64.msi`
- `PicLens-<version>-windows-x86_64.msi.sha256`

The default build is unsigned. It does not install PicLens, create a Git tag, or upload files. Building does not need administrator rights; installing the per-machine MSI does.

## Published assets

For version `<version>`, the workflow publishes:

- `PicLens-<version>-windows-x86_64.zip`
- `PicLens-<version>-windows-x86_64.msi`
- `PicLens-<version>-windows-x86_64.zip.sha256`
- `PicLens-<version>-windows-x86_64.msi.sha256`

Each payload contains the PicLens executable, license, README, and Noto Sans CJK TC OFL notice. The MSI installs PicLens per machine and adds a Start Menu shortcut.

All current release assets are unsigned. The release page says this explicitly and provides SHA-256 checksum files. The build script supports optional MSI Authenticode signing, but the hosted workflow does not enable it.

Package lifecycle checks are manual release checks. The scripts remain available under `scripts/`; they are not part of GitHub Actions. Use a clean test machine and an isolated `PICLENS_DATA_ROOT` for install, launch, reinstall/replace, and uninstall checks.

## Release procedure

1. Update the workspace version and lockfile in one release commit.
2. Run the full commands in [Testing](testing.md).
3. Build and inspect the Windows MSI and portable ZIP from the release commit.
4. Test installation, launch, folder access, file operations, profile preservation, shutdown, and uninstall on a clean Windows system.
5. Record all unverified platforms and paths.
6. Create an annotated `v<version>` tag on the release commit.
7. Push the release commit and tag.
8. Confirm that the Windows package job passes and that the GitHub Release contains the MSI, portable ZIP, and both checksum files.

Local compilation or archive creation does not complete a release. Completion requires a successful tag push and a successful release workflow.

## Known limitation

Code signing is not configured. Do not describe these assets as signed. A successful workflow proves that packaging and upload completed. It does not prove installation, upgrade, uninstall, runtime, GPU, or visual behavior.

Review [Licensing and redistribution](licensing.md) for every release candidate.
