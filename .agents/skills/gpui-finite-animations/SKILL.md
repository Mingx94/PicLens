---
name: gpui-finite-animations
description: Build and debug short finite animations with GPUI element animation APIs. Use for fades, small scale or translation feedback, disclosures, anchored overlay entry and exit, bounded loops, or migration from CSS transitions and keyframes. Do not use for drag, momentum, or retargetable springs.
metadata:
  short-description: Build finite GPUI animations
---

# GPUI Finite Animations

Use the smallest animation mechanism supported by the pinned GPUI revision.

## Translate Web concepts correctly

- A CSS transition becomes a finite GPUI presentation derived from elapsed progress.
- A keyframe sequence becomes a small piecewise interpolation only when it is truly autonomous and bounded.
- `transform-origin` becomes explicit geometry around the trigger or anchor.
- DOM mount and unmount animation becomes entity state that keeps the outgoing presentation renderable until completion.
- CSS reduced-motion media queries become GPUI preference checks or the project preference layer.

Do not copy CSS properties, browser easing assumptions, React lifecycle patterns, or GPU claims into GPUI code.

## Rules

- Inspect `AnimationExt::with_animation` and local examples at the exact locked revision.
- Keep the semantic end state correct even if the animation is cancelled or skipped.
- Prefer opacity, small translation, and small scale-like presentation changes. Avoid repeated layout of large subtrees.
- Use the same anchor and direction for entry and exit. Make exits shorter when that helps the user continue.
- Do not use a finite timeline for a value the user can grab or reverse. Use explicit motion state instead.
- Never delay keyboard input, focus transfer, command handling, or accessibility state until animation completion.

Read [the GPUI motion reference](../build-gpui-apps/references/motion-input.md) before implementation.
