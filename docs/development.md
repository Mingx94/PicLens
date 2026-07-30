# Development guide

這份文件提供穩定的日常維護入口，不記錄特定日期、commit、tag 或最近一次測試結果。歷史 cutover 證據位於 [archive](archive/2026-07-qt-cutover.md)；目前 checkout 的狀態必須由 Git、程式碼、測試與 workflow 重新確認。

## 開始工作

先閱讀 [documentation index](README.md)，再依變更範圍閱讀：

1. [Product specification](product-spec.md)：使用者可見行為與產品範圍。
2. [Runtime invariants](runtime-invariants.md)：不可無意退化的工程界線。
3. [Architecture](architecture.md)：layer ownership 與依賴方向。
4. [Testing](testing.md)：本機驗證、隔離與 release gates。
5. [Design system](design/system.md)：視覺 token、元件與互動規則。

開始修改前先檢查工作樹，避免覆蓋使用者或其他工作中的變更：

```powershell
git status --short
git branch --show-current
git rev-parse --short HEAD
```

## 修改入口

依賴方向是 `app -> presentation -> core` 與 `app -> infrastructure -> core`。Core 不依賴 QML、平台命令、檔案系統實作或 image codecs。

| 要改的行為 | 先找哪裡 | 對應測試 |
|---|---|---|
| 格式、排序、路徑、設定合併、縮放、rename 規劃 | `src/core/` | `tests/core/` |
| 掃描、設定、log、縮圖、檔案操作、OS adapter | `src/infrastructure/` | `tests/infrastructure/` |
| library、selection、folder tree、viewer、縮圖協調 | `src/presentation/` | `tests/presentation/` |
| composition、啟動參數、診斷、QML registration | `src/app/` | `tests/app/` |
| 畫面、pointer／keyboard glue、元件與 theme | `qml/PicLens/` | `tests/qml/` |
| portable、installer、lifecycle、效能 gate | `scripts/`、`installer/`、`packaging/` | release workflow／lifecycle scripts |

新增 `.cpp`、`.h` 或 QML 檔時要同步更新對應 `CMakeLists.txt`；專案刻意使用明確 source lists，不使用自動 glob。

`src/app/src/main.cpp` 除了正式啟動，也承擔 CLI smoke、screenshot、viewer launch 與 performance metrics。修改啟動流程時必須驗證這些診斷入口，不能只手動開啟 GUI。

## 安全界線

- 測試、smoke 與診斷使用 `PICLENS_DATA_ROOT` 或 `--data-root` 隔離，不得碰使用者真正的 local app data。
- Trash 必須使用 OS recycle bin／trash；Linux `gio trash` 失敗時不可退回永久刪除。
- 轉檔保留原檔；target collision 必須略過，不覆寫。
- Settings 更新維持 normalization、atomic write 與歷史 schema continuity。
- 搜尋、排序、重新掃描、recursive mode 與檔案操作後不得殘留 stale selection。
- Folder tree 不得因 library 搜尋或目前資料夾切換而 reset／收合。
- Gallery context menu 與 viewer reveal 失敗時必須保留目前狀態、回報錯誤並寫入診斷。
- QML pointer capture、selection、drag/drop 與 viewer overlay 有刻意的事件邊界；修改時至少執行 QML 與 presentation tests。
- Thumbnail pipeline 是 bounded asynchronous work，包含 cancellation、timeout、cache bound 與 stale-generation discard；不要在 UI thread 同步 decode。

完整行為以 [runtime invariants](runtime-invariants.md) 為準；刻意改變行為時，在同一個 change 更新規格、invariants 與測試。

## 驗證

日常修改先執行最接近的 test target，交付前依變更範圍執行完整 Debug：

```powershell
cmake --preset debug
cmake --build --preset debug
ctest --preset debug --output-on-failure
```

碰 optimizer、部署、資源或 packaging 時再執行 Release。Windows 完整 release gate 需要 caller 提供 representative library：

```powershell
pwsh -NoProfile -File scripts/run-windows-cutover-gate.ps1 `
  -PerformanceFolder <representative-folder>
```

Package lifecycle 會安裝、替換或解除安裝軟體，必須遵守 [testing](testing.md) 與 [release](release.md) 中的顯式確認和 disposable-runner 規則。

## 最小交付檢查

- 修改位於正確 layer，並優先重用既有規則與 controller。
- 新行為有最小可失敗測試；契約變更同步更新文件。
- 執行與變更相稱的 build、tests、smoke 或 package audit。
- 檔案操作沒有永久刪除、覆寫或觸碰真實 profile。
- Packaging 變更沒有讓 portable、MSI、DEB、RPM 形成不一致的產品內容。
- 最終 diff 沒有無關格式化、生成產物或使用者既有變更。
