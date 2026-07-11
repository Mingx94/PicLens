# Data continuity after Qt cutover

Without `PICLENS_DATA_ROOT`, PicLens uses the platform local application data root under `PicLens`:

- Windows: `%LOCALAPPDATA%\PicLens`
- Linux: `$XDG_DATA_HOME/PicLens`, or `~/.local/share/PicLens`

`PICLENS_DATA_ROOT` is the supported test/diagnostic override. Release smoke and lifecycle scripts always set it to an isolated directory.

## Settings

The Qt store reads the existing `settings.json` field names and numeric sort enums used by pre-cutover versions. Missing/default values are normalized; corrupt JSON is quarantined; updates are atomic. Deprecated `favoriteFolders` and `version` fields are ignored and removed on the next write.

Qt persistence tests include fixtures for the historical schema so installed users do not need a conversion step. This is forward continuity, not a rollback dependency.

## Cache and logs

Thumbnail cache and logs retain their established locations and bounded-pruning behavior. Cache entries are disposable and can be regenerated; source images are never modified by pruning. Error logs avoid recording image contents and should not expose unrelated environment data.

## Verification

`scripts/verify-data-continuity.ps1` copies a profile into `artifacts/data-migration/profile-copy`, launches the packaged Qt app against that copy, verifies restored folder/sort/recursive/size state and confirms the original source profile remains byte-for-byte unchanged. Access to a real profile requires explicit user authorization.

MSI/DEB/RPM lifecycle scripts separately verify that install, upgrade/replacement and uninstall do not delete the isolated profile.
