---
name: feature-first-planner
description: Plan the flow and states of a new UI feature before implementation.
---

# Feature-First Planner

Start with one real user capability, not global navigation or an imagined complete application.

## Method

1. Name the user, trigger, desired outcome, required information, and primary action.
2. Cover the user's requested scope. Divide implementation into useful increments when helpful; an increment does not replace the complete requested outcome.
3. Sketch content and controls when layout or flow is unclear. Use grayscale when it helps resolve hierarchy; use the established visual system for routine choices.
4. Identify the states needed for the requested outcome, such as initial, working, success, empty, and likely failure states. Assign them to increments without omitting required behavior.
5. Flag speculative functionality as later work instead of implying it in the current UI.
6. Prefer a short design-build-use cycle over designing every feature and edge case in advance.

## Output

Describe the requested feature, required flow and states, and acceptance criteria. Include implementation increments or sketches where useful. Defer only ideas outside the requested scope unless the user agrees to reduce it. Do not modify code when the user requested planning only.
