# Versioning and Release Protocol

## Current State

- The GPUI branch does not have a working packaging or release pipeline.
- The checked-in GitHub release workflow belongs to the removed Qt build and must not be run for this branch.
- `Cargo.toml` reports the Rust workspace package version. Root `VERSION` is retained from the Qt release and is not wired into the GPUI binary.

## Before the Next Release

1. Choose one version authority and connect it to package metadata and the binary.
2. Add tested Windows and Linux package paths for the GPUI runtime.
3. Add clean-machine install, launch, upgrade, and uninstall gates.
4. Replace the legacy workflow and verify every claimed platform.
5. Define signing, checksums, license payloads, and release asset names.

See [Release and packaging](../release.md) for the current boundary.
