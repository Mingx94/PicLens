# Licensing and redistribution

This is an engineering release policy, not legal advice. The project owner must review the final dependency and asset set before public or commercial distribution.

## Authorities

- PicLens source code uses the root MIT `LICENSE`.
- `Cargo.lock` is the authority for exact Rust dependency revisions in a checkout.
- The bundled Noto Sans CJK TC files use the notice in `assets/Fonts/NotoSansCJKtc-OFL.txt`. Keep the fonts and required license text together when distributed.
- Derive exact third-party obligations from the final package and locked dependency graph.

## Current dependency model

PicLens uses Rust crates plus native platform and graphics dependencies reached through GPUI and gpui-component. GPUI is locked to Zed commit `c7537bdf463a998e7ec636adff33b198891e69ed`. gpui-component is pinned to commit `9a4a3473e1ee6afa9960c5decf18d3dc321b6ea2`.

There is no current GPUI package layout. Do not reuse the removed Qt redistribution inventory for a future GPUI package.

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
