---
name: prototype
description: Build several distinct GPUI desktop motion variants in an isolated prototype surface so the user can compare them in the real app. Explicit invocation only. Do not change production behavior until the user selects a variant.
metadata:
  short-description: Prototype GPUI motion variants
---

# Prototype GPUI Motion Variants

Explore one interaction per run. Default to three variants with distinct named axes such as instant, finite, and spring-settled. Variants must use project tokens, realistic content, real pointer and keyboard behavior, and reduced-motion fallbacks.

## Isolation

- Keep prototype code behind an isolated view, debug action, example binary, or test-only surface chosen from existing project conventions.
- Do not import prototype code into production paths.
- Do not add a Web server, HTML picker, React, or browser runtime.
- Switching variants must be instant because comparison is high frequency.

## Verification

Launch the actual GPUI prototype. Exercise every variant, re-trigger it mid-flight, use keyboard and pointer input, resize the window, test reduced motion, and inspect logs. Capture matching screenshots or recordings when useful.

Present each variant's axis, when it is the right choice, and its cost. Stop for user selection. After selection, integrate only the winner and remove the prototype surface unless the user asks to keep it.

Read [the GPUI motion reference](../build-gpui-apps/references/motion-input.md) before building variants.
