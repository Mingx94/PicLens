# PicLens design system

PicLens is a calm, image-first desktop workspace. The current interface uses Rust, GPUI, and gpui-component. The implementation in `crates/piclens-gpui/src/theme.rs` and `crates/piclens-gpui/src/app/` is the executable authority.

## Structure

- A 64 px command bar owns global navigation, search, and folder selection.
- A 228 px sidebar owns the folder tree and can collapse.
- The main surface owns folder context, sort and scope controls, gallery content, and file operations.
- A 48 px status bar owns counts and thumbnail size.
- The viewer and confirmation dialogs render as layers in the main window.

The gallery uses a virtualized GPUI list. Keep shared dialogs and overlays outside repeated rows.

## Palette

The app is light-only until a complete dark theme and runtime selection exist. Semantic colors live in `Theme`; views must not create a second palette.

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

`Theme::high_contrast` and `Theme::opaque` are tested fallback palettes, but the app does not select them from operating-system preferences. Do not claim automatic reduced-transparency or high-contrast support until that connection exists.

## Typography and assets

PicLens embeds Noto Sans CJK TC Regular, Medium, and Bold and registers them before the window opens. Use `Noto Sans CJK TC` for the interface. Keep the OFL notice with the fonts.

Use the packaged PicLens artwork for app identity and gpui-component icons for commands. Use the [Lucide icon catalog](https://lucide.dev/icons/) to find a suitable icon and check the available `gpui_component::IconName` variants before implementation. Controls need stable IDs, clear labels, keyboard access, disabled states, and visible state feedback.

## Layout and interaction

- Use a 4 px spacing base where practical.
- Keep the minimum window size at 480 x 320.
- Keep blocking filesystem and image work outside render and off the application thread.
- Preserve direct selection, bounded thumbnail work, viewer focus return, and explicit confirmation for file mutations.
- Avoid decorative motion that competes with image browsing.
- Keep file operation results and errors visible and logged.

## Validation

For visual changes, launch the real app with an isolated profile and representative images. Check default and minimum window sizes, gallery and list modes, empty and loading states, selection, dialogs, viewer, keyboard focus, and high-DPI rendering. Compilation alone does not prove layout, fonts, focus, or platform behavior.
