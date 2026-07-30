# PicLens documentation

PicLens 是 Windows／主流 Linux 的 Qt 6、C++20、Qt Quick 圖片整理與檢視應用程式。返回 [repository README](../README.md)。

## 日常開發

1. [Product specification](product-spec.md)：使用者功能、產品範圍與驗收準則。
2. [Runtime invariants](runtime-invariants.md)：不可無意退化的資料、非同步、檔案操作與互動界線。
3. [Architecture](architecture.md)：C++／QML layers、ownership 與依賴方向。
4. [Development guide](development.md)：修改入口、安全界線與最小交付檢查。
5. [Testing](testing.md)：本機測試、隔離規則與 CI/release gates。
6. [Design system](design/system.md)：Qt Quick 視覺 token、元件與互動規則。

## Release 與運維

- [Performance](performance.md)：量測方法、門檻與 evidence 規則。
- [Release and packaging](release.md)：Windows／Linux portable、MSI、DEB、RPM、lifecycle、signing 與 GitHub Release。
- [Data continuity](data-continuity.md)：profile schema、settings、log、cache 與 package lifecycle preservation。
- [Licensing and redistribution](licensing.md)：Qt 與第三方授權交付 policy。

## 歷史紀錄

- [2026-07 Qt cutover](archive/2026-07-qt-cutover.md)：framework migration、destructive-removal decision 與完成時 evidence。
- [2026-07 performance evidence](archive/performance/2026-07.md)：歷史 local/hosted metrics。
- [介面概念稿](archive/design/piclens-ui-concept.png) 與 [生成提示](archive/design/piclens-ui-concept.prompt.md)：設計過程參考，不是 runtime contract。

歷史紀錄不是目前狀態、backlog 或 release 證據。Current checkout 必須重新執行相稱的 gates。

## 文件權責

| 主題 | Narrative owner | Executable authority |
|---|---|---|
| 使用者需求與產品範圍 | [Product specification](product-spec.md) | Current runtime and acceptance tests |
| 工程不變條件與資料契約 | [Runtime invariants](runtime-invariants.md) | C++/QML implementation and tests |
| Layer ownership | [Architecture](architecture.md) | CMake target dependencies |
| 視覺語意與互動規則 | [Design system](design/system.md) | `qml/PicLens/Theme.qml` and QML components |
| Build、test 與 CI | [Testing](testing.md) | CMake presets、CTest registration and workflow |
| Packaging、lifecycle、signing | [Release](release.md) | Scripts、CMake install graph、WiX and workflow |
| 效能門檻 | [Performance](performance.md) | Performance script and CI gate |
| Version | 文件只描述規則 | Root `VERSION` |
| Licensing policy | [Licensing](licensing.md) | Final artifact manifest、notices and package audit |

敘述性文件不應複製可執行設定的完整清單。若文件與程式碼不一致，先確認預期行為，再在同一個 change 修正對應 narrative owner、實作與測試。
