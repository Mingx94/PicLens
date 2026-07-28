# PicLens Agent Handoff

這份文件給下一位維護 PicLens 的 AGENT。先相信程式碼、測試與 workflow 的實際行為，再相信敘述性文件；如果兩者不一致，修正文件並留下測試。

## 接手基準線

- 盤點日期：2026-07-28
- 分支／版本：`main`、`VERSION=2.1.9`
- HEAD／tag：`3621565`，同時是 `v2.1.9`
- 工作樹：盤點前乾淨
- 本機驗證：Debug configure/build 成功，CTest 15/15 通過
- 程式碼內沒有 `TODO`、`FIXME`、`HACK` 或 `XXX` 標記
- [Runtime contract](docs/runtime-contract.md) 目前沒有已知但尚未實作的 committed behavior

重跑基準線：

```powershell
cmake --preset debug
cmake --build --preset debug
ctest --preset debug --output-on-failure
```

## 先讀這些

1. [Runtime contract](docs/runtime-contract.md)：不可無意改掉的產品行為。
2. [Architecture](docs/architecture.md)：layer ownership 與依賴方向。
3. [Testing](docs/testing.md)：本機與 release gates。
4. [Product specification](docs/product-spec.md)：使用者功能與驗收準則。
5. [Design system](DESIGN.md)：動 QML 前先看 token、元件與互動規則。

`docs/qt-migration.md` 與 `docs/qt-parity-audit.md` 是 cutover 紀錄，不是目前工作的 backlog。

## 實際架構與修改入口

依賴方向是 `app -> presentation -> core` 與 `app -> infrastructure -> core`，不要讓 Core 知道 QML、平台命令或檔案系統實作。

| 要改的行為 | 先找哪裡 | 對應測試 |
| --- | --- | --- |
| 格式、排序、路徑、設定合併、縮放、rename 規劃 | `src/core/` | `tests/core/` |
| 掃描、設定、log、縮圖、檔案操作、OS adapter | `src/infrastructure/` | `tests/infrastructure/` |
| library、selection、folder tree、viewer、縮圖協調 | `src/presentation/` | `tests/presentation/` |
| composition、啟動參數、診斷、QML registration | `src/app/` | `tests/app/` |
| 畫面、pointer／keyboard glue、元件與 theme | `qml/PicLens/` | `tests/qml/` |
| portable、installer、lifecycle、效能 gate | `scripts/`、`installer/`、`packaging/` | release workflow／lifecycle scripts |

新增 `.cpp`、`.h` 或 QML 檔時要同步檢查各層 `CMakeLists.txt`；這個專案沒有自動 glob source。

`src/app/src/main.cpp` 不只啟動程式，也承擔 CLI smoke、screenshot、viewer 啟動與效能 metrics。改啟動流程時要檢查這些診斷路徑，不能只手動開 GUI。

## 不能踩的資料與互動界線

- 測試、smoke 與診斷一律用 `PICLENS_DATA_ROOT` 或 `--data-root` 隔離；不要碰使用者真正的 `%LOCALAPPDATA%\PicLens`。
- Trash 必須走 OS recycle bin／trash；Linux `gio trash` 失敗時不可退回永久刪除。
- 轉檔保留原檔、碰到 target collision 就略過，不覆寫。
- Settings 更新要維持 normalization、atomic write 與舊 schema continuity。
- 搜尋、排序、重新掃描、recursive mode 與檔案操作後，不得殘留 stale selection。
- Folder tree 不應因 library 搜尋或目前資料夾切換而 reset／收合。`v2.1.9` 就是修這個 regression。
- QML 的 pointer capture、selection、drag/drop 與 viewer overlay 有刻意的事件邊界；改動時至少跑 QML 與 presentation tests。
- Thumbnail pipeline 是 bounded async work，包含取消、timeout、cache bound 與 stale generation discard；不要在 UI thread 同步 decode。

完整細節以 [Runtime contract](docs/runtime-contract.md) 為準；行為若刻意改變，要同一個 change 更新 contract 與測試。

## 驗證層級

日常修改至少跑最接近的 test target，交付前跑完整 Debug：

```powershell
ctest --preset debug --output-on-failure
```

碰 optimizer、部署、資源或 packaging 時再跑 Release：

```powershell
cmake --preset release
cmake --build --preset release
ctest --preset release --output-on-failure
```

Windows 完整 cutover gate：

```powershell
pwsh -NoProfile -File scripts/run-windows-cutover-gate.ps1
```

這會包含 portable smoke、10,000-image 效能與 data continuity，成本明顯高於日常 CTest。MSI lifecycle 會安裝／解除安裝並變更系統，只有明確授權時才加 `-ConfirmSystemChanges`。

程式能 compile 不等於 runtime 正常。Crash、thumbnail、folder tree 或 viewer 問題要用隔離 profile 做短時間 launch，並檢查 `<data-root>/Logs/PicLens.log`。

## Release 手順

版本唯一權威是根目錄 `VERSION`。CI 使用 Qt 6.8.3；CMake 宣告最低 Qt 6.5。

1. 更新程式、測試與受影響文件。
2. 跑 Debug 與 Release CTest；依變更範圍跑 portable／installer／lifecycle／performance gate。
3. 將 `VERSION` 改成新的 semver 並提交。
4. 建立完全相符的 tag，例如 `VERSION=2.2.0` 就只能用 `v2.2.0`。
5. push tag；`.github/workflows/release.yml` 通過 Windows、Ubuntu 與 Fedora gates 後才會發布 GitHub Release assets。

Windows portable 是 MSI 的 audited payload；不要分別拼兩份內容。Windows 簽章需要 repository 外的憑證，Linux package signing 也是 release operations 責任。

## 已確認的文件／流程落差

這些不是推測：

- `docs/testing.md` 說 feature branch 會由 pull-request event 驗證，但目前唯一 workflow 沒有 `pull_request` trigger。
- 同一份文件說 direct push validation 限於 `main`，目前 workflow 也沒有 branch push trigger；只有 `v*` tag push 或手動 dispatch。
- `docs/runtime-contract.md` 指向 `docs/lore/`，但該目錄不存在。

在補 CI 或 lore 之前，不要假設它們存在。最優先值得處理的是讓 PR 至少跑 configure/build/CTest，或修正 `docs/testing.md` 使它誠實描述只在 release tag 執行的現況。

## 最小交付檢查

- 修改放在正確 layer，先重用既有規則與 controller。
- 新行為有最小可失敗的測試；契約變更同步更新文件。
- `ctest --preset debug --output-on-failure` 通過。
- 檔案操作沒有永久刪除、覆寫或碰真實 profile。
- packaging 變更沒有讓 portable、MSI、DEB、RPM 產生不同產品內容。
- `git diff` 沒有無關格式化、產物或使用者既有變更。
