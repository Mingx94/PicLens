---
name: winui-design-guidelines
description: Use when designing, implementing, or reviewing WinUI 3 and Windows app UI against Microsoft Windows app design guidelines and Fluent Design. Applies to XAML layout, NavigationView, TabView, title bars, commands, color, typography, iconography, geometry, materials such as Mica and Acrylic, motion, accessibility, localization, widgets, and UI copy.
---

# WinUI Design Guidelines

## Quick Use

Use this skill to make WinUI 3 UI decisions feel native to Windows 11.

1. Identify the task type: new UI design, XAML implementation, visual polish, accessibility review, or code review.
2. Use the Reference Map below and read only the relevant reference files.
3. Prefer built-in WinUI controls, theme resources, text styles, icons, transitions, and system behaviors before custom visuals.
4. Check the design across light, dark, high contrast, text scaling, keyboard use, and three effective-width classes: `<640`, `641-1007`, and `>=1008` epx.
5. When changing code, keep the UI consistent with the app's existing architecture while correcting guideline violations.

## Decision Flow

### Navigation

- Use `NavigationView` for primary app destinations.
- Use top navigation when every destination should be visible, vertical content space is important, or icons alone would be ambiguous.
- Use left navigation when there are more than five top-level items or page switching is not constant.
- Use `TabView` for document/browser-like experiences where users open, close, reorder, or move tabs.
- Use `BreadcrumbBar` when navigation is deeper than two levels or users need to return to any parent level.
- Use list/details when users frequently switch between sibling items and inspect or edit details.
- Avoid deep hierarchies and "bounce-stick" paths where users must go up and down just to reach related content.

### Commands

- Put commands near the object they affect when the command is core and frequent.
- Use `CommandBar`, `MenuBar`, menus, context menus, or `CommandBarFlyout` to group secondary or contextual actions.
- Prefer direct manipulation where it is clearer than a command.
- Use confirmation dialogs only for irreversible or high-consequence actions; use undo for reversible actions.
- Provide feedback only when it adds value and integrate it into the UI when possible.

### Layout

- Design in effective pixels, not physical pixels.
- Align control sizes, margins, and positions to multiples of 4 epx.
- Use responsive techniques: reposition, resize, reflow, show/hide, or restructure at breakpoints.
- Keep title bar behavior native: draggable empty regions, system menu on right-click/press-hold, double-click to maximize/restore.
- Use app silhouettes intentionally: top navigation, left navigation, menu bar, or tab-view shape.

### Visual System

- Use theme resources and system brushes; avoid hard-coded colors unless there is a clear product reason.
- Use accent color sparingly for important interactive states or emphasis.
- Do not rely on color alone to convey meaning.
- Use `Segoe UI Variable` and WinUI text styles; avoid viewport-dependent font scaling.
- Use Windows 11 geometry defaults unless the product has a coherent reason: 4 px for controls, 8 px for top-level transient surfaces.
- Use Mica, Acrylic, and Smoke for their intended surfaces only.
- Use Segoe Fluent Icons or established WinUI iconography for command, navigation, and status icons.
- Use WinUI transitions and motion resources before custom animation.

### Usability And Text

- Treat accessibility, localization, settings persistence, and help as design requirements, not polish.
- UI copy should be warm, helpful, concise, and action-oriented.
- Error messages should state what happened, what it means, and what the user can do next without blame.
- Button labels should be short action verbs or verb phrases.

## Reference

Use `references/guidelines.md` as the routing index and source map.

Load only the targeted files needed for the task:

- `references/overview.md` for overall principles and source snapshot context.
- `references/navigation.md` for app structure, `NavigationView`, `TabView`, breadcrumbs, list/details, and back behavior.
- `references/commanding.md` for buttons, command surfaces, direct manipulation, feedback, dialogs, confirmation, and undo.
- `references/layout.md` for effective pixels, breakpoints, responsive design, app silhouettes, and title bars.
- `references/color.md` for theme, accent color, contrast, and color accessibility.
- `references/typography.md` for Segoe UI Variable, type ramp, sizing, alignment, and truncation.
- `references/geometry.md` for corner radii and connected edges.
- `references/layering-and-elevation.md` for layers, elevation, shadows, strokes, and focus hierarchy.
- `references/materials.md` for Mica, Acrylic, and Smoke.
- `references/iconography.md` for Segoe Fluent Icons, modifiers, layering, and localization.
- `references/motion.md` for WinUI transitions, connected animations, timing, and easing.
- `references/usability.md` for accessibility, app settings, localization, and help.
- `references/widgets.md` for Windows widget principles and Adaptive Card surfaces.
- `references/writing-style.md` for UI copy, errors, dialogs, and button labels.
- `references/review-checklist.md` for final review passes.

If the user asks for the latest Microsoft guidance, or if the answer depends on current platform behavior, refresh the linked Microsoft Learn pages before giving final guidance.
