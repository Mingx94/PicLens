# PicLens

PicLens is a desktop image library and viewer for Windows and mainstream Linux.

**This branch (`experiment/gpui`) is the GPUI migration.** The runtime is Rust + [GPUI](https://github.com/zed-industries/zed) + [gpui-component](https://github.com/longbridge/gpui-component). Product behavior stays under [docs/product-spec.md](docs/product-spec.md).

## Layout

```text
crates/piclens-domain/   product rules (no I/O UI)
crates/piclens-infra/    filesystem, settings, thumbs, OS adapters
crates/piclens-gpui/     application UI
.agents/skills/          GPUI agent skills
docs/                    product and engineering docs
assets/                  icons and fonts
```

## Requirements

- Rust **nightly** (`rust-toolchain.toml` pins the channel; current Zed GPUI needs unstable APIs)
- Windows or Linux desktop stack suitable for GPUI

## Build and run

From the repo root:

```powershell
cargo run -p piclens-gpui --release
cargo run -p piclens-gpui -- --folder D:\Photos
# Optional smoke: open folder then quit (CI)
cargo run -p piclens-gpui -- --folder D:\Photos --smoke-ms 4000
```

Isolate profile data:

```powershell
$env:PICLENS_DATA_ROOT = "F:\PicLens\artifacts\gpui-profile"
cargo run -p piclens-gpui
```

Validation:

```powershell
cargo fmt --check
cargo check --workspace --all-targets --locked
cargo test --workspace --locked
cargo clippy --workspace --all-targets --locked -- -D warnings
```

See [docs/testing.md](docs/testing.md) for runtime smoke and profile isolation.

## Data

Without `PICLENS_DATA_ROOT`, settings and logs use local app data under `PicLens` (`piclens-settings.json`, `Logs/PicLens.log`, `Thumbnails/`).

## Docs

- [docs/README.md](docs/README.md)
- [docs/archive/gpui-experiment.md](docs/archive/gpui-experiment.md)
