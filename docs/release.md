# Release and packaging

## Current state

The GPUI migration branch has no working package or publication pipeline. A Cargo release build creates a development binary under `target/release`; it is not an installer, portable bundle, or validated release artifact.

The checked-in `.github/workflows/release.yml` is a legacy Qt workflow. It calls removed CMake and packaging scripts. Do not run it, push a release tag for this branch, or describe its outputs as GPUI release assets.

Version metadata is also not unified:

- the Cargo workspace version is `0.1.0`;
- root `VERSION` contains the last Qt release value;
- the GPUI binary does not expose root `VERSION` as its package version.

## Required release baseline

Before the next PicLens release:

1. Choose one version authority and use it in Cargo metadata, the binary, packages, and tags.
2. Define supported Windows and Linux targets and their minimum runtime requirements.
3. Build clean Windows and Linux artifacts from the locked toolchain and dependency revisions.
4. Package the GPUI runtime, application icon, bundled fonts, license files, and required native libraries.
5. Test install, launch, folder access, upgrade, uninstall, and profile preservation on every claimed platform.
6. Audit the final package file list and run it outside the source tree.
7. Define signing policy, checksums, asset names, and GitHub publication gates.
8. Replace the legacy Qt workflow before any `v*` tag is pushed.

Every candidate must also pass [Testing](testing.md) and the review in [Licensing and redistribution](licensing.md). A successful `cargo build --release` is only a compiler gate.
