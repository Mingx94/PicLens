# Release and packaging

## Version and trigger

The root `Cargo.toml` field `[workspace.package].version` is the only version authority. Every workspace crate inherits it. A release tag must use `v<version>`, for example `v0.1.0`.

Pushing a matching tag starts `.github/workflows/release.yml`. A manual run can rebuild an existing tag. The workflow checks that the tag is annotated and matches the `piclens-desktop` Cargo version before it builds anything.

## Build workflow

The only GitHub Actions workflow builds the Windows x86_64 MSI and portable ZIP on Windows 2025. It uses the nightly toolchain pinned in `rust-toolchain.toml`. The MSI build script runs:

```text
cargo build --release --locked -p piclens-desktop
```

The portable ZIP uses the same executable. There is no branch or pull-request CI. The workflow runs the Windows MSI lifecycle before publication, but it does not run format, test, or Clippy. Run the checks in [Testing](testing.md) locally before release.

Windows release builds use the GUI subsystem and do not open a console at startup. Debug builds keep the console. To read release CLI output in PowerShell, pipe it to `Out-String`, for example `& .\PicLens.exe --help | Out-String` from the executable's directory.

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

The release workflow runs `scripts/test-msi-lifecycle.ps1` on its clean Windows runner before publication. The script uses an isolated `PICLENS_DATA_ROOT` and checks install, launch, reinstall／replace, uninstall, and profile preservation. Linux lifecycle scripts remain manual checks.

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

Code signing is not configured. Do not describe these assets as signed. A successful workflow proves that Windows packaging, MSI install／launch／replace／uninstall, isolated profile preservation, and upload completed. It does not prove ordinary interactive use, GPU behavior, or visual correctness.

## v3.0.0 release evidence

Annotated tag `v3.0.0` points to commit `ac544e4d`. [Windows packages run 33756956796](https://github.com/Mingx94/PicLens/actions/runs/33756956796) completed on a clean Windows 2025 runner. It passed MSI install, launch, replace, uninstall, isolated profile preservation, portable ZIP creation, artifact upload, and GitHub Release publication.

[GitHub Release v3.0.0](https://github.com/Mingx94/PicLens/releases/tag/v3.0.0) contains the four expected unsigned assets. Independent downloads produced these payload hashes, which match their published checksum files:

- MSI: `60dd879387260b5c92d049f4eee0dcfc0603458dafb12f0a9e5d88d911aff74b`
- ZIP: `4e15af1d2b0e8c5466d81b6eaa4640171f49f8704eae1a7c09d9e5edf3973256`

Review [Licensing and redistribution](licensing.md) for every release candidate.
