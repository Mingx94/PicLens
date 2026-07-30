# Licensing and redistribution

這是工程 release policy，不是法律意見。公開或商業散佈前，project owner 必須依實際 Qt edition、取得方式、linking model 與產品授權模式完成法律審查。

## Authorities

- PicLens source code uses the root MIT `LICENSE`.
- `THIRD_PARTY_NOTICES.txt` remains a separate redistribution notice and must not be merged into or replaced by the MIT license.
- Root `VERSION` is the PicLens package version authority.
- Exact third-party files must be derived from the actual release artifact, deployment tool output and package metadata—not from a hand-maintained DLL snapshot in this document.

## Linking and package models

- PicLens dynamically links Qt、compiler/runtime libraries and image plugins.
- Windows portable/MSI bundle the required runtime next to the application and include applicable PicLens、Qt and third-party notices.
- Ubuntu DEB and Linux portable bundle their configured Qt runtime and corresponding license sources.
- Fedora/RHEL RPM uses distribution Qt dependencies; package metadata must express required dynamically loaded plugins such as `qt6-qtimageformats`.
- Test-only plugins such as `qoffscreen` must not remain in the final distributed payload.

## Fonts and source assets

PicLens does not currently package the OTF files under `assets/Fonts/`. Runtime selects installed platform fonts and falls back to Qt's system font. If those source fonts are retained or redistributed in the future, the OTF files and OFL notice must be handled as one licensed set.

## Required release review

For every release candidate:

1. Record the actual Qt edition, version, source and target platform.
2. Generate the runtime/dependency inventory from the final staged artifact.
3. Confirm PicLens MIT license and third-party notices are present at the documented package locations. Bundled Linux package configuration must use `PICLENS_REQUIRE_BUNDLED_LICENSES=ON` so missing Qt/libwebp license sources fail before packaging.
4. Confirm bundled Qt modules/plugins have matching license sources or distribution-provided notices.
5. Review compiler runtimes、image codecs、fonts and dynamically loaded plugins.
6. Re-run the review after signing or any operation that changes the final artifact.

The Windows MSI audit must confirm notices and runtime files are not lost during incremental packaging. Linux package review must inspect the final DEB/RPM file list and dependencies rather than assuming the build environment supplies the same content.

Engineering checks do not constitute legal approval.
