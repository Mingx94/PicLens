---
name: color-system
description: Build or rationalize a complete interface color system with neutral, primary, accent, and semantic scales. Use when a palette is too small, inconsistent, or generated from arbitrary one-off colors.
---

# Color System

Create enough deliberate choices for real interface states without allowing indistinguishable shade proliferation.

## Method

1. Inventory colors by actual role and usage.
2. Define neutral, primary, accent, and semantic families only as required by current components.
3. For each family, choose a useful middle color plus realistic light and dark edge cases, then fill visibly distinct gaps.
4. Tune shades by eye in their intended UI context; do not blindly apply lighten or darken formulas.
5. Preserve perceived intensity in very light and dark shades by adjusting saturation and, when appropriate, hue modestly.
6. Give neutral scales a consistent warm, cool, or neutral character when that supports the product personality.

## Output

Return color scales in the project's native format, role mappings, contrast-sensitive pairings to verify, deprecated colors, and migration guidance. Do not replace an established brand palette unless requested.
