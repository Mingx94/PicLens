# Product

<!-- impeccable:product-schema 1 -->

## Platform

adaptive

## Users

PicLens primarily serves people who manage large collections of local images. They need to browse, inspect, select, and organize many files without adopting an account-based or catalog-first workflow.

## Product Purpose

PicLens is a Windows and Linux desktop image library and viewer. It helps users enter a folder-based image collection quickly, inspect images, manage selections, and complete simple batch organization tasks in one workspace.

Success means that users can understand the current folder, item count, sort and scope settings, selection, and file-operation results while working with large local collections.

## Positioning

PicLens combines a modern desktop interface with simple image-management tools. It keeps the local folder structure as the source of truth and avoids the setup and complexity of account-based, cloud-based, or catalog-first systems.

## Operating Context

- Users work with folders and image files on their local Windows or Linux file system.
- The main workflow combines folder navigation, grid browsing, search, sorting, selection, batch operations, and an embedded image viewer.
- File operations include safe rename, move to the operating-system trash, JPG conversion, lossless WebP conversion, and same-basename format cleanup.
- The application preserves enough visible status and diagnostic logging for users to understand failures and for later troubleshooting.

## Capabilities and Constraints

- The current interface language is Traditional Chinese for Taiwan.
- Supported image formats are JPG, JPEG, PNG, BMP, WebP, and GIF.
- Animated GIF and WebP files are recognized but are not played.
- File mutations use conservative rules: preserve source files during conversion, never overwrite a conflicting target, move removed files to the operating-system trash, and continue independent batch items after one item fails.
- Thumbnail decoding and other blocking file or image work must not block the application thread.
- The current scope excludes accounts, cloud or cross-device sync, remote libraries, image editing, tags, ratings, albums, duplicate detection, animation playback, macOS, mobile, and web versions.
- Formal performance acceptance thresholds, marquee selection scope, and release smoke acceptance remain undecided.

## Brand Commitments

- Keep the product name PicLens.
- Preserve a modern interface and simple management experience.
- Use the packaged PicLens application artwork for product identity.
- Preserve the bundled Noto Sans CJK TC font assets and their license notice while those assets remain in use.

## Evidence on Hand

- `docs/product/product-spec.md` is the authority for user-visible behavior and product scope.
- `docs/engineering/runtime-invariants.md` records engineering constraints that protect product behavior.
- `docs/design/system.md` records the current egui interface system.
- `assets/` contains the current PicLens application icons and licensed Noto Sans CJK TC fonts.
- The repository contains no confirmed testimonials, customer claims, usage benchmarks, pricing, or market evidence. Future work must not fabricate them.

## Product Principles

1. Keep local folders and files as the source of truth.
2. Make large image collections fast to browse and easy to understand.
3. Keep organization tools simple and close to the browsing workflow.
4. Protect original files and report every meaningful operation result.
5. Prefer a modern, focused desktop experience over feature breadth.

## Accessibility & Inclusion

No product-specific accessibility standard or additional user need has been confirmed. This remains an open product decision; future work must still preserve keyboard access, visible focus, clear labels, disabled states, and understandable feedback already required by the incumbent interface system.
