# Versioning and Release Protocol

## Version Format

- Use the UTC release date and a two-digit serial number: `YY.MMDD.NN` (for example, `26.0814.01`).
- Source of truth: [VERSION](../../VERSION).

## Release Steps

1. Verify package builds locally across target presets.
2. Push the matching `v<version>` tag to GitHub.
3. Confirm that the release GitHub Action succeeds.
4. For packaging specifications, refer to [docs/release.md](../release.md).
