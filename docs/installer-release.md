# Installer Release

Windows MSI 由 Qt portable payload 建立。Qt Linux DEB/RPM 由 CMake install tree 建立；`Tasks.cs installer` 的 Linux host branch 暫時仍是 legacy Avalonia fallback，直到遠端 distro lifecycle gate 通過。

This page documents the installer outputs currently implemented by `Tasks.cs`. Product support still targets mainstream Linux desktop distributions, not Fedora/RPM only.

## Build

Run the platform-detecting installer task from repository root:

```shell
dotnet run Tasks.cs installer
```

The wrapper builds the implemented installer for the current host:

- Windows: `artifacts/installer/PicLens-win-x64.msi`
- Fedora Linux: `artifacts/installer/PicLens-1.0.0-fedora-x86_64.rpm`

Package version is read from the repository root `VERSION` file. Update that file for normal releases.

If a required packaging tool is missing, the wrapper prints the install command and exits.

Installer builds do not run tests. Windows 發行前先跑 Qt Debug/Release CTest；coexistence 期間仍需跑適用的 legacy tests。

## Linux coverage

Qt CMake/CPack 現在定義 Debian/Ubuntu `.deb` 與 Fedora `.rpm` candidates。Ubuntu job 使用官方 Qt deployment tree，並會實際 install/launch/remove DEB；Fedora 44 job 使用 distro system Qt、RPM auto dependencies，並實際 install/launch/remove RPM。兩條 workflow 在首次遠端成功前仍不可視為 release-ready。generic Qt `linux-x64` portable 是不經系統安裝的跨 distro candidate。

## Options

```shell
dotnet run Tasks.cs installer --dry-run
dotnet run Tasks.cs installer --no-clean
dotnet run Tasks.cs installer --no-release
```

Use `--version` only for an explicit one-off override:

```shell
dotnet run Tasks.cs installer --version 1.0.1
```

## Tooling

Windows MSI builds restore WiX Toolset through `installer/PicLens.wixproj`.
`Tasks.cs installer` 會先執行 `qt/scripts/build-portable.ps1`，因此 MSI payload 包含 Qt/QML、platform/image plugins、MSYS2 runtime dependency closure、relative `qt.conf`、PicLens MIT 與第三方 license texts，不要求目標機安裝 .NET Runtime。

WiX build 使用 forced non-incremental rebuild，避免沿用舊 payload；package 保留既有 UpgradeCode，定義 WiX `MajorUpgrade`（包含同版 migration replacement），且 per-machine shortcut component 使用 HKLM key path。完成後 `qt/scripts/audit-msi.ps1` 會唯讀檢查 ProductVersion、Upgrade table、HKLM registration、Start Menu shortcut、完整 file count/bytes，以及必要 Qt/runtime/license files。本機 ICE validation 因目前開發環境無法存取 Windows Installer service 而 suppress；clean-machine release gate 必須重新啟用 ICE。

在可丟棄、已提升權限的 Windows runner/VM 執行安裝生命週期：

```powershell
pwsh -NoProfile -File qt/scripts/test-msi-lifecycle.ps1 -ConfirmSystemChanges
```

這會實際 install、從 `%ProgramFiles%` 啟動 Qt offscreen smoke、比對 portable executable
hash、檢查共同 Start Menu shortcut/HKLM registration、uninstall，並確認隔離 user profile
逐檔未被移除。若提供 `-PreviousMsiPath <old.msi>`，還會先安裝舊版再驗證 major upgrade。
此命令會改變系統安裝狀態，本機執行必須取得當次明確授權；GitHub hosted runner 是可丟棄
環境，因此 workflow 會保留第一個 build 作為同版 migration replacement fixture、重建新
ProductCode，再自動執行 install/upgrade/launch/uninstall 路徑。
Verifier 在任何安裝前會檢查既有 PicLens uninstall registration；預設拒絕取代使用者已
安裝版本。只有另行取得明確 replacement 授權後才能加
`-AllowReplacingExistingInstallation`。

2026-07-11 本機提升權限 smoke 已通過 fresh install/launch/uninstall，並以臨時 `1.1.9`
package 驗證升級到 `1.2.0`；upgrade 前後均能啟動 packaged Qt app，解除安裝後 executable、
共同捷徑與 HKLM registration 消失，隔離 profile manifest 完全不變。乾淨 hosted runner 的
首次 workflow evidence 仍待取得。

Windows default output:

```text
artifacts/installer/PicLens-win-x64.msi
```

Legacy Fedora RPM builds require `rpm-build`：

```shell
sudo dnf install rpm-build
```

Legacy Fedora default output:

```text
artifacts/installer/PicLens-1.0.0-fedora-x86_64.rpm
```

## Notes

The MSI installs machine-wide under `%ProgramFiles%\PicLens`, so installation may require Administrator rights.

Windows Qt MSI 是 self-contained runtime bundle，不要求目標機安裝 Qt 或 .NET Runtime。Fedora RPM 目前仍是 legacy framework-dependent package，目標機需安裝 .NET Runtime 10。

Qt Linux package candidates 在 Qt Release build directory 執行：

```bash
cd qt/build/release
cpack -G DEB
cpack -G RPM
```

CPack application prefix 為 `/opt/piclens`；system package mode 將 desktop entry 與 icon 安裝到標準 `/usr/share` 位置。Ubuntu DEB 包含 generated Qt/QML deployment tree 與 Qt license texts；Fedora RPM 設定 `PICLENS_USE_SYSTEM_QT=ON`，不重複散佈 Fedora Qt libraries，而由 RPM dependencies 安裝 distro Qt。兩者都不要求 .NET Runtime；舊 `Tasks.cs installer` Linux branch 只保留到新 packages 通過遠端 lifecycle gates。

Linux lifecycle verifier：

```bash
bash qt/scripts/test-linux-package-lifecycle.sh --deb path/to/piclens.deb \
  --expected-executable artifacts/qt-portable/PicLens-linux-x64/bin/PicLens
bash qt/scripts/test-linux-package-lifecycle.sh --rpm path/to/piclens.rpm
```

Verifier 會檢查 `/opt/piclens/bin/PicLens`、`/usr/share` desktop/icon integration、
offscreen launch、package removal 及隔離 profile preservation。

Installer builds strip debug symbol `.pdb` files from setup staging. Portable release folders keep them.
