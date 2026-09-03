# Architecture

PicLens is a Rust workspace with an egui/eframe desktop application that uses the wgpu renderer.

```text
crates/piclens-domain/     framework-light product rules and value models
crates/piclens-infra/      filesystem, settings, logging, thumbnails, OS adapters
crates/piclens-desktop/    egui/eframe shell and composition root
.agents/skills/            repository agent skills
docs/                      product contracts and engineering notes
assets/                    application icons and fonts
```

## Dependency direction

`piclens-desktop -> piclens-infra -> piclens-domain`. Domain does not depend on egui, filesystem codecs, or platform UI.

## Runtime composition

`crates/piclens-desktop/src/main.rs` parses the command line. `lib.rs` loads compatible settings, configures the eframe window, selects wgpu, and starts `PicLensApp`. The app owns the reducer, request identities, the background backend, image textures, diagnostics, and frame lifecycle. Views in `ui/mod.rs` only read model state and append `Action` values. The reducer converts actions to bounded background `Command` work. Matching `Event` values update the model. Infrastructure implements scan, settings JSON, trash/reveal, conversion, and thumbnail cache helpers.

## Data and diagnostics

Without `PICLENS_DATA_ROOT`, platform local application data under `PicLens` is the authority for settings, cache, and logs. See [data continuity](data-continuity.md).

## History

Migration records and measurements are kept under [the archive](archive/README.md). They do not define current architecture, commands, support, or release outputs.
