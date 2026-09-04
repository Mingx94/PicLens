---
name: image-integration
description: Audit the visual integration of photos, background images, screenshots, icons, logos, and user-uploaded media. Use when image quality, scaling, cropping, or text contrast weakens an interface.
---

# Image Integration

Treat image selection and presentation as part of the design, not replaceable decoration.

## Review

- Match image quality and specificity to its visual prominence.
- Make text contrast consistent across background imagery using an overlay, tonal adjustment, colorization, or restrained text glow.
- Keep small icons near their intended detail scale; place them inside a larger supporting shape when more visual presence is needed.
- Avoid shrinking detailed screenshots until their text becomes illegible; use a narrower capture, crop, or intentionally simplified illustration.
- Redraw small logo variants when automatic downscaling destroys detail.
- Put user-uploaded images in controlled aspect-ratio containers with an intentional crop position.
- Prevent same-color background bleed with a subtle inner shadow or translucent inner edge.

## Output

Return findings by asset, recommended treatment, content-loss risks, responsive behavior, and checks at actual rendered sizes. Do not alter or crop source assets destructively without explicit authorization.
