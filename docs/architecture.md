# Architecture

PicLens is a Rust workspace with a GPUI desktop application.

```text
crates/piclens-domain/     framework-light product rules and value models
crates/piclens-infra/      filesystem, settings, logging, thumbnails, OS adapters
crates/piclens-gpui/       GPUI shell, controllers, and composition root
.agents/skills/            agent skills for GPUI / gpui-component
docs/                      product contracts and engineering notes
assets/                    application icons and fonts
```

## Dependency direction

`piclens-gpui -> piclens-infra -> piclens-domain`. Domain does not depend on GPUI, filesystem codecs, or platform UI.

## Runtime composition

`crates/piclens-gpui/src/main.rs` starts `gpui_platform`, registers bundled fonts, publishes the light `Theme` global, installs native menus, calls `gpui_component::init`, and opens a window wrapped in `Root`. `PicLensApp` owns settings, folder history, library scan results, selection, viewer snapshot, and file-operation commands. Window render is split under `src/app/` (`gallery`, `shell`, `overlays`, `render`). Infrastructure implements scan, settings JSON, trash/reveal, convert, and thumbnail cache helpers.

## Data and diagnostics

Without `PICLENS_DATA_ROOT`, platform local application data under `PicLens` is the authority for settings, cache, and logs. See [data continuity](data-continuity.md).

## History

Migration records and measurements are kept under [the archive](archive/README.md). They do not define current architecture, commands, support, or release outputs.
