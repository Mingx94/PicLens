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

Use a crate-scoped command while you iterate. Run the workspace gates before delivery when a change can affect more than one crate. Pure product rules belong in `piclens-domain`; filesystem and persistence checks belong in `piclens-infra`; egui state helpers belong beside the owning UI module.

## Isolation

Set `PICLENS_DATA_ROOT` to a disposable directory to isolate settings, logs, and thumbnails during app smoke, performance work, and tests. This override does not isolate files under `--folder`. Use a disposable copied fixture folder for tests that can rename, trash, or convert source files. Do not use a real user profile unless the user explicitly authorizes a copied-profile check.

## Validation layers

- Use ordinary Rust tests for product rules, data transformations, reducers, backend contracts, and framework-independent state helpers.
- Use `egui_kittest` for deterministic layout, pointer and keyboard actions, focus, resize, scrolling, dialogs, and AccessKit semantics. These tests do not prove native window-system or assistive-technology behavior.
- Use an isolated real app for renderer, image quality, high-DPI, drag/drop, native helper, and platform behavior. The built-in `--screenshot` option can capture deterministic evidence. Use Computer Use only when the user explicitly requests it.

Do not use a headless test result as evidence that pixels are correct. Do not use a launch-only smoke as evidence that interaction works.

## Runtime smoke

```powershell
$env:PICLENS_DATA_ROOT = "F:\PicLens\artifacts\desktop-smoke"
cargo run -- --folder <representative-folder>
```

For an automated launch-only check, add `--smoke-ms 4000`. This proves that the process opened and stayed alive until the timer elapsed. It does not prove that the library finished loading or that interaction works.

For runtime changes, also check the affected mouse, keyboard, focus, resize, scrolling, error, and cancellation paths in the real app. Inspect `Logs/PicLens.log` under the isolated data root.

Windows 批次資源量測使用 disposable copied fixture：

```powershell
.\scripts\measure-windows-batch-performance.ps1 -SourcePng <representative-png>
```

此腳本只接受 1 至 49 份副本，要求 fixture 位於隔離 profile 內，並檢查來源 PNG 保留、JPG 數量、批次結果、app log、CPU、GPU 與 peak working set 證據。

## Current gaps

- There is no branch or pull-request CI. Run the workspace gates locally.
- The release workflow builds the Windows MSI, runs its install／launch／replace／uninstall／profile-preservation lifecycle on the clean hosted runner, then publishes the MSI, portable ZIP, and checksums. It does not build Linux packages.
- `egui_kittest` can inspect AccessKit roles, names, states, actions, and focus. It does not replace a native assistive-technology check or a real launch.
