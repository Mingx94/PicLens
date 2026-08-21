---
name: improve-animations
description: Audit existing GPUI motion across a repository and write evidence-based improvement plans. Use for a read-only whole-codebase or scoped audit, not a diff review and not implementation.
metadata:
  short-description: Audit and plan GPUI motion improvements
---

# Improve GPUI Animations

Survey existing motion, select the highest-leverage problems, and write plans only when the user requests plans.

## Boundaries

- Do not edit production code, install dependencies, run formatters, or commit.
- Respect documented product decisions and repository conventions.
- Re-read every reported location before presenting it.
- Prefer a short list of high-confidence findings. A clean audit is valid.

## Audit

Map the pinned GPUI revision, owning entities, frame request sites, finite animations, spring state, gestures, preference handling, focus paths, and existing tests. Search for `with_animation`, `request_animation_frame`, `Instant`, spring state, pointer handlers, `cx.notify()`, `reduce_motion`, tasks, and subscriptions.

Evaluate:

1. Purpose and frequency
2. State ownership and semantic end state
3. Interruption and velocity continuity
4. Input, focus, cancellation, and accessibility
5. Frame lifecycle and per-frame work
6. Reduced-motion behavior
7. Cohesion and spatial continuity
8. Runtime and test evidence

Rank findings by impact divided by effort. For selected plans, include exact paths, current behavior, target behavior, scope boundaries, ordered steps, and verification with real app launch and re-trigger tests.

Read [the GPUI motion reference](../build-gpui-apps/references/motion-input.md) before judging custom motion.
