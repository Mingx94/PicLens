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
cargo fmt --check
cargo check --workspace --all-targets --locked
cargo test --workspace --locked
cargo clippy --workspace --all-targets --locked -- -D warnings
cargo run -p piclens-gpui -- --folder <path>
```

Use `PICLENS_DATA_ROOT` for isolated profiles in tests and smoke runs.

## Delivery check

1. Workspace format, check, test, and lint gates pass.
2. Manual smoke covers open folder, select, viewer Escape, and one file operation when those paths change.
3. Runtime checks use an isolated profile and the app log is clean for the tested path.
4. The commit uses a short message on the current task branch. Push only when requested.
