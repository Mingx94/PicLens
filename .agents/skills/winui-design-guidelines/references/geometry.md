# Geometry

Source: https://learn.microsoft.com/zh-tw/windows/apps/design/signature-experiences/geometry

Source snapshot: Microsoft Learn zh-tw page reviewed on 2026-06-06.

Use Windows 11 geometry to keep the app calm, predictable, and native.

## Guidelines

- Use default corner radii where possible:
  - 8 px: top-level containers, app windows, flyouts, dialogs.
  - 4 px: page controls such as buttons, list backgrounds, text boxes, combo boxes, progress bars, scroll bars, and sliders.
  - 4 px: tooltips even though they are overlays.
  - 0 px: edges that touch another straight edge, connected control parts, and snapped/maximized window corners.
- Override `ControlCornerRadius` and `OverlayCornerRadius` only as a deliberate app-wide design decision.
- Preserve connected edges for controls like split buttons or flyouts attached to their trigger.
