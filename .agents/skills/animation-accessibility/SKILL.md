---
name: animation-accessibility
description: Design, implement, or review accessible motion in GPUI desktop apps. Use for reduced motion, vestibular safety, keyboard equivalents, focus during transitions, decorative loops, flashing, drag alternatives, and preference-aware fallbacks.
metadata:
  short-description: Make GPUI motion accessible
---

# Accessible GPUI Motion

Reduced motion changes behavior, not only duration.

- Snap navigation and context changes when travel does not aid understanding.
- Use a short non-spatial state change when continuity still matters.
- Remove parallax, elastic overshoot, large zoom, repeated travel, and decorative loops.
- Never remove functional feedback, progress meaning, selection state, or validation state.
- Do not use motion or color as the only signal.
- Keep stable focus, visible focus, keyboard actions, semantic roles, and correct disabled state while presentation changes.
- Give drag operations a keyboard alternative and restore or commit focus after completion or cancellation.
- Avoid rapid flashing, large repeated zoom, and unbounded oscillation.
- Provide opaque and increased-contrast fallbacks when motion is combined with material or transparency.

Inspect how the pinned GPUI revision and project expose platform preferences. Do not assume a Web media query or a GPUI API location from another revision.

Validate the normal and reduced-motion paths in the real app. Read [the GPUI motion reference](../build-gpui-apps/references/motion-input.md) and [the platform accessibility reference](../build-gpui-apps/references/accessibility-platform.md).
