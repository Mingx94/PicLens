# Licensing and redistribution

This is an engineering release policy, not legal advice. The project owner must review the final dependency and asset set before public or commercial distribution.

## Authorities

- PicLens source code uses the root MIT `LICENSE`.
- `Cargo.lock` is the authority for exact Rust dependency revisions in a checkout.
- The bundled Noto Sans CJK TC files use the notice in `assets/Fonts/NotoSansCJKtc-OFL.txt`. Keep the fonts and required license text together when distributed.
- Derive exact third-party obligations from the final package and locked dependency graph.

## Current dependency model

PicLens uses Rust crates plus native platform and graphics dependencies reached through GPUI and gpui-component. `Cargo.lock` records the exact Git commits. The application manifest requests the GPUI Windows backend and both Linux window backends.

The portable release archives contain the executable, PicLens MIT license, README, and Noto Sans CJK TC OFL notice. The font files are embedded in the executable. Linux graphics and desktop integration remain system dependencies.

## Bundled assets

The GPUI binary embeds three Noto Sans CJK TC font files. The application icon PNG and Windows ICO are also source assets. A release audit must confirm the correct license and provenance for every distributed asset.

## Required release review

1. Record the target, toolchain, Cargo lockfile, GPUI revision, and package format.
2. Generate a dependency and runtime inventory from the final staged artifact.
3. Confirm the PicLens MIT license and all required third-party notices are present.
4. Review Rust crates, native libraries, image codecs, bundled fonts, and application artwork.
5. Inspect the final package file list and its declared system dependencies.
6. Re-run the audit after signing or any operation that changes the artifact.

Engineering checks do not constitute legal approval.
