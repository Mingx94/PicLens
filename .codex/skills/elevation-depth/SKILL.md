---
name: elevation-depth
description: Design or audit interface depth using light direction, elevation scales, shadows, color, overlap, and interaction states. Use when surfaces feel flat, arbitrary, overly shadowed, or spatially inconsistent.
---

# Elevation and Depth

Make depth communicate spatial relationships and interaction instead of serving as arbitrary decoration.

## Review

- Establish one implied light direction and keep highlights and shadows consistent with it.
- Use a small elevation scale: tight subtle shadows near the surface and broader softer shadows for higher layers.
- Consider two-part shadows when ambient contact and cast shadow need separate control.
- Reduce the contact shadow as an object moves farther from the surface.
- Change elevation meaningfully during dragging, pressing, opening, or focusing.
- For flatter styles, use lighter or darker surfaces, solid offset shadows, or overlap before adding blur.
- Separate overlapping images with a background-colored gap when their edges would clash.

## Output

Provide an elevation-role table, shadow or surface tokens, interaction transitions, and rendered acceptance checks. Avoid photorealistic effects or extra layers that do not improve comprehension.
