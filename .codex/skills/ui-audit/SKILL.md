---
name: ui-audit
description: Perform a broad visual UI review; use a focused skill for a single design concern.
---

# UI Audit

Evaluate the interface as a working product, not as an isolated decoration exercise. Preserve the user's product requirements, existing behavior, framework, and authorization boundaries.

## Inputs

Use the strongest evidence available: rendered UI, screenshots, design files, source code, and design tokens. State which forms of evidence were inspected. Do not claim visual correctness from source inspection alone.

## Review

Use [references/review-checklist.md](references/review-checklist.md) as needed: select sections for hierarchy and actions, layout, typography, color, depth, images, or finishing. Read across sections for a broad review; skip unrelated topics for a scoped review. Do not load every focused skill merely because this checklist mentions its topic.

Prioritize findings by user impact:

1. Comprehension, task completion, accessibility, and misleading hierarchy.
2. Layout, responsive behavior, readability, and consistency.
3. Depth, imagery, decoration, and polish.

Do not mechanically apply a rule when density, platform conventions, data volume, localization, or accessibility creates a better reason to keep the current design.

## Output

Lead with the overall assessment. For each actionable finding, provide:

- Location or component.
- Evidence visible in the supplied artifact.
- Why it matters.
- Smallest useful change.
- A concrete acceptance check.

Separate confirmed defects from subjective options and from items that require rendered, responsive, or assistive-technology verification. If the user asked only for an assessment, do not edit files.
