---
name: gpui-motion-state
description: Build and debug interruptible GPUI motion with entity-owned presentation state, animation-frame requests, springs, drag, momentum, and retargeting. Use when finite GPUI animations cannot preserve input continuity or velocity.
metadata:
  short-description: Build interruptible GPUI motion state
---

# GPUI Motion State

Use explicit motion state for direct manipulation and physical settling.

```rust
struct MotionState {
    spring: Spring1D,
    last_frame: Option<Instant>,
    dragging: bool,
}
```

## Lifecycle

1. On press, capture the pointer identity, presentation value, and grab offset.
2. During drag, update presentation 1:1 and estimate bounded velocity.
3. On release, choose a semantic target and preserve current velocity.
4. On each frame, use monotonic time, cap or subdivide long steps, update state, call `cx.notify()`, and request another frame only if unsettled.
5. On re-grab, continue from the visible presentation without a jump.
6. On reduced motion, snap to the semantic target and clear velocity.

Keep the committed model value separate from overshoot or rubber-band presentation. Clamp projected targets to valid stops. Do not rubber-band destructive or precision controls.

Use `WeakEntity` for long-lived callbacks. Hold tasks and subscriptions when dropping them must cancel work. Do not start a general-purpose animation framework for one component.

Read [the GPUI motion reference](../build-gpui-apps/references/motion-input.md) and adapt [spring.rs](../build-gpui-apps/assets/spring.rs) instead of inventing spring math.
