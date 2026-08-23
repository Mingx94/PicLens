# Testing

## Static and test gates

```powershell
cargo fmt --check
cargo build --workspace --all-targets --locked
cargo check --workspace --all-targets --locked
cargo test --workspace --locked
cargo clippy --workspace --all-targets --locked -- -D warnings
git diff --check
```

Use a crate-scoped command while you iterate. Run the workspace gates before delivery when a change can affect more than one crate. Pure product rules belong in `piclens-domain`; filesystem and persistence checks belong in `piclens-infra`; GPUI state helpers belong beside the owning UI module.

## Isolation

Set `PICLENS_DATA_ROOT` to a disposable directory to isolate settings, logs, and thumbnails during app smoke, performance work, and tests. This override does not isolate files under `--folder`. Use a disposable copied fixture folder for tests that can rename, trash, or convert source files. Do not use a real user profile unless the user explicitly authorizes a copied-profile check.

## Validation layers

- Use ordinary Rust tests for product rules, data transformations, and GPUI-independent state helpers.
- Use `#[gpui::test]` with `TestAppContext` and, when needed, `VisualTestContext` for GPUI entities, actions, mouse and keyboard input, focus, resize, scrolling, overlays, and asynchronous state. These tests use GPUI's simulated platform and do not require `computer-use`.
- Use `computer-use` with the real app only when pixel appearance matters. Check layout, typography, colors, image rendering, high-DPI output, animation quality, and other visual details with an isolated profile and representative images.

Do not use a headless test result as evidence that pixels are correct. Do not use `computer-use` for behavior that a deterministic GPUI or Rust test can assert.

## Runtime smoke

```powershell
$env:PICLENS_DATA_ROOT = "F:\PicLens\artifacts\gpui-smoke"
cargo run -p piclens-gpui -- --folder <representative-folder>
```

For an automated launch-only check, add `--smoke-ms 4000`. This proves that the process opened and stayed alive until the timer elapsed. It does not prove that the library finished loading or that interaction works.

For runtime changes, also check the affected mouse, keyboard, focus, resize, scrolling, error, and cancellation paths in the real app. Inspect `Logs/PicLens.log` under the isolated data root.

## Current gaps

- Rust CI runs the workspace gates on Windows 2025 and Ubuntu 24.04.
- The release workflow builds portable Windows and Linux archives, but it does not test install, upgrade, or uninstall behavior.
- There are no GPUI window or input integration tests yet. Cargo unit tests do not replace a real launch.
