# Build and Test

- Format: `cargo fmt --check`
- Build: `cargo build --workspace --all-targets --locked`
- Check: `cargo check --workspace --all-targets --locked`
- Test: `cargo test --workspace --locked`
- Lint: `cargo clippy --workspace --all-targets --locked -- -D warnings`
