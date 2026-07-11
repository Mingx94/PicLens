# Release and cutover

## Release requirements

Preserve these current product obligations unless explicitly changed:

- Windows and mainstream Linux desktop support.
- A no-install portable output.
- A Windows installer.
- Fedora/RPM coverage plus a Debian/Ubuntu-compatible or cross-distro path.
- App icons, embedded Traditional Chinese font assets, logs, settings, and thumbnail-cache locations.
- Release verification separate from unit tests.

## Qt deployment

- Define install rules in CMake and generate deployment steps from current official Qt CMake deployment APIs.
- Include Qt libraries, platform plugins, image format plugins, QML modules, shader/cache resources, fonts, icons, and required compiler runtimes.
- Do not validate a package only on a development machine whose `PATH` exposes the Qt SDK.
- Run the packaged executable with a sanitized environment on each target platform.
- Use `windeployqt` only through a reproducible build/deploy task or generated deployment script, not as an undocumented manual release step.
- On Linux, inspect plugin and shared-library resolution and test on supported distribution families.

Verify exact deployment APIs and arguments against the installed Qt version; they are version-sensitive.

## Packaging direction

- Keep portable output as the source payload for installers when practical.
- Windows: preserve installation intent, shortcuts, icons, uninstall behavior, and upgrade identity when replacing WiX inputs.
- Linux: provide the project-decided RPM and Debian/Ubuntu coverage; do not treat a single RPM as complete Linux support.
- Keep version authority in the root `VERSION` file unless an explicit release design changes it.
- Report file count, total size, executable identity, and SHA-256 for produced artifacts.

## Licensing gate

Before distributing Qt builds:

- Record the Qt edition and license used by the project.
- Inventory every Qt module and third-party image or codec dependency included in the package.
- Preserve required license texts and notices.
- Confirm dynamic/static linking and redistribution obligations with the chosen license. Do not present implementation guidance as legal advice.

## Cutover gate

Do not switch the primary app or delete .NET projects until all are true:

1. Every committed runtime-contract area has an owner in the Qt tree.
2. Applicable legacy characterization tests have Qt replacements or a documented reason they are no longer applicable.
3. Windows and Linux portable builds pass clean-machine smoke tests.
4. Required installers build, install, launch, upgrade/uninstall as applicable, and preserve user data.
5. Logging, settings, and thumbnail-cache migration behavior is defined.
6. Performance and memory evidence is acceptable for representative large libraries.
7. Documentation entry points, build/test commands, and architecture describe Qt as primary.
8. The user explicitly approves the destructive legacy-removal phase.

After approval, remove legacy code in a focused change. Do not combine broad deletion with unrelated feature work. Preserve history and product documentation; remove only stale framework-specific instructions and artifacts.
