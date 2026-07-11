# PicLens Design System

## 1. Atmosphere & Identity

PicLens is a quiet desktop workspace for browsing and organizing local image folders. It should feel focused, dense enough for repeated file work, and visually calm around the images. The signature is an image-first workbench: neutral layered surfaces, a single cobalt accent, compact commands, and generous breathing room around library content.

The app uses three visually distinct zones:

- A compact command bar for global navigation, search, and opening folders.
- A calm Folder Tree pane that anchors navigation without competing with images.
- A raised white Library workspace that owns folder context, display controls, and content.

## 2. Color

PicLens intentionally supports the light theme only. Keep `RequestedThemeVariant="Light"` until a complete dark palette and visual test coverage are added.

### Palette

| Role | Token | Light | Dark | Usage |
|------|-------|-------|------|-------|
| Surface/app | `AppBackgroundBrush` | `#F5F6F8` | N/A | Main shell background |
| Surface/command | `CommandBarBrush` | `#FCFCFD` | N/A | Global command and status bars |
| Surface/sidebar | `SidebarBrush` | `#F8F9FB` | N/A | Folder pane |
| Surface/content | `SurfaceBrush` | `#FFFFFF` | N/A | Main library surface |
| Surface/tile | `TileFrameBrush` | `#F2F3F5` | N/A | Thumbnail tile frame |
| Border/default | `LineBrush` | `#E1E4E9` | N/A | Dividers and tile borders |
| Border/strong | `StrongLineBrush` | `#CBD0D8` | N/A | Interactive hover boundary |
| Text/primary | `PrimaryTextBrush` | `#1D2026` | N/A | Main labels and file names |
| Text/secondary | `SecondaryTextBrush` | `#626975` | N/A | Paths, metadata, viewer filename |
| Text/muted | `MutedTextBrush` | `#7A828F` | N/A | Lower emphasis icons/text |
| State/hover | `HoverBrush` | `#EEF1F5` | N/A | Toolbar hover |
| State/selected | `SelectedBrush` | `#E8EEFF` | N/A | Selected Library Item |
| Accent/primary | `AccentBrush` | `#4968E8` | N/A | Primary action, selection, and status |
| Accent/soft | `AccentSoftBrush` | `#EEF2FF` | N/A | Empty-state icon and active scope |
| Accent/soft pressed | `AccentSoftPressedBrush` | `#DBE4FF` | N/A | Pressed active toggles |
| Brand/shell | `brandShell` | `#EAF7FA` | N/A | In-app brand mark shell |
| Brand/outline | `brandOutline` | `#B9DDE5` | N/A | In-app brand mark boundary |
| Brand/sky | `brandSky` | `#45A9D4` | N/A | In-app brand mark image field |
| Brand/sun | `brandSun` | `#FFCA52` | N/A | In-app brand mark sun |
| Brand/hill | `brandHill` | `#2CB49D` | N/A | In-app brand mark foreground hill |
| Brand/mountain | `brandMountain` | `#155DBB` | N/A | In-app brand mark primary mountain |
| Surface/viewer | `ViewerCanvasBrush` | `#11141A` | Stable dark surface | Image Viewer canvas |

### Rules

- Keep color functional: surfaces, text hierarchy, selection, and status only.
- Add new colors as named properties in `Theme.qml` before using them in views.

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

- Primary: embedded `Noto Sans CJK TC` from `assets/Fonts`.
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
| space-7 | 28 | Gallery outer margin |

### Grid

- Main shell: 64px global command bar, Folder Tree, resizable splitter, Library workspace, 48px status bar.
- Thumbnail grid: virtualized, reusable `GridView` delegates; new profiles default to 160px thumbnails.

### Rules

- Keep layout values compact and multiples of 4px where practical.
- Preserve the resizable sidebar and virtualized thumbnail grid.
- Keep global commands in the top bar and Library-specific sort, scope, and view controls in the Library header.
- Align both Library header edges to `space-7`, matching the gallery content inset.
- Keep the top search field geometrically centered at the default window size; at narrow widths it occupies the safe gap between command groups.
- Keep thumbnail sizing in the status-bar slider; do not duplicate it with a Library-header menu.

## 5. Components

### Toolbar Buttons

- **Structure**: `Button.toolbar` with Fluent icon and optional text.
- **Spacing**: 7px icon/text spacing, compact 36-38px minimum height.
- **States**: default transparent, hover uses `HoverBrush`, disabled uses opacity.
- **Accessibility**: tooltip plus automation IDs for primary controls.
- **Primary action**: only `開啟資料夾` uses a filled cobalt treatment in the main command bar.

### Icons

- Use the shared `AppIcon.qml` 24×24 vector coordinate system for command, navigation, view, search, and folder-tree disclosure icons.
- Keep icon strokes round, optically centered, and consistent across Windows and Linux; do not substitute platform-dependent symbol fonts or Unicode glyphs for toolbar icons.
- Use `LensMark.qml` for the compact in-app brand mark. Its simplified sun-and-mountain composition derives from the packaged application icon, which remains the authority for operating-system surfaces.

### Library Tile

- **Structure**: thumbnail/icon frame plus wrapped file name.
- **States**: hover, selected, drop target.
- **Accessibility**: automation ID and automation name from tile view model.
- **Selection**: use a 2px cobalt outline, pale cobalt fill, stronger filename color, and a visible check badge so selection is not color-only.

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

## 8. Interface Reference

The generated high-fidelity direction is stored at `docs/design/piclens-ui-concept.png`. It is a visual reference rather than a pixel-perfect contract: runtime behavior, responsive constraints, accessibility, and the tokens in `Theme.qml` remain authoritative.
