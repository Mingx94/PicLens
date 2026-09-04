---
name: responsive-sizing
description: Review responsive sizing and fixed-versus-fluid layout decisions. Use when grids, proportional scaling, or breakpoints make components awkward across viewport sizes.
---

# Responsive Sizing

Size components from their content constraints rather than treating a column grid as a universal answer.

## Review

- Use fixed widths for regions that should remain stable and flexible widths for regions that should absorb available space.
- Prefer `max-width` when a component has an optimal readable size and should shrink only when necessary.
- Let large typography and generous spacing contract faster than already-small controls and text.
- Tune component padding independently across sizes rather than uniformly zooming the component.
- Start from a narrow viewport when that exposes real constraints, then restore enhancements at wider sizes.
- Keep internal grids local to the content that benefits from them.

## Output

Return a fixed/fluid/max-width decision table, breakpoint rationale, likely overflow or wrapping risks, and checks for narrow, intermediate, and wide viewports. Do not invent breakpoints unsupported by content behavior.
