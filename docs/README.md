# PicLens documentation

PicLens is a desktop image library and viewer. This branch builds with **Rust + GPUI**. Return to the [repository README](../README.md).

## Daily development

1. [Product specification](product-spec.md): user features, product scope, acceptance intent.
2. [Runtime invariants](runtime-invariants.md): data, async, file ops, and interaction bounds.
3. [Architecture](architecture.md): crate layers and dependency direction.
4. [Development guide](development.md): change entry points and delivery checks.
5. [Data continuity](data-continuity.md): settings, log, cache, profile isolation.

## History

- [GPUI migration notes](archive/gpui-experiment.md)
- [2026-07 Qt cutover archive](archive/2026-07-qt-cutover.md): historical only

Product-spec and runtime-invariants remain the behavior authority. Release packaging for the GPUI binary is not complete on this branch.

## Document ownership

| Topic | Narrative owner | Executable authority |
|---|---|---|
| User needs and product scope | [Product specification](product-spec.md) | Current GPUI runtime |
| Engineering invariants | [Runtime invariants](runtime-invariants.md) | Domain + infra + UI |
| Layer ownership | [Architecture](architecture.md) | Cargo crate graph |
| Version | Rules only | Root `VERSION` |
