# PicLens Design System

## 1. Atmosphere & Identity

PicLens is a quiet desktop workspace for browsing and organizing local image folders. It should feel focused, dense enough for repeated file work, and visually calm around the images. The signature is an image-first workbench: neutral layered surfaces, a single cobalt accent, compact commands, and generous breathing room around library content.

The app uses three visually distinct zones:

- A compact command bar for global navigation, search, and opening folders.
- A calm Folder Tree pane that anchors navigation without competing with images.
- A raised white Library workspace that owns folder context, display controls, and content.

## 2. Color

### Palette

| Role | Token | Light | Dark | Usage |
|------|-------|-------|------|-------|
| Surface/app | `AppBackgroundBrush` | `#F4F5F7` | TBD | Main shell background |
| Surface/command | `CommandBarBrush` | `#FBFBFC` | TBD | Global command and status bars |
| Surface/sidebar | `SidebarBrush` | `#F8F9FB` | TBD | Folder pane |
| Surface/content | `SurfaceBrush` | `#FFFFFF` | TBD | Main library surface |
| Surface/tile | `TileFrameBrush` | `#ECEEF2` | TBD | Thumbnail tile frame |
| Border/default | `LineBrush` | `#E2E5EA` | TBD | Dividers and tile borders |
| Border/strong | `StrongLineBrush` | `#D5D9E1` | TBD | Interactive hover boundary |
| Text/primary | `PrimaryTextBrush` | `#20242B` | TBD | Main labels and file names |
| Text/secondary | `SecondaryTextBrush` | `#5F6672` | TBD | Paths, metadata, viewer filename |
| Text/muted | `MutedTextBrush` | `#8A919D` | TBD | Lower emphasis icons/text |
| State/hover | `HoverBrush` | `#ECEFF4` | TBD | Toolbar hover |
| State/selected | `SelectedBrush` | `#E7EEFF` | TBD | Selected Library Item |
| Accent/primary | `AccentBrush` | `#4968E8` | TBD | Primary action, selection, and status |
| Accent/soft | `AccentSoftBrush` | `#EDF1FF` | TBD | Empty-state icon and active scope |
| Surface/viewer | `ViewerCanvasBrush` | `#11141A` | TBD | Image Viewer canvas |

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

- Main shell: global command bar, Folder Tree, resizable splitter, Library workspace, status bar.
- Thumbnail grid: `ItemsRepeater` with `UniformGridLayout`.

### Rules

- Keep layout values compact and multiples of 4px where practical.
- Preserve the resizable sidebar and virtualized thumbnail grid.
- Keep global commands in the top bar and Library-specific sort, scope, and view controls in the Library header.

## 5. Components

### Toolbar Buttons

- **Structure**: `Button.toolbar` with Fluent icon and optional text.
- **Spacing**: 7px icon/text spacing, compact 36-38px minimum height.
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
| Default border | 1px `LineBrush` | Pane dividers, Library workspace, tile frame, status bar |
| Selected fill | `SelectedBrush` | Active library selection |
| Drop target border | 2px `AccentBrush` | Drag rename target |
