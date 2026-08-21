---
name: animation-vocabulary
description: Name a desktop UI motion effect from a loose description and map it to the relevant GPUI concept. Use for terminology, not design or implementation.
metadata:
  short-description: Name GPUI motion effects
---

# Desktop Motion Vocabulary

Return the best term first, then at most two close alternatives.

- **Finite animation** - A bounded presentation change driven by elapsed progress.
- **Spring animation** - Motion modeled with position, velocity, stiffness, damping, and mass.
- **Interruptibility** - Retargeting from the current presentation and velocity without a jump.
- **Direct manipulation** - Presentation follows pointer or touch input 1:1.
- **Momentum** - Motion continues after release using captured velocity and bounded decay or settling.
- **Rubber-banding** - Presentation moves past a bound with increasing resistance, then returns.
- **Retargeting** - Changing a moving object's destination while preserving continuity.
- **Press feedback** - Immediate visual response while a control is held.
- **Anchored entry** - An overlay appears from the control or edge that caused it.
- **Crossfade** - One state fades out while another fades in with a short overlap.
- **Shared-element transition** - A presentation preserves object identity across two layouts or views.
- **Layout animation** - Presentation interpolates between measured layout states.
- **Stagger** - Related items begin motion with small ordered delays.
- **Reduced motion** - A preference-aware variant that removes large spatial movement, overshoot, parallax, and decorative loops.
- **Frame budget** - The time available to update, layout, paint, and present one frame.
- **Presentation state** - Temporary visual state kept separate from committed product state.
- **Semantic target** - A valid product endpoint to which presentation settles.

Do not answer with CSS, DOM, React, or browser API names unless the user is explicitly comparing Web terminology with GPUI.
