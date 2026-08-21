# Testing

## Static and test gates

```powershell
cargo fmt --check
cargo check --workspace --all-targets --locked
cargo test --workspace --locked
cargo clippy --workspace --all-targets --locked -- -D warnings
git diff --check
```

Use a crate-scoped command while you iterate. Run the workspace gates before delivery when a change can affect more than one crate. Pure product rules belong in `piclens-domain`; filesystem and persistence checks belong in `piclens-infra`; GPUI state helpers belong beside the owning UI module.

## Isolation

Set `PICLENS_DATA_ROOT` to a disposable directory for app smoke, performance work, and tests that can create settings, logs, thumbnails, or mutate files. Do not use a real user profile unless the user explicitly authorizes a copied-profile check.

## Runtime smoke

```powershell
$env:PICLENS_DATA_ROOT = "F:\PicLens\artifacts\gpui-smoke"
cargo run -p piclens-gpui -- --folder <representative-folder>
```

For an automated launch-only check, add `--smoke-ms 4000`. This proves that the process opened and stayed alive until the timer elapsed. It does not prove that the library finished loading or that interaction works.

For runtime changes, also check the affected mouse, keyboard, focus, resize, scrolling, error, and cancellation paths in the real app. Inspect `PicLens/Logs/PicLens.log` under the isolated data root.

## Current gaps

- There are no package lifecycle tests for the GPUI binary.
- There is no active GPUI CI workflow.
- The legacy Qt release workflow references removed scripts and is not a valid test gate.
- There are no GPUI window or input integration tests yet. Cargo unit tests do not replace a real launch.
