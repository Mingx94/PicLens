---
name: animate
description: Design, implement, debug, and validate motion in Rust GPUI desktop apps. Use for transitions, entrances, exits, press feedback, overlays, view changes, springs, drag, momentum, or motion that feels slow or unstable. Do not use Web CSS, DOM, React, or browser animation APIs.
metadata:
  short-description: Build native GPUI motion
---

# Animate GPUI Interfaces

Build motion that explains state and stays responsive under desktop input.

## Before implementation

1. Inspect the pinned GPUI revision and similar local call sites. Treat the checkout as the API authority.
2. State the trigger, purpose, start and end presentation, interruption behavior, reduced-motion result, and test oracle.
3. Cut animation from keyboard-heavy and high-frequency paths unless it is necessary feedback.
4. Keep product state authoritative. Animation controls presentation only.

## Choose the mechanism

- Use element hover, active, and focus styles for immediate feedback that needs no timeline.
- Use `AnimationExt::with_animation` for short finite decorative motion when the pinned GPUI revision supports it.
- Use explicit entity-owned state plus `window.request_animation_frame()` for interruptible springs, drag, momentum, or retargeting.
- Use ordinary GPUI layout and paint first. Use `canvas` or a custom `Element` only when normal elements cannot express the effect.
- Reuse existing gpui-component overlays, lists, scroll behavior, and controls before creating custom motion infrastructure.

## GPUI contract

- Start direct manipulation 1:1 with input and preserve the grab offset.
- Retarget from the current presentation value and velocity.
- Request frames only while values change. Cap long frame steps after stalls.
- Keep input, focus, keyboard alternatives, and hit testing active while motion runs.
- Call `cx.notify()` when animation state changes rendered output.
- Keep `Task`, `Subscription`, and entity ownership explicit. Do not detach animation work without app-lifetime intent.
- Under reduced motion, snap spatial movement or use a short non-spatial state change. Remove bounce, parallax, and elastic overshoot.

Use small travel and restrained bounce for desktop product UI. Starting values are hypotheses: about 100-180 ms for tiny feedback, 180-280 ms for small anchored overlays, and 240-420 ms for larger navigation.

Before coding custom motion, read [the GPUI motion reference](../build-gpui-apps/references/motion-input.md). For reusable spring math, adapt [spring.rs](../build-gpui-apps/assets/spring.rs) to the target revision.

## Validation

Run repository checks, launch the real app, and test re-triggering, cancellation, focus, resize, scale factor, inactive/reactivated windows, reduced motion, and production-like content. Do not call motion complete from `cargo check` alone.
