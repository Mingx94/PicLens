# Release and packaging

## Version and trigger

The root `Cargo.toml` field `[workspace.package].version` is the only version authority. Every workspace crate inherits it. A release tag must use `v<version>`, for example `v0.1.0`.

Pushing a matching tag starts `.github/workflows/release.yml`. A manual run can rebuild an existing tag. The workflow checks that the tag exists and matches the `piclens-gpui` Cargo version before it builds anything.

## Release gates

Windows 2025, Ubuntu 24.04, and Fedora 42 use the nightly toolchain pinned in `rust-toolchain.toml`. Windows runs the complete Rust gates below. Normal CI runs the same workspace gates on Windows and Ubuntu.

```text
cargo fmt --check
cargo test --workspace --locked
cargo clippy --workspace --all-targets --locked -- -D warnings
cargo build --release --locked -p piclens-gpui
```

The normal CI workflow also runs workspace build and check gates on both platforms.

## Published assets

For version `<version>`, the workflow publishes:

- `PicLens-<version>-windows-x86_64.zip`
- `PicLens-<version>-windows-x86_64.msi`
- `PicLens-<version>-windows-x86_64.zip.sha256`
- `PicLens-<version>-linux-x86_64.tar.gz`
- `PicLens-<version>-linux-x86_64.deb`
- `PicLens-<version>-linux-x86_64.rpm`
- `PicLens-<version>-linux-x86_64.tar.gz.sha256`

Each payload contains the PicLens executable, license, README, and Noto Sans CJK TC OFL notice. The DEB and RPM also install the desktop entry, AppStream metadata, and icon. The MSI installs PicLens per machine and adds a Start Menu shortcut.

All current release assets are unsigned. The release page says this explicitly and provides SHA-256 checksum files. The build script supports optional MSI Authenticode signing, but the hosted workflow does not enable it. Linux users must provide a Vulkan 1.3 driver, X11 or Wayland, and the required desktop portals and system libraries.

Before upload, clean native runners install, launch, reinstall/replace, and uninstall each package. The lifecycle checks preserve an isolated `PICLENS_DATA_ROOT`. A package from another operating system or cross-compilation does not satisfy this gate.

## Release procedure

1. Update the workspace version and lockfile in one release commit.
2. Run the full commands in [Testing](testing.md).
3. Build and inspect both portable archives from the release commit.
4. Test launch, folder access, file operations, profile preservation, and shutdown on clean Windows and Linux systems.
5. Record all unverified platforms and paths.
6. Create an annotated `v<version>` tag on the release commit.
7. Push the release commit and tag.
8. Confirm that all three native package lifecycle jobs pass and that the GitHub Release contains MSI, DEB, RPM, portable archives, and checksum files.

Local compilation or archive creation does not complete a release. Completion requires a successful tag push and a successful release workflow.

## Known limitation

Code signing is not configured. Do not describe these assets as signed. The package lifecycle uses native clean runners and a real GPUI launch under the available desktop session or Xvfb; it does not replace manual GPU and visual validation on representative hardware.

Review [Licensing and redistribution](licensing.md) for every release candidate.
