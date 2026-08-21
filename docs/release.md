# Release and packaging

## Version and trigger

The root `Cargo.toml` field `[workspace.package].version` is the only version authority. Every workspace crate inherits it. A release tag must use `v<version>`, for example `v0.1.0`.

Pushing a matching tag starts `.github/workflows/release.yml`. A manual run can rebuild an existing tag. The workflow checks that the tag exists and matches the `piclens-gpui` Cargo version before it builds anything.

## Release gates

Windows 2025 and Ubuntu 24.04 each use the nightly toolchain pinned in `rust-toolchain.toml` and run these locked gates:

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
- `PicLens-<version>-windows-x86_64.zip.sha256`
- `PicLens-<version>-linux-x86_64.tar.gz`
- `PicLens-<version>-linux-x86_64.tar.gz.sha256`

Each archive contains the PicLens executable, `LICENSE`, `README.md`, and the Noto Sans CJK TC OFL notice. The font files are embedded in the executable. The release workflow generates GitHub release notes and attaches the four files.

These are unsigned portable archives. They are not MSI, MSIX, DEB, RPM, or auto-update packages. Linux users must provide a Vulkan 1.3 driver, X11 or Wayland, and the required desktop portals and system libraries.

## Release procedure

1. Update the workspace version and lockfile in one release commit.
2. Run the full commands in [Testing](testing.md).
3. Build and inspect both portable archives from the release commit.
4. Test launch, folder access, file operations, profile preservation, and shutdown on clean Windows and Linux systems.
5. Record all unverified platforms and paths.
6. Create an annotated `v<version>` tag on the release commit.
7. Push the release commit and tag.
8. Confirm that the GitHub Action passes and the GitHub Release contains both archives and both checksum files.

Local compilation or archive creation does not complete a release. Completion requires a successful tag push and a successful release workflow.

## Remaining release work

- Code signing is not configured.
- Installer lifecycle and upgrade tests are not configured.
- Package-manager integration and desktop-file installation are not configured.
- The hosted workflow compiles and packages the app, but it does not launch a GPU window.

Review [Licensing and redistribution](licensing.md) for every release candidate.
