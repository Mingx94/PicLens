# Versioning and Release Protocol

## Version Authority

- `[workspace.package].version` in the root `Cargo.toml` is the only package version authority.
- All workspace crates inherit this version.
- Release tags use `v<version>`. The release workflow rejects any tag that does not match the Cargo version.

## Release Protocol

1. Update the workspace version and `Cargo.lock` in one release commit.
2. Run every command in [Testing](../testing.md).
3. Build the portable archives from the clean release commit and inspect their contents.
4. Test the archives on clean Windows and Linux systems. Record launch, folder access, profile preservation, and the unverified paths.
5. Create an annotated tag named `v<version>` on the release commit.
6. Push the release commit and tag. The tag starts `.github/workflows/release.yml`.
7. Confirm that both archives, both checksum files, and the GitHub Release are present before reporting completion.

Do not create a release tag when packaging or clean-machine verification has failed. Do not claim installer, signing, or auto-update support; the current outputs are unsigned portable archives.

See [Release and packaging](../release.md) for asset names and workflow details.
