# Native Parity

This document tracks the Electron behavior that ImageViewerWin should preserve.

## Supported Formats

The native app should support the same extension gate as the Electron app:

- `jpg`
- `jpeg`
- `png`
- `bmp`
- `webp`
- `gif`

Animated GIF and animated WebP are detected and treated as unsupported for display in the current product scope. Do not add AVIF, HEIC, TIFF, SVG, or animation playback without updating this document and tests.

## Main Window

Parity target:

- Default size: `1220x820`
- Favorites sidebar
- Folder tree rooted at the best matching favorite
- Toolbar for history, sorting, recursive mode, and file operations
- Grid containing folders and supported image files
- Status/error feedback area

The current native app implements this surface with real filesystem-backed data, persisted favorites/settings, thumbnails for still images, multi-selection, drag/drop rename, browser-style mouse side-button history navigation, and status feedback.

## Core Behaviors Already Covered

The Core test suite covers:

- Image extension support
- Animated GIF/WebP checks
- Folder-first sorting with numeric name ordering
- Settings merge and user-favorite normalization
- Startup folder fallback
- Image sequence snapshot immutability
- Zoom clamp and pointer-anchor math

## Pending Parity

Implemented in the current native milestone:

- Settings persistence
- System and user favorites
- Direct and recursive folder scanning
- Thumbnail and full-image loading
- Secondary image window and viewer controls
- Selection behavior
- Conservative file operations
- Recursive scanner canonical-directory de-duplication for symlink/junction aliases
- Drop-target batch rename sequencing that advances past existing target paths
- Image viewer `1120x760` parity size and Escape-to-main-window focus behavior

Remaining follow-up work should focus on deeper GUI automation and polish rather than missing service wiring:

- Rectangle drag selection parity with the Electron main window
- Automated WinUI UI tests for add/remove/reorder favorites and file operation dialogs
- Manual smoke coverage for real Windows recycle-bin behavior across drives
- Larger-library performance profiling for thumbnail loading

## File Management Rules To Preserve

Native file-management behavior should stay conservative:

- Trash-like operations go to the OS recycle bin.
- JPG conversion preserves original files.
- Target collisions are skipped, not overwritten.
- Batch operations report per-file results.
- Failures continue item-by-item.
- Single rename validates a basename only, reports same-name skips as `same_name`, and rejects existing targets as an invalid request.
