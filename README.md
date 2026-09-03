# PicLens

PicLens is a desktop image library and viewer for Windows and Linux. It uses Rust, [egui](https://github.com/emilk/egui), eframe, and wgpu. [The product specification](docs/product-spec.md) defines its behavior.

## Layout

```text
crates/piclens-domain/   product rules (no I/O UI)
crates/piclens-infra/    filesystem, settings, thumbs, OS adapters
crates/piclens-desktop/  egui/eframe application UI
.agents/skills/          repository agent skills
docs/                    product and engineering docs
assets/                  icons and fonts
```

## Requirements

- Rust nightly `2026-08-11`, selected by `rust-toolchain.toml`
- Windows x86_64 with a Vulkan-capable graphics driver
- Linux x86_64 with an Ubuntu 24.04-compatible runtime, Vulkan 1.3, X11 or Wayland, and the required desktop portals

## Build and run

From the repo root:

```powershell
cargo run --release
cargo run -- --folder D:\Photos
# Optional smoke: open folder then quit (CI)
cargo run -- --folder D:\Photos --smoke-ms 4000
```

Isolate profile data:

```powershell
$env:PICLENS_DATA_ROOT = "F:\PicLens\artifacts\desktop-profile"
cargo run
```

See [Testing](docs/testing.md) for validation commands, runtime smoke, and profile isolation.

## Data

See [Data continuity](docs/data-continuity.md) for profile paths, settings, logs, and thumbnails.

## Docs

- [docs/README.md](docs/README.md)
- [docs/release.md](docs/release.md)

## Automation

GitHub Actions only builds and publishes the Windows MSI, portable ZIP, and checksums. See [Release and packaging](docs/release.md) for tag and manual triggers, and [Testing](docs/testing.md) for local checks.
