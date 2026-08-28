---
name: animate
description: Design, implement, debug, review, and validate native motion in Rust GPUI desktop apps. Use for finite transitions, entrances, exits, press feedback, overlays, springs, drag, momentum, reduced motion, vestibular safety, focus during transitions, or keyboard alternatives. Do not use Web CSS, DOM, React, or browser animation APIs.
metadata:
  short-description: Build native GPUI motion
---

# Animate GPUI Interfaces

Use this as the single entrypoint for GPUI motion. Build motion that explains
state, stays responsive under desktop input, and remains accessible.

## Before implementation

1. Inspect the pinned GPUI revision and similar local call sites. Treat the checkout as the API authority.
2. State the trigger, purpose, start and end presentation, semantic end state,
   interruption behavior, reduced-motion result, and test oracle.
3. Cut animation from keyboard-heavy and high-frequency paths unless it is necessary feedback.
4. Keep product state authoritative. Animation controls presentation only.

## Choose the mechanism

- Use element hover, active, and focus styles for immediate feedback that needs no timeline.
- Use `AnimationExt::with_animation` for fades, small translation or scale,
  disclosures, anchored overlays, and other short finite presentation changes.
- Use explicit entity-owned state plus `window.request_animation_frame()` for interruptible springs, drag, momentum, or retargeting.
- Use ordinary GPUI layout and paint first. Use `canvas` or a custom `Element` only when normal elements cannot express the effect.
- Reuse existing gpui-component overlays, lists, scroll behavior, and controls before creating custom motion infrastructure.

## Finite motion

- Keep the semantic end state correct when motion is cancelled or skipped.
- Keep outgoing content renderable until its exit presentation completes.
- Use the same anchor and direction for entry and exit. A shorter exit is fine.
- Avoid repeatedly laying out large subtrees. Prefer opacity and small transforms.
- Do not use a finite timeline for a value the user can grab or reverse.
- Never delay keyboard input, focus transfer, commands, or accessibility state.
- Translate Web motion concepts into GPUI presentation state. Do not copy CSS
  properties, browser easing assumptions, or React lifecycle patterns.

## GPUI contract

- Start direct manipulation 1:1 with input and preserve the grab offset.
- Retarget from the current presentation value and velocity.
- Request frames only while values change. Cap long frame steps after stalls.
- Keep input, focus, keyboard alternatives, and hit testing active while motion runs.
- Call `cx.notify()` when animation state changes rendered output.
- Keep `Task`, `Subscription`, and entity ownership explicit. Do not detach animation work without app-lifetime intent.

## Accessible motion

- Reduced motion changes behavior, not only duration. Snap spatial movement or
  use a short non-spatial state change when continuity still matters.
- Remove bounce, parallax, elastic overshoot, large repeated zoom, decorative
  loops, rapid flashing, and unbounded oscillation.
- Preserve functional feedback, progress meaning, selection, and validation.
  Do not use motion or color as the only signal.
- Keep stable and visible focus, semantic roles, keyboard actions, and correct
  disabled state while presentation changes.
- Give drag operations a keyboard alternative and restore or commit focus after
  completion or cancellation.
- Provide opaque and increased-contrast fallbacks when motion is combined with
  material or transparency.
- Inspect how the pinned GPUI revision exposes platform preferences. Do not
  assume a Web media query or an API location from another revision.

Use small travel and restrained bounce for desktop product UI. Starting values are hypotheses: about 100-180 ms for tiny feedback, 180-280 ms for small anchored overlays, and 240-420 ms for larger navigation.

Read [the GPUI motion reference](../build-gpui-apps/references/motion-input.md)
before implementing finite or custom motion. For preference mapping, focus, or
platform accessibility behavior, also read [the accessibility reference](../build-gpui-apps/references/accessibility-platform.md).
For reusable spring math, adapt [spring.rs](../build-gpui-apps/assets/spring.rs)
to the target revision.

## Validation

Run repository checks, launch the real app, and test re-triggering, cancellation,
focus, keyboard and pointer paths, resize, scale factor,
inactive/reactivated windows, normal and reduced-motion behavior, and
production-like content. Do not call motion complete from `cargo check` alone.
