---
name: motion-brief
description: Produce a decision-complete motion brief for one GPUI desktop interaction before implementation. Use when the user asks for a motion brief, wants to decide how a GPUI interaction should move, or repeated implementations still feel wrong. Produces a brief, not code.
metadata:
  short-description: Specify one GPUI motion interaction
---

# GPUI Motion Brief

Inspect the code first. Find the component, two semantic states, trigger, pinned GPUI API, current tokens or conventions, focus owner, and reduced-motion support. Ask only for product decisions that cannot be discovered.

Frequency and purpose can end the brief with a `cut` verdict. High-frequency keyboard actions normally stay instant.

The final brief must contain:

```markdown
## Motion brief - <component>

**Verdict:** animate | cut
**Trigger:** <pointer, keyboard, data, window, or gesture event>
**Frequency:** <expected use>
**Purpose:** <feedback, continuity, state, or none>
**Semantic states:** <A> -> <B>
**Presentation:** <properties and geometry>
**Anchor:** <trigger, center, edge, or pointer grab offset>
**Mechanism:** <element state | finite animation | spring state>
**Timing:** <duration/curve or spring response/damping hypothesis>
**Interrupt:** <repeat, reverse, cancel, re-grab behavior>
**Input and focus:** <behavior while moving>
**Reduced motion:** <snap or non-spatial replacement>
**Ownership:** <entity, task, subscription, completion state>
**Verification:** <test and real-app checks>
**Open risk:** <uncertainty and how to measure it>
```

Do not implement until the user confirms the brief when this skill was explicitly requested as a planning step.

Use [the GPUI motion reference](../build-gpui-apps/references/motion-input.md) for mechanism and starting values.
