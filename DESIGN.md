# PicLens Design System

## 1. Atmosphere & Identity

PicLens is a quiet desktop workspace for browsing and organizing local image folders. It should feel focused, dense enough for repeated file work, and visually calm around the images. The signature is restrained utility: neutral surfaces, compact controls, and clear thumbnail/file-name reading.

## 2. Color

### Palette

| Role | Token | Light | Dark | Usage |
|------|-------|-------|------|-------|
| Surface/app | `AppBackgroundBrush` | `#F8F9FA` | TBD | Main shell background |
| Surface/command | `CommandBarBrush` | `#F1F3F5` | TBD | Toolbar and viewer command bar |
| Surface/sidebar | `SidebarBrush` | `#FBFCFD` | TBD | Folder pane |
| Surface/content | `SurfaceBrush` | `#FFFFFF` | TBD | Main library surface |
| Surface/tile | `TileFrameBrush` | `#EEF0F2` | TBD | Thumbnail tile frame |
| Border/default | `LineBrush` | `#E2E5E9` | TBD | Dividers and tile borders |
| Text/primary | `PrimaryTextBrush` | `#1F2328` | TBD | Main labels and file names |
| Text/secondary | `SecondaryTextBrush` | `#5F6368` | TBD | Paths, metadata, viewer filename |
| Text/muted | `MutedTextBrush` | `#7A8088` | TBD | Lower emphasis icons/text |
| State/hover | `HoverBrush` | `#E8EBEF` | TBD | Toolbar hover |
| State/selected | `SelectedBrush` | `#DFECFF` | TBD | Selected library tile |
| Accent/primary | `AccentBrush` | `#0078D4` | TBD | Info badge and drop target |
| Surface/viewer | `ViewerCanvasBrush` | `#F7F7F7` | TBD | Image viewer canvas |

### Rules

- Keep color functional: surfaces, text hierarchy, selection, and status only.
- Add new colors as named Avalonia resources before using them in views.

## 3. Typography

### Scale

| Level | Size | Weight | Line Height | Tracking | Usage |
|-------|------|--------|-------------|----------|-------|
| Page title | 24 | SemiBold | Font metrics | 0 | Current folder name |
| Pane title | 22 | SemiBold | Font metrics | 0 | Sidebar and empty state title |
| Section label | 20 | SemiBold | Font metrics | 0 | Parent folder label |
| Body | Default | Normal | Font metrics | 0 | Toolbar, status, tree labels |
| Tile label | 14 | SemiBold | Font metrics | 0 | Thumbnail file names |
| Caption | 12 | Normal | Font metrics | 0 | Current folder path |

### Font Stack

- Primary: embedded `Noto Sans CJK TC` from `PicLens/Assets/Fonts`.
- Mono: platform default monospace when needed.
- Serif: not used.

### Rules

- Use one UI font family so Traditional Chinese, Japanese, Korean, and Latin filenames share consistent metrics.
- Letter spacing stays at `0`.
- Do not add display or serif fonts unless PicLens gains a non-tooling marketing surface.

## 4. Spacing & Layout

### Base Unit

All spacing derives from a base of 4px.

| Token | Value | Usage |
|-------|-------|-------|
| space-1 | 4 | Tile inner padding |
| space-2 | 8 | Button groups, dialog button gaps |
| space-3 | 12 | Grid/sidebar compact spacing |
| space-4 | 16 | Sidebar padding, grid margin |
| space-5 | 20 | Main content padding |
| space-6 | 24 | Status/footer horizontal padding |

### Grid

- Main shell: title bar, folder sidebar, resizable splitter, library content, status bar.
- Thumbnail grid: `ItemsRepeater` with `UniformGridLayout`.

### Rules

- Keep layout values compact and multiples of 4px where practical.
- Preserve the resizable sidebar and virtualized thumbnail grid.

## 5. Components

### Toolbar Buttons

- **Structure**: `Button.toolbar` with Fluent icon and optional text.
- **Spacing**: 7px icon/text spacing, compact 36px minimum height.
- **States**: default transparent, hover uses `HoverBrush`, disabled uses opacity.
- **Accessibility**: tooltip plus automation IDs for primary controls.

### Library Tile

- **Structure**: thumbnail/icon frame plus wrapped file name.
- **States**: hover, selected, drop target.
- **Accessibility**: automation ID and automation name from tile view model.

## 6. Motion & Interaction

### Timing

| Type | Duration | Easing | Usage |
|------|----------|--------|-------|
| Immediate | 0ms | none | Folder navigation and selection |
| Timer/debounce | 250ms | n/a | Thumbnail size commit |
| Autoscroll tick | 33ms | n/a | Drag autoscroll |

### Rules

- Avoid decorative motion; image browsing should stay responsive.
- Drag, selection, zoom, and pan feedback must remain direct.

## 7. Depth & Surface

### Strategy

PicLens uses borders plus tonal shifts. Surfaces are separated with `LineBrush` and near-neutral backgrounds; shadows are avoided in the main shell.

| Type | Value | Usage |
|------|-------|-------|
| Default border | 1px `LineBrush` | Pane dividers, tile frame, status bar |
| Selected fill | `SelectedBrush` | Active library selection |
| Drop target border | 2px `AccentBrush` | Drag rename target |
