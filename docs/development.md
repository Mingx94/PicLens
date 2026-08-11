# Development guide

## Before you change code

1. `git status --short` and confirm branch (`experiment/gpui` for this migration).
2. Read [product-spec](product-spec.md) and [runtime-invariants](runtime-invariants.md) for the behavior you touch.
3. Prefer the smallest working change on a green `cargo check`.

## Change entry points

Dependency direction: `piclens-gpui -> piclens-infra -> piclens-domain`.

| Behavior | Crate |
|---|---|
| Formats, sort, path rules, settings merge, zoom, rename plan | `crates/piclens-domain` |
| Scan, settings JSON, log, thumbs, trash/reveal, convert | `crates/piclens-infra` |
| Window, library, sidebar, viewer, selection, commands | `crates/piclens-gpui` |

## Commands

```powershell
cargo test -p piclens-domain
cargo check -p piclens-gpui
cargo run -p piclens-gpui -- --folder <path>
```

Use `PICLENS_DATA_ROOT` for isolated profiles in tests and smoke runs.

## Delivery check

1. Domain tests pass for touched rules.
2. `cargo check -p piclens-gpui` is clean.
3. Manual smoke for open folder, select, viewer Escape, and one file op when those paths change.
4. Short commit message on the migration branch. Push only when requested.
