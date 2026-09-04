# PicLens design system

PicLens is a calm, reliable, local-first, image-first desktop workspace. The current interface uses Rust, egui, eframe, and wgpu. The implementation in `crates/piclens-desktop/src/theme.rs` and `crates/piclens-desktop/src/ui/mod.rs` is the executable authority.

## Structure

- The top command surface owns product identity and the sidebar control.
- A resizable 230 px sidebar owns the folder tree and can collapse. Its allowed range is 160 to 360 px.
- The main surface owns folder context, sort and scope controls, gallery content, and file operations.
- The main surface also owns counts, thumbnail size, status, and errors.
- The viewer and confirmation dialogs render as layers in the main window.

The gallery uses `egui::ScrollArea::show_rows` to virtualize fixed grid rows. Keep shared dialogs and overlays outside repeated rows.

## Palette

The app follows the operating-system light or dark preference. On Windows, it checks high-contrast mode at startup and once per second while running. High-contrast mode uses the current Windows system colors. Shared semantic colors live in `theme.rs`; views must not create a second palette.

| Role | Value |
|---|---|
| App background | `#F5F6F8` |
| Command surface | `#FCFCFD` |
| Sidebar | `#F8F9FB` |
| Content surface | `#FFFFFF` |
| Tile frame | `#F2F3F5` |
| Border | `#E1E4E9` |
| Primary text | `#1D2026` |
| Secondary text | `#626975` |
| Accent | `#4968E8` |
| Selected | `#E8EEFF` |
| Viewer canvas | `#11141A` |
| Viewer control | `#252A33` |
| Viewer control hover | `#343B47` |
| Viewer error text | `#FF9696` |
| Modal backdrop | black at 35% opacity |

The table documents the light palette. The dark palette keeps the same semantic roles with darker surfaces and brighter foreground colors.

## Typography and assets

PicLens embeds Noto Sans CJK TC Regular, Medium, and Bold and registers them before the window opens. Use `Noto Sans CJK TC` for the interface. Keep the OFL notice with the fonts.

Use the packaged PicLens artwork for app identity. Use the selected Lucide SVG assets in `assets/Icons/Lucide` for interface actions and categories. Keep the outline style consistent and do not mix icon collections. Use an 18 px icon inside a control that is at least 32 px square. Familiar navigation, refresh, and zoom actions can use icons alone, but they must have a Traditional Chinese tooltip and AccessKit name. Keep text beside important, unfamiliar, or destructive actions.

Controls need stable IDs, clear AccessKit names and roles, keyboard access, disabled states, and visible state feedback.

## Layout and interaction

- Use a 4 px spacing base where practical.
- Use 5 px corner radii for controls and 6 px for menus and windows. Avoid pill-shaped controls.
- Keep the minimum window size at 800 x 600.
- At 800 px wide, remove the sidebar from layout and compress repeated library chrome so the gallery remains usable.
- Render gallery thumbnails in square containers. Preserve the source aspect ratio and use a centered cover crop without modifying the source image.
- Align folder icons to the same square preview region and place folder names on the same line as image filenames below that region.
- Keep unselected gallery tiles visually quiet. Show a surface and border on hover, selection, or an active drag target.
- Keep viewer controls on dark neutral surfaces so the image remains the strongest visual element.
- Keep blocking filesystem and image work outside render and off the application thread.
- Preserve direct selection, bounded thumbnail work, viewer focus return, and explicit confirmation for file mutations.
- Avoid decorative motion that competes with image browsing.
- Keep file operation results and errors visible and logged.

## Validation

For visual changes, launch the real app with an isolated profile and representative images. Check default and minimum window sizes, the grid gallery, empty and loading states, selection, dialogs, viewer, keyboard focus, and high-DPI rendering. Compilation alone does not prove layout, fonts, focus, or platform behavior.
