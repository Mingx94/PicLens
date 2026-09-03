# Development guide

## Before you change code

1. Run `git status --short --branch` and preserve unrelated work.
2. Read [product-spec](product-spec.md) and [runtime-invariants](runtime-invariants.md) for the behavior you touch.
3. Prefer the smallest working change on a green `cargo check`.

## Change entry points

Dependency direction: `piclens-desktop -> piclens-infra -> piclens-domain`.

| Behavior | Crate |
|---|---|
| Formats, sort, path rules, settings merge, zoom, rename plan | `crates/piclens-domain` |
| Scan, settings JSON, log, thumbs, trash/reveal, convert | `crates/piclens-infra` |
| Window, library, sidebar, viewer, selection, commands | `crates/piclens-desktop` |

## Commands

Use the commands and isolated profile workflow in [Testing](testing.md).

## Delivery check

1. Workspace format, build, check, test, and lint gates pass.
2. Manual smoke covers open folder, select, viewer Escape, and one file operation when those paths change.
3. Runtime checks use an isolated profile and the app log is clean for the tested path.
4. The commit uses a short message on the current task branch. Push only when requested.

Run the Cargo gates locally. GitHub Actions only builds and publishes Windows packages through `.github/workflows/release.yml`; see [Release and packaging](release.md).
