# Review Checklist

Derived from the topical reference files in this skill. Use `guidelines.md` for the full source map.

Use this checklist for WinUI design reviews or implementation passes.

## Checklist

- Navigation: top-level destinations are clear; hierarchy is no deeper than needed; breadcrumbs exist when needed; back behavior matches user expectations.
- Commands: frequent actions are easy to find; destructive actions use confirmation only when irreversible/high consequence; reversible actions offer undo.
- Layout: design handles small, medium, and large widths; sizes/margins use effective pixels and 4 epx multiples; text does not overlap or clip unexpectedly.
- Title bar: custom title bar preserves drag regions, system menu, double-click maximize/restore, text scaling, and standard button affordances.
- Color/theme: light, dark, and high contrast work; accent color is sparse; color is not the only signal.
- Typography: uses Segoe UI Variable/WinUI text styles; hierarchy is clear; minimum sizes and truncation are acceptable.
- Geometry/elevation: corner radii and shadows match Windows conventions; elevation clarifies hierarchy.
- Materials: Mica/Acrylic/Smoke are used only where appropriate and degrade gracefully.
- Icons: icons are recognizable, standard where possible, accessible, and paired with labels when meaning is not obvious.
- Motion: animations are short, contextual, and based on WinUI/platform resources.
- Accessibility: keyboard, focus, screen reader names, contrast, touch targets, and text scaling are covered.
- Localization: strings can expand; cultural assumptions are avoided; RTL is considered where relevant.
- Writing: copy is concise, helpful, action-oriented, and not blameful.
