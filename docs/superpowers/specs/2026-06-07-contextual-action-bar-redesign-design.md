# Contextual Action Bar Redesign Design

## Purpose

ImageViewerWin should feel better for high-volume image organization. The first improvement is to make selection and file-affecting actions clearer without changing the underlying file-operation rules.

This design adds a contextual action bar for selected images in the main window and keeps the secondary image viewer aligned with the same "fast confirmation" workflow.

## Goals

- Show selection state near the image grid instead of relying on disabled toolbar commands.
- Make single-image actions explicit: rename and move to recycle bin remain available only when exactly one image is selected.
- Keep batch operation results in the existing bottom `InfoBar`.
- Preserve current folder navigation, sorting, thumbnail loading, drag-to-rename, conversion, clear-same-basename, rename, trash, and viewer behavior.
- Use native WinUI controls and theme resources only.

## Non-Goals

- Do not change `FileOperationService` behavior.
- Do not introduce batch trash or batch rename.
- Do not add a permanent right-side details panel.
- Do not replace the main window shell, folder tree, or thumbnail grid.
- Do not introduce custom visual libraries.

## Main Window Design

`MainPage.xaml` keeps the current two-pane layout: folder pane on the left, library content on the right, and bottom `InfoBar` for operation results.

The library content gains one contextual row between the existing command bar row and the `GridView`.

When no image is selected:

- The contextual row is collapsed.
- The existing library command bar stays in its current location.
- The bottom `InfoBar` continues to show load and operation status.

When one image is selected:

- The contextual row appears.
- It shows a compact selection summary such as `已選 1 張圖片`.
- It shows `重新命名`, `移至回收筒`, and `清除選取`.
- `重新命名` and `移至回收筒` execute the existing `RenameSelectedCommand` and `TrashSelectedCommand`.

When more than one image is selected:

- The contextual row shows a pluralized selection summary such as `已選 3 張圖片`.
- `重新命名` and `移至回收筒` are disabled because the existing commands only support a single selected image.
- `清除選取` remains enabled.
- The UI must not imply that multi-image trash or rename exists.

## ViewModel Design

`MainPageViewModel` owns the selection-derived state so the XAML can bind to it directly.

Add:

- `SelectedImageCount`: number of selected visible image items.
- `HasSelectedImages`: true when `SelectedImageCount > 0`.
- `SelectionSummaryText`: Traditional Chinese selection summary for the contextual row.
- `ClearSelectedLibraryItems()`: public method that clears the ViewModel selection state.

`UpdateSelectedLibraryItems` continues to receive the ordered `GridView` selection from `MainPage.xaml.cs`. It updates the existing `selectedImagePaths` list, then raises property changes for the selection summary and command state.

The contextual row's `清除選取` button is handled in `MainPage.xaml.cs`. It clears `LibraryGrid.SelectedItems`, clears the code-behind `librarySelectionOrder`, and calls `ViewModel.ClearSelectedLibraryItems()` so visual and ViewModel selection state cannot diverge.

## Viewer Window Design

`ImageViewerWindow` keeps the current title bar, image surface, and status bar. The polish is intentionally light:

- Keep title, position, and zoom readable for quick image confirmation.
- Keep `ViewerTitleCommandBar` and `ViewerStatusBar` automation identifiers.
- Do not change previous/next, zoom, pan, full-screen, keyboard, or unsupported animated-image behavior.

Any viewer copy changes should remain concise Traditional Chinese and should not add instructional text to the window.

## Accessibility And Localization

- Add `AutomationProperties.AutomationId` for the contextual action bar and its controls.
- Add meaningful `AutomationProperties.Name` values for icon-only or compact commands.
- Keep all visible text in Traditional Chinese (Taiwan).
- Use WinUI text styles and theme resources; avoid hard-coded colors.
- Ensure the action bar is keyboard reachable and does not trap focus.
- Keep spacing on the 4 epx grid and avoid fixed widths that clip localized text.

## Diagnostics

The repo instruction is to add `ERROR LOG` for development paths that can fail and need later diagnosis.

This design does not add new file-operation behavior. Existing async operations continue to use existing command paths and diagnostics. If implementation introduces new async or failure-prone UI coordination, log failures through `App.Logger.Error(...)` with enough context to identify the selection state and command path. Do not add noisy logs for ordinary selection changes or synchronous visual clearing.

## Tests

Add focused tests before implementation:

- `MainPageViewModel` reports `SelectedImageCount`, `HasSelectedImages`, and `SelectionSummaryText` for zero, one, and multiple selected images.
- `RenameSelectedCommand` and `TrashSelectedCommand` remain executable only for exactly one selected image.
- Clearing selection resets the selection-derived state and command availability.
- XAML/text tests confirm the contextual action bar exists, has automation IDs, and binds to the selection-derived state.
- Existing tests for `InfoBar` severity mapping, command IDs, thumbnail behavior, and viewer localization remain passing.

## Verification

Run:

```powershell
dotnet test ImageViewerWin.slnx --no-restore -m:1
.\BuildAndRun.ps1 ImageViewerWin\ImageViewerWin.csproj -SkipRun
git diff --check
```

If solution-level tests are blocked by environment or platform issues, run each affected test project directly and record the exact reason solution-level verification could not run.
