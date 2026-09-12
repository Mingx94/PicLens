# 共用驗收資料

`windows-native-cases.json` 只保存兩平台共用的純資料案例：`SORT-01` 的自然排序輸入，以及 `DATA-01` 的設定正規化輸入。它不宣告不存在的檔案路徑，也不包含二進位 fixture manifest。

Arch 測試使用 `QTemporaryDir` 在執行時產生可丟棄素材。圖片、資料夾、Unicode 名稱、損壞內容、symlink、快取檔與設定檔都由測試本身建立；因此不需要提交使用者圖庫或額外依賴。Windows consumer 可繼續讀取既有的 `schemaVersion`、`naturalSort`、`settings` 與 `description` 欄位。

| 共用案例 | Arch 實際測試 | 素材或產生方式 |
| --- | --- | --- |
| `SORT-01` | `apps/linux/tests/domain_test.cpp`：`sharedCases()`、`domainCases()` | 讀取 JSON 的 `naturalSort`；另以純字串確認數字排序與穩定結果。 |
| `DATA-01` | `apps/linux/tests/domain_test.cpp`：`sharedCases()`、`domainCases()`；`apps/linux/tests/app_test.cpp`：`atomicSettings()`、`dataRootPrecedenceAndAtomicFailure()` | `QJsonObject`／JSON bytes、`QTemporaryDir` profile、損壞檔 quarantine、同一 settings destination 的寫入失敗與原始 bytes 保留。 |
| `FILE-01` | `apps/linux/tests/domain_test.cpp`：`fileCases()`；`apps/linux/tests/controller_test.cpp`：`canceledConfirmationDoesNotWrite()` | `QTemporaryDir` 內的文字檔與 QImage 產生的 PNG；轉檔、來源保留、取消與衝突。 |
| `FILE-02` | `apps/linux/tests/domain_test.cpp`：`fileCases()` | 可丟棄來源、取消、缺少 trash helper、逾時與未知結果。 |
| `FILE-03` | `apps/linux/tests/domain_test.cpp`：`fileCases()`；`apps/linux/tests/controller_test.cpp`：`renameReportsResultAndKeepsSourceScope()` | `QTemporaryDir`、來源 stamp、Unicode basename、同名／衝突與 Linux no-replace rename。 |
| `RESULT-01` | `apps/linux/tests/domain_test.cpp`：`fileCases()`；`apps/linux/tests/controller_test.cpp`：`renameReportsResultAndKeepsSourceScope()` | 可丟棄副本驗證逐項成功、略過、取消、失敗與來源／目標／原因。 |
| `THUMB-01` | `apps/linux/tests/imaging_test.cpp`：`staticFormats()`、`animationAndOversizeRejected()`、`coldWarmAndIdentity()`、`failuresAndCancellationAreTerminal()` | QImage 產生 JPG／JPEG／PNG／BMP／WebP；測試內建立 GIF、動畫 WebP、透明 PNG、損壞圖片與超限 BMP。 |
| `VIEW-01`／`VIEW-03`／`VIEW-04` | `apps/linux/tests/controller_test.cpp`：`staleNavigationAndImmutableViewerSnapshot()`；`apps/linux/tests/imaging_test.cpp`：`originalTilesAndBorders()`、`threePreviewsFitActualAllocationBudget()` | 兩個暫存資料夾、QImage、A-B-A、重新載入、關閉重開、預覽／原圖 tile 與資源上限。 |
| `JOB-01` | `apps/linux/tests/imaging_test.cpp`：`queueBoundAndShutdownReap()`、`stalledWorkerTimeout()`、`quiesceReapsAndAllowsResume()` | 現有 test executable 作為 bounded helper；以 marker、timeout、cancel、shutdown 驗證回收。 |

平台差異只描述可重現的命名邊界。Windows 使用不分大小寫語意並保留 `CON`、`PRN`、`AUX`、`NUL`、`COM1`、`LPT1` 等裝置名稱；`/`、`\\`、NUL 及 `.`／`..` 不可作一般 basename。Arch 使用大小寫敏感的 POSIX 語意；`/`、NUL、`.`、`..` 仍受限制，但 Windows 保留名稱文字本身（例如 `CON`、`NUL`）可作一般 basename，前提是實際檔案系統允許。這些欄位是規則說明，不代表兩平台已互相執行測試。
