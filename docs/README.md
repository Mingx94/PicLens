# PicLens documentation

PicLens 是 Windows / 主流 Linux 的 Qt 6、C++20、Qt Quick 圖片整理與檢視應用程式。

## 接手與日常開發

1. [Agent handoff](../HANDOFF.md)：最近驗證快照、修改入口、安全界線與最小交付檢查。
2. [Product specification](product-spec.md)：使用者功能、產品範圍與驗收準則。
3. [Runtime contract](runtime-contract.md)：已承諾且不可無意退化的 runtime 行為。
4. [Architecture](architecture.md)：C++／QML layers、ownership 與依賴方向。
5. [Testing](testing.md)：本機測試、隔離規則與 CI/release gates。
6. [Design system](../DESIGN.md)：Qt Quick 視覺 token、元件與互動規則。

## Release 與運維參考

- [Performance](performance.md)：大型 library 的量測方法、門檻與證據。
- [Portable release](portable-release.md)：Windows／Linux portable bundle。
- [Installer release](installer-release.md)：Windows MSI、Debian DEB、Fedora RPM 與 GitHub Release。
- [Data continuity](data-migration.md)：既有 profile schema、settings、log、cache 與 package lifecycle preservation。
- [Qt licensing](qt-licensing.md)：Qt 與第三方授權交付 inventory。

## 歷史紀錄

- [Qt migration record](qt-migration.md)：Qt cutover 的範圍、決策與完成時證據。
- [Qt parity audit](qt-parity-audit.md)：cutover 完成時的 runtime owner 與 gate 結果。
- [介面概念稿](design/piclens-ui-concept.png) 與 [生成提示](design/piclens-ui-concept.prompt.md)：設計過程參考，不是 runtime contract。

歷史紀錄不是 backlog，也不取代現行規格、測試或 release 指南。

## 文件權責

| 主題 | 權威文件 | 更新規則 |
|---|---|---|
| 使用者需求、產品範圍與待確認問題 | [Product specification](product-spec.md) | 產品意圖改變時更新；不放 framework、build 或部署細節 |
| 已承諾的工程行為與資料契約 | [Runtime contract](runtime-contract.md) 與對應測試 | 行為改變時在同一個 change 更新 contract 與測試 |
| Layer ownership 與依賴方向 | [Architecture](architecture.md) | production composition 或依賴方向改變時更新 |
| 視覺與互動規則 | [Design system](../DESIGN.md) 與 `qml/PicLens/Theme.qml` | token 或元件規則改變時同步更新 |
| Build、test 與 CI 觸發條件 | [Testing](testing.md)、CMake presets 與 workflow | 以可執行設定為準，變更 automation 時同步更新 |
| Packaging、lifecycle 與 signing | [Portable release](portable-release.md)、[Installer release](installer-release.md)、[Qt licensing](qt-licensing.md) | 以 scripts、CMake install graph 與 workflow 為準 |
| 效能門檻與量測證據 | [Performance](performance.md) | 記錄 build type、dataset、環境與日期，避免用 smoke 取代代表性證據 |

程式碼、測試、scripts 與 workflow 是可執行事實；若敘述性文件不一致，先確認預期行為，再在同一個 change 修正負責該主題的文件。其他文件只保留必要摘要並連回權威文件，避免複製整段規則。

Repo root 是可直接由 Qt Creator 開啟的 CMake project；production code 位於 `src/` 與 `qml/`，共用資產位於 `assets/`。舊 Avalonia/.NET runtime、tests、PoC 與 rollback packaging paths 已在取得明確授權後移除。
