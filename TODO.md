# PicLens TODO

Tasks derived from codebase review (`26.0807.01`).

## 1. Dependencies

- [x] Pin exact Git commit hash for `gpui` in `crates/piclens-gpui/Cargo.toml`.
- [x] Pin exact Git commit hash for `gpui-component` in `crates/piclens-gpui/Cargo.toml`.

## 2. Code Quality

- [x] Replace manual `Default` implementations in `crates/piclens-domain/src/models.rs` with `#[derive(Default)]`.
- [x] Fix `field_reassign_with_default` warning in `crates/piclens-domain/src/settings.rs`.
- [x] Fix `single_match` warning in `crates/piclens-domain/src/settings.rs`.
- [x] Ensure `cargo clippy --workspace -- -D warnings` passes without errors.

## 3. Accessibility

- [x] Add semantic accessibility roles (`role`) to gallery grid elements in `crates/piclens-gpui/src/app/gallery.rs`.
- [x] Add semantic accessibility labels (`label`) to overlay dialogs in `crates/piclens-gpui/src/app/overlays.rs`.

## 4. Theme & Visual Fallbacks

- [x] Add solid background fallbacks for high-contrast mode in `crates/piclens-gpui/src/theme.rs`.
- [x] Support system reduced-transparency preferences.
