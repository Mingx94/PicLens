---
name: ui-polish
description: Refine visual details once layout, hierarchy, and behavior are sound.
---

# UI Polish

Improve what already exists before introducing new decorative elements.

## Review

- Enhance native content such as bullets, quotations, links, checkboxes, and selected controls where that supports meaning.
- Use accent borders sparingly to mark active, branded, or important regions.
- Add background color, a gentle gradient, low-contrast pattern, or simple shape only when readability remains protected.
- Remove borders that duplicate separation already created by spacing, surface color, or shadow.
- Keep decorative styles consistent with the interface personality and token system.
- Verify states affected by the change, such as hover, focus, dark mode, high contrast, reduced motion, or window resizing. Product support for a state alone does not require retesting it.

## Output

Return a short ranked list of affected components, intended effects, and smallest changes. Include a rollback condition only when a change has a meaningful regression risk. Do not conceal unresolved hierarchy, spacing, content, or accessibility problems with decoration.
