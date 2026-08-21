---
name: review-animations
description: Review GPUI animation and gesture code for correctness, desktop feel, lifecycle, accessibility, and frame performance. Use for a diff or scoped review. Report findings only; do not implement fixes unless the user asks.
metadata:
  short-description: Review GPUI motion
---

# Review GPUI Animations

Review motion, not unrelated code. Treat the pinned checkout as the API authority.

## Finding bar

Report a finding when evidence shows:

- motion has no clear feedback, continuity, or state purpose;
- keyboard or high-frequency input waits for animation;
- a new target restarts from an old endpoint or clears velocity;
- direct manipulation does not track input 1:1 or loses the grab offset;
- input, focus, or hit testing is disabled while motion runs;
- frames continue after presentation settles;
- frame callbacks perform blocking I/O, parsing, or unbounded allocation;
- a dropped `Task` or `Subscription` cancels required work, or detached work outlives its owner;
- rendered animation state changes without `cx.notify()`;
- large layout subtrees rerender when a smaller presentation layer can work;
- reduced motion still uses large travel, overshoot, parallax, or endless decorative movement;
- gesture cancellation leaves pressed, dragged, or focus state stale;
- compilation is presented as runtime or visual proof.

Do not turn preferred durations or easing taste into correctness findings without runtime evidence or a documented product contract.

## Output

List findings first, ordered by user impact. Give `file:line`, evidence, consequence, and the smallest correct fix direction. If there are no findings, say so and list remaining runtime or platform checks that were not performed.

Use [the GPUI motion reference](../build-gpui-apps/references/motion-input.md) as the review standard.
