# PicLens documentation

PicLens 是 Windows / 主流 Linux 的 Qt 6、C++20、Qt Quick 圖片整理與檢視應用程式。

- [Product specification](product-spec.md)：使用者功能與驗收準則。
- [Architecture](architecture.md)：C++/QML layers、ownership 與依賴方向。
- [Runtime contract](runtime-contract.md)：產品行為與資料契約。
- [Testing](testing.md)：CMake/CTest、QML 與 release gates。
- [Performance](performance.md)：大型 library 的量測方法與門檻。
- [Portable release](portable-release.md)：Windows/Linux portable bundle。
- [Installer release](installer-release.md)：Windows MSI、Debian DEB、Fedora RPM。
- [Data continuity](data-migration.md)：舊 profile schema、settings/log/cache 與 package lifecycle preservation。
- [Qt licensing](qt-licensing.md)：Qt 與第三方授權交付。
- [Qt migration record](qt-migration.md)：cutover scope、證據與後續 release work。
- [Parity audit](qt-parity-audit.md)：runtime contract owner 與 gate 結果。

Repo root 是可直接由 Qt Creator 開啟的 CMake project；production code 位於 `src/` 與 `qml/`，共用圖示與字型位於 `assets/`。舊 Avalonia/.NET runtime、tests、PoC 與 rollback packaging paths 已在取得明確授權後移除。
