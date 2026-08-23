# Versioning and Release Protocol

Use [Release and packaging](../release.md) as the authority for versioning, validation, artifact names, and publication.

## Agent completion rules

1. Use `[workspace.package].version` and the matching annotated `v<version>` tag.
2. Do not tag when packaging or clean-machine verification fails.
3. Push the release commit and tag only when authorized.
4. Report completion only after the GitHub workflow succeeds and publishes both archives and checksum files.
