---
name: pick-ui-library
description: Choose the smallest existing GPUI or gpui-component mechanism for a desktop UI motion task. Use when deciding between element styling, finite GPUI animation, entity-owned spring state, existing scroll or overlay behavior, canvas, a custom Element, or a platform bridge.
metadata:
  short-description: Pick the GPUI motion mechanism
---

# Pick a GPUI Motion Mechanism

Inspect `Cargo.toml`, `Cargo.lock`, the pinned GPUI source, and similar local components before recommending anything. Recommend one mechanism.

| Need | Use |
| --- | --- |
| Hover, press, focus, selected feedback | Element state styling, often without a timeline |
| Small finite decorative transition | `AnimationExt::with_animation` when supported locally |
| Drag or direct manipulation | Entity-owned presentation state updated 1:1 |
| Retargetable settling or momentum | Explicit spring state plus animation-frame requests |
| Scrolling or large collections | Existing `list`, `uniform_list`, or project scroll behavior |
| Dialog, popover, menu, tooltip, tabs | Existing gpui-component or project primitive |
| Custom painted motion | `canvas` or custom `Element` only when normal elements cannot express it |
| Native-only behavior | Narrow platform bridge behind availability and fallback boundaries |

Do not add a Web animation package, React runtime, embedded browser, or general animation framework to solve native GPUI motion. Do not create a second component system when gpui-component or the project already owns the control.

Read [the GPUI motion reference](../build-gpui-apps/references/motion-input.md) for the detailed choice.
