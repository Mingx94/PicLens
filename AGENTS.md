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

1. Check workspace: `git status`.
2. Stage and change task files only.
3. Read the product contract and runtime invariants for changed behavior.
4. Run fitting Cargo checks and a real app smoke when runtime behavior changes.
5. Run each Cargo command, `git add`, and `git commit` as a separate shell call. Do not combine them with shell control flow.
6. Commit to the current task branch with a short message. Report the commit hash.
7. Do not push, amend, or rewrite history without explicit user permission.

## Detailed Guidelines

- [Git Workflow](docs/agent/git-workflow.md)
- [Versioning and Release Protocol](docs/agent/versioning-and-release.md)
- [Architecture Principles](docs/agent/architecture-principles.md)
- [Project Documentation](docs/README.md)
