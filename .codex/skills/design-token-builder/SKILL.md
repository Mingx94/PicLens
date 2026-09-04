---
name: design-token-builder
description: Create or rationalize a compact UI token system for typography, spacing, size, color, radius, border, opacity, and elevation. Use when a design relies on arbitrary one-off values or needs a practical foundation.
---

# Design Token Builder

Reduce decision fatigue by defining a small set of values that are visibly distinct and sufficient for current product needs.

## Method

1. Inventory existing values before proposing new ones.
2. Reuse stable clusters and eliminate near-duplicates where doing so preserves appearance and behavior.
3. Use tighter steps for small values and progressively larger steps for large values; a merely linear multiple is not automatically useful.
4. Define practical, hand-tuned typography and spacing scales instead of forcing mathematical purity.
5. Create only the color families and elevation levels that current components need.
6. Name tokens by role when the role is stable; keep raw scales available where multiple roles share a value.

## Output

Return the proposed token tables, mapping from old values to new values, intentional exceptions, migration order, and visual acceptance checks. Do not add a design-system dependency or rewrite components unless requested.
