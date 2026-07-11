# Portable release

## Windows

```powershell
pwsh -NoProfile -File scripts/build-portable.ps1
```

Output: `artifacts/qt-portable/PicLens-win-x64/`. The script builds and tests Release, runs `windeployqt`, copies licenses/assets, rejects build-machine path leaks and launches an isolated offscreen smoke.

## Linux

```bash
bash scripts/build-linux-portable.sh
```

Output: `artifacts/qt-portable/PicLens-linux-x64/`. The script builds/tests, deploys required libraries/plugins and performs a sanitized smoke run.

Both outputs are self-contained folders, not single-file executables. Distribute the complete directory. Package creation does not replace CTest or lifecycle verification.
