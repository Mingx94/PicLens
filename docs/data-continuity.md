# Data continuity

Without `PICLENS_DATA_ROOT`, PicLens uses the platform local application data root under `PicLens`:

- Windows: `%LOCALAPPDATA%\PicLens`
- Linux: `$XDG_DATA_HOME/PicLens`, or `~/.local/share/PicLens`

`PICLENS_DATA_ROOT` is the supported test and diagnostic override. Set it to an isolated directory for tests, smoke runs, performance work, and package lifecycle checks.

## Settings

The production store is `piclens-settings.json`. It reads the historical camel-case fields and numeric sort enums. Unknown legacy fields are ignored. Missing and invalid bounded values are normalized. Corrupt JSON is renamed with a `.corrupt.<suffix>` name before defaults are used. Writes use a temporary file followed by rename.

The active schema stores the selected folder, sort, recursive mode, thumbnail size, sidebar state, and window size. The last folder selected through the folder picker remains the startup restore authority.

## Cache and logs

Thumbnail cache and logs retain their established locations under `PicLens/Thumbnails` and `PicLens/Logs/PicLens.log`. The cache stores generated PNG files and prunes entries beyond its bound. Cache data is disposable; pruning does not modify source images. The log is append-only and currently has no rotation policy.

## Verification

Package publication does not prove data continuity. For a release check, copy a profile, point `PICLENS_DATA_ROOT` to the copy, launch the packaged app, and verify the restored folder, sort, recursive mode, thumbnail size, sidebar state, and window size. Access to a real profile requires explicit user authorization.
