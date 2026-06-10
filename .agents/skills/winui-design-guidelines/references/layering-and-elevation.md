# Layering And Elevation

Source: https://learn.microsoft.com/zh-tw/windows/apps/design/signature-experiences/layering

Source snapshot: Microsoft Learn zh-tw page reviewed on 2026-06-06.

Layering and elevation communicate hierarchy, focus, and navigation.

## Guidelines

- Use shadows and outlines subtly to indicate elevation when surfaces overlap.
- Keep elevation consistent with Windows defaults:
  - Window and modal dialog: elevation 128, 1 px stroke.
  - Flyout: elevation 32, 1 px stroke.
  - Tooltip: elevation 16, 1 px stroke.
  - Card: elevation 8, 1 px stroke.
  - Control: elevation 2, 1 px stroke.
  - Layer: elevation 1, 1 px stroke.
- Pressed controls generally lower elevation compared with rest/hover states.
- Use a two-layer mental model:
  - Base layer: app foundation, navigation, menus, and command surfaces.
  - Content layer: the core task surface, either continuous or split into cards.
- Do not add elevation just for decoration; it should clarify hierarchy or focus.
