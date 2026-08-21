# PicLens documentation

PicLens is a desktop image library and viewer built with Rust, GPUI, and gpui-component. Return to the [repository README](../README.md).

## Daily development

1. [Product specification](product-spec.md): user features, product scope, acceptance intent.
2. [Runtime invariants](runtime-invariants.md): data, async, file ops, and interaction bounds.
3. [Architecture](architecture.md): crate layers and dependency direction.
4. [Development guide](development.md): change entry points and delivery checks.
5. [Data continuity](data-continuity.md): settings, log, cache, profile isolation.
6. [Testing](testing.md): Cargo checks and runtime verification.
7. [Design system](design/system.md): current GPUI layout, palette, and component rules.

## Release and operations

- [Release and packaging](release.md): versioning, portable artifacts, and publication.
- [Performance](performance.md): implemented safeguards and evidence rules.
- [Licensing](licensing.md): source and bundled asset obligations.

## History

- [Archive index](archive/README.md): migration records and old evidence; never current instructions.

Product-spec and runtime-invariants remain the behavior authority. Current build, test, and release commands must use Cargo and the Rust workflows under `.github/workflows/`.

## Document ownership

| Topic | Narrative owner | Executable authority |
|---|---|---|
| User needs and product scope | [Product specification](product-spec.md) | Current GPUI runtime |
| Engineering invariants | [Runtime invariants](runtime-invariants.md) | Domain + infra + UI |
| Layer ownership | [Architecture](architecture.md) | Cargo crate graph |
| Build and test commands | [Testing](testing.md) | Cargo workspace and lockfile |
| Release readiness | [Release and packaging](release.md) | `.github/workflows/release.yml` |
| Package version | [Release and packaging](release.md) | `[workspace.package].version` in root `Cargo.toml` |
