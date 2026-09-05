---
name: design-token-builder
description: Consolidate repeated UI values into a reusable token system.
---

# Design Token Builder

Reduce decision fatigue by defining a small set of values that are visibly distinct and sufficient for current product needs.

## Method

1. Inventory existing values before proposing new ones.
2. Reuse stable clusters and eliminate near-duplicates where doing so preserves appearance and behavior.
3. Preserve a consistent spacing scale that serves current content. Consider tighter steps for small values and larger steps for large values only when the existing scale causes a concrete problem.
4. Define practical, hand-tuned typography and spacing scales instead of forcing mathematical purity.
5. Create only the color families and elevation levels that current components need.
6. Name tokens by role when the role is stable; keep raw scales available where multiple roles share a value.

## Output

Return affected token definitions and old-to-new mappings. Include exceptions, migration order, and visual checks where needed. Make component edits necessary to apply the requested tokens; avoid unrelated rewrites or new design-system dependencies unless authorized.
