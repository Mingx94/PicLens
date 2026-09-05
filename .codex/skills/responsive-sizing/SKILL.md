---
name: responsive-sizing
description: Review layout constraints when window size or display scaling causes poor fit.
---

# Responsive Sizing

Size components from their content constraints rather than treating a column grid as a universal answer.

## Review

- Use fixed widths for regions that should remain stable and flexible widths for regions that should absorb available space.
- Use the framework's maximum-size constraint when a component has an optimal readable size and should shrink only when necessary.
- Distinguish a smaller window from user-selected text or display scaling. Adjust spacing, wrapping, or layout when content does not fit; do not counteract user scaling by shrinking text or controls. Reduce large type only when a demonstrated layout problem justifies it and readability is preserved.
- Tune component padding independently across sizes rather than uniformly zooming the component.
- Inspect narrow and wide window sizes where they expose real constraints. In desktop apps, account for minimum window size and DPI scaling; use browser breakpoints only for web interfaces.
- Keep internal grids local to the content that benefits from them.

## Output

Explain affected size constraints and overflow or wrapping risks. Verify representative window sizes and display scales for the changed behavior; add a decision table only when comparing multiple regions.
