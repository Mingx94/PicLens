# PicLens Agent Guide

PicLens is a cross-platform desktop image viewer and organizer built with Qt 6, C++20, and Qt Quick.

## Communication Standard

- Use ASD-STE100 Simplified Technical English.
- Apply Zinsser's four principles: Simplicity, Brevity, Clarity, Humanity.

## Build and Test

- Build (Debug): `cmake --preset debug && cmake --build --preset debug`
- Test (Debug): `ctest --preset debug --output-on-failure`

## Core Workflow

1. Check workspace: `git status`.
2. Stage and change task files only.
3. Verify changes with unit and preset tests.
4. Commit directly on `main` with a short message. Report the commit hash.
5. Do not push, amend, or rewrite history without explicit user permission.

## Detailed Guidelines

- [Git Workflow](docs/agent/git-workflow.md)
- [Versioning and Release Protocol](docs/agent/versioning-and-release.md)
- [Architecture Principles](docs/agent/architecture-principles.md)
- [Project Documentation](docs/README.md)
