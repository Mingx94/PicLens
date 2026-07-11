# Avalonia to Qt runtime contract audit

這份 matrix 從目前 Avalonia product surface 反向列出 Qt owner；「Implemented」只表示 code 與
本機適用測試存在，不取代 clean-platform 或人工 evidence。

## Application shell and library

| Legacy contract | Qt owner | Evidence | Status |
|---|---|---|---|
| Native folder picker、startup restore、取消後 empty action | `Main.qml`、`AppController` | app tests、runtime smoke；`--folder` suppresses startup modal | Implemented |
| Back/forward history、mouse side buttons、refresh | `LibraryController`、`HistoryMouseHandler.qml` | presentation tests、QML test、Windows pointer smoke | Windows complete |
| Lazy folder tree、expand/load、keyboard Enter/Left/Right | `FolderTreeModel`、`FolderTreePane.qml` | folder-tree tests、QML compile/runtime | Implemented |
| Sidebar collapse/expand | `AppController.sidebarOpen`、`Main.qml` | app state test、real qwindows visual smoke | Windows complete |
| Direct/recursive scanning、folder-first natural sort | `FolderScanner`、core sorter、`LibraryController` | scanner/core/presentation tests | Implemented |
| Four sort modes and persistence | `LibraryController`、`AppController` | presentation/app/persistence tests | Implemented |
| Include-subfolders toggle and persistence | same owners | presentation/app/persistence tests | Implemented |
| Search name/path、clear、no-results state | `LibraryController.searchQuery`、`Main.qml` | no-rescan/filter/visible-image presentation test、qwindows visual smoke | Windows complete |
| Grid/list view and row metadata | `AppController.gridViewMode`、`LibraryPane.qml`、`GalleryTile.qml` | app state test、QML compile、qwindows visual smoke | Windows complete |
| Thumbnail slider 120–240 step 20 and persistence | core settings rules、`ThumbnailCoordinator`、QML slider | core/app tests | Implemented |
| Empty/loading/error/retry/status states | `LibraryPane.qml`、library status/error state | runtime/QML tests、failure presentation test | Implemented |

## Interaction, viewer, and file operations

| Legacy contract | Qt owner | Evidence | Status |
|---|---|---|---|
| Single/Ctrl/Shift/Space ordered image selection | `LibraryController`、`GalleryTile.qml` | presentation tests、Windows UI smoke | Windows complete |
| Enter/double-click viewer; folder Enter navigation | `GalleryTile.qml`、`ViewerController` | controller tests、real renderer smoke | Windows complete |
| Right-click and Shift+F10 context scope | `GalleryTile.qml`、`FileOperationController` | selection/controller tests、Windows context-menu smoke | Windows complete |
| Reveal、rename、multi-trash、cancel | file-operation presentation/infrastructure owners | unit/controller tests、Windows Explorer/Recycle Bin smoke | Windows complete; Linux runtime pending |
| Visible convert-to-JPG and same-basename cleanup | same owners | filesystem/controller tests | Implemented |
| Internal multi-image drag/drop rename preview/confirm/autoscroll | planner、controller、`GalleryTile.qml`、`LibraryPane.qml` | core/controller/QML tests、Windows pointer smoke | Windows complete |
| Inline viewer sequence、zoom/pan/reset、keyboard/wheel、animated feedback | `ViewerController`、`ViewerOverlay.qml` | core/controller/QML tests、Windows renderer screenshot | Windows complete |

## Data, diagnostics, accessibility, and release

| Contract | Qt owner/evidence | Status |
|---|---|---|
| Shared settings/log/cache paths and bidirectional JSON schema | infrastructure tests、legacy xUnit、packaged copied-profile smoke | Windows synthetic/profile lifecycle passed |
| Bounded async logger、scanner/file-operation failure context | infrastructure/controller tests and file log | Implemented |
| Accessible names/roles/actions for custom toolbar、gallery、tree; keyboard actions | QML `Accessible` metadata、QML test、QML cache compile | Implemented; final Windows UIA tree read was user-stopped |
| Windows portable/MSI deployment and upgrade identity | sanitized smoke、MSI DB audit、fresh and 1.1.9→1.2.0 lifecycle | Windows local complete |
| Reproducible Windows local cutover evidence | `run-windows-cutover-gate.ps1` emits tests/performance/artifact hashes to JSON | Implemented |
| Ubuntu portable/DEB and Fedora 44 RPM | checked-in clean-runner build/lifecycle jobs | First remote runs pending |
| Representative performance/memory | Windows Release 2,017-image evidence and CI 10,000-path gate | Windows local passed; remote/Linux pending |

## Remaining cutover evidence

- Run the checked-in workflow on Windows MSVC、Ubuntu 24.04 and Fedora 44 and retain results/artifacts.
- Read the Windows UIA tree again only after Computer Use is explicitly resumed.
- Run copied-profile verification against an authorized real `%LOCALAPPDATA%/PicLens` profile.
- Choose and add the project-level license, then review the final redistribution inventory.
- Obtain explicit approval before deleting Avalonia/.NET projects and legacy packaging paths.

Until those gates pass, this matrix must not be used to claim destructive cutover is complete.
