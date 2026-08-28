# PicLens Agent Guide

PicLens is a desktop image viewer and organizer built with Rust, GPUI, and gpui-component.

## Communication Standard

- Use ASD-STE100 Simplified Technical English.
- Apply Zinsser's four principles: Simplicity, Brevity, Clarity, Humanity.

## Build and Test

- Format: `cargo fmt --check`
- Build: `cargo build --workspace --all-targets --locked`
- Check: `cargo check --workspace --all-targets --locked`
- Test: `cargo test --workspace --locked`
- Lint: `cargo clippy --workspace --all-targets --locked -- -D warnings`

## Core Workflow

1. Read the product contract and runtime invariants for changed behavior.
2. Run fitting Cargo checks and a real app smoke when runtime behavior changes.

## Tool Use

- Do not use Computer Use unless the task requires direct interaction with a Windows app.

## Detailed Guidelines

- [Versioning and Release Protocol](docs/agent/versioning-and-release.md)
- [Architecture Principles](docs/agent/architecture-principles.md)
- [Project Documentation](docs/README.md)
