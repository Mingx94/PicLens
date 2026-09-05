---
name: PicLens
description: An image workbench for browsing and batch organization.
colors:
  accent: "#167074"
  selected: "#DCEFEE"
  app-background: "#EFF2F2"
  command-surface: "#FAFBFB"
  sidebar: "#EBEFEF"
  content: "#FFFFFF"
  tile: "#EDF1F1"
  border: "#D5DDDD"
  primary-text: "#1D2026"
  secondary-text: "#626975"
  danger: "#B72323"
typography:
  heading:
    fontFamily: "Noto Sans CJK TC"
    fontSize: "24px"
    fontWeight: 700
  body:
    fontFamily: "Noto Sans CJK TC"
    fontSize: "15px"
    fontWeight: 400
  button:
    fontFamily: "Noto Sans CJK TC"
    fontSize: "15px"
    fontWeight: 500
  small:
    fontFamily: "Noto Sans CJK TC"
    fontSize: "12.5px"
    fontWeight: 400
rounded:
  control: "5px"
  window: "6px"
spacing:
  item: "8px"
  content: "24px"
  compact-content: "16px"
components:
  button:
    backgroundColor: "{colors.tile}"
    textColor: "{colors.primary-text}"
    rounded: "{rounded.control}"
    padding: "6px 12px"
  button-hover:
    backgroundColor: "{colors.selected}"
    textColor: "{colors.primary-text}"
    rounded: "{rounded.control}"
  search:
    backgroundColor: "{colors.content}"
    textColor: "{colors.primary-text}"
    rounded: "{rounded.control}"
---

# Design System: PicLens

## Overview

**Creative North Star: "Image Workbench"**

Large image collections and batch organization have equal weight. Neutral surfaces keep photographs prominent. Teal identifies selection and interaction. Controls stay close to the results they affect.

**Key Characteristics:**
- Quiet image field with compact navigation.
- Visible selection, result operations, and status.
- Native desktop controls with system theme support.

This captures the implemented egui interface. [PRODUCT.md](PRODUCT.md) and the [product specification](docs/product/product-spec.md) define product scope. [The implementation guide](docs/design/system.md) adds platform and validation details. Measurements use egui logical points; frontmatter uses portable px notation.

## Colors

### Primary

Muted teal (`accent`) marks selection outlines and interactive states. Pale teal (`selected`) provides their supporting fill.

### Neutral

The light palette uses cool neutral surfaces, dark primary text, and muted secondary text. `command-surface` groups navigation and result feedback; `sidebar` separates folder navigation; `content` holds images. `danger` identifies errors and destructive actions.

These tokens describe light mode. Dark mode and Windows high-contrast system colors remain defined in `crates/piclens-desktop/src/theme.rs`. Views must use semantic palette roles instead of hard-coded light values.

## Typography

Use the bundled Noto Sans CJK TC Regular, Medium, and Bold fonts. Body and controls share a compact scale. Folder headings reduce to 20 logical points in compact layout. Long folder names and paths truncate and expose the full path on hover. Keep the font license with distributed assets.

## Layout

The top bar holds identity, sidebar visibility, history, refresh, and folder choice. A resizable folder tree sits beside the main image field. Its default width is 230 logical points, with a 160–360 range.

Folder context precedes the search and filter row. This row has no surrounding panel border; the search field retains its own border. Search moves above wrapped filters when the available row width is below 900 logical points or the window uses compact layout.

The result footer stays at the bottom of the gallery area. It holds result count, result operations, selection count, and status. Hide it only when the library has no ready result, the backend is ready, and there is no notice. A ready result with zero items still has a footer.

The minimum window size is 800 × 600. At width 800 or below, compact layout uses reduced margins and a separately toggled folder tree. The virtualized square thumbnail grid fills the remaining area with an 8-point gap.

## Elevation & Depth

Main surfaces use tonal separation and restrained borders. Unselected tiles remain quiet; hover, selection, and drag targets carry visible state. Viewer and dialog layers retain their existing egui treatments. Do not add decorative shadows to the gallery.

## Shapes

Controls have gently rounded corners. Menus and windows use the slightly larger window radius. Square image containers preserve aspect ratio through centered cover cropping; source files remain unchanged.

## Components

- Buttons use neutral fill, with teal hover and active feedback. Icon controls use 18-point Lucide artwork and targets at least 32 points square.
- Search has one outlined field and an unframed inner editor. Sorting, scope, and thumbnail size stay alongside it or wrap below.
- Gallery items align folder and image previews, then names. Selection must remain clear without turning every item into a raised card.
- Result operations remain in the fixed footer. Disabled controls communicate unavailable actions. Notices and errors remain visible there.
- The viewer uses a dark neutral canvas and control surfaces. Dialogs retain explicit action labels and focus behavior.

## Do's and Don'ts

- Do preserve visible focus, keyboard access, AccessKit names, and Traditional Chinese tooltips.
- Do preserve system dark mode and Windows high-contrast palette handling.
- Do keep images, selection, and operation results legible at narrow widths.
- Don't add decorative containers around the search and filter row.
- Don't treat screenshot review or automated layout tests as proof of native interaction, dark-mode, or high-contrast behavior.
