---
name: find-animation-opportunities
description: Inspect a GPUI desktop interface and propose only the few places where motion would improve feedback, state continuity, or direct manipulation. Read-only; do not review existing motion or edit code.
metadata:
  short-description: Find useful GPUI motion opportunities
---

# Find GPUI Animation Opportunities

Default to no animation. Product UI must stay fast under repeated desktop use.

## Gate every candidate

1. Frequency: reject motion on keyboard-heavy and very frequent paths unless it is immediate functional feedback.
2. Purpose: require feedback, spatial continuity, state indication, or removal of a jarring change.
3. Interruption: define what happens when input reverses or repeats mid-flight.
4. Accessibility: define the reduced-motion result and a keyboard-equivalent action.
5. Cost: reject motion that requires broad relayout, continuous idle frames, or custom infrastructure without clear value.

Cap the report at five opportunities. For each, give `file:line`, current behavior, proposed GPUI mechanism, purpose, frequency, interruption behavior, and reduced-motion result. Also name two to five tempting candidates that should remain static and state which gate rejected them.

Prefer existing gpui-component behavior, finite GPUI animation for small decorative changes, and explicit spring state only for direct or interruptible motion.

Read [the GPUI motion reference](../build-gpui-apps/references/motion-input.md) before making recommendations.
