# PicLens TODO

Tasks derived from codebase review (`26.0807.01`).

## 1. Dependencies

- [ ] Pin exact Git commit hash for `gpui` in `crates/piclens-gpui/Cargo.toml`.
- [ ] Pin exact Git commit hash for `gpui-component` in `crates/piclens-gpui/Cargo.toml`.

## 2. Code Quality

- [ ] Replace manual `Default` implementations in `crates/piclens-domain/src/models.rs` with `#[derive(Default)]`.
- [ ] Fix `field_reassign_with_default` warning in `crates/piclens-domain/src/settings.rs`.
- [ ] Fix `single_match` warning in `crates/piclens-domain/src/settings.rs`.
- [ ] Ensure `cargo clippy --workspace -- -D warnings` passes without errors.

## 3. Accessibility

- [ ] Add semantic accessibility roles (`role`) to gallery grid elements in `crates/piclens-gpui/src/app/gallery.rs`.
- [ ] Add semantic accessibility labels (`label`) to overlay dialogs in `crates/piclens-gpui/src/app/overlays.rs`.

## 4. Theme & Visual Fallbacks

- [ ] Add solid background fallbacks for high-contrast mode in `crates/piclens-gpui/src/theme.rs`.
- [ ] Support system reduced-transparency preferences.
