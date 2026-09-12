* _2026-09-12 23:45:24 (gpt-5.6-luna/xhigh)_

## 結果

本次 correction 保留既有 core 實作與測試範圍，只修正驗收證據的準確性。

- `Controller::start` 仍只在 explicit／restored path 存在、是資料夾且可讀時建立 root；無效啟動路徑保留空狀態 picker、寫入狀態與 log，不自動改寫 `lastFolderPath`。
- 移除 `fixtureManifest` 及其自我鏡像檢查。沒有提交虛構路徑、二進位 manifest 或新增依賴。
- `test-data/windows-native-cases.json` 保留既有 `schemaVersion`、`naturalSort`、`settings` 與 `description` consumer contract；平台欄位改為明確描述 Windows 保留名稱，以及 Arch 的 `/`、NUL、`.`、`..` 限制與可接受的 Windows 保留名稱文字。
- `dataRootPrecedenceAndAtomicFailure()` 現在對同一個 profile、同一個 `piclens-settings.json` 目的地製造寫入失敗，保存並比對失敗前後的完整 bytes；權限會先恢復，再進行任何後續驗證。若執行身分可繞過權限，該 permission case 只會以明確原因 `QSKIP`。
- 同輸入 request 測試現名為 `sameInputRequestsHaveIndependentResults`。兩個 callback 與相同 pixels 只證明兩個結果可獨立完成，不宣稱只有一個 helper process。

## 變更檔案

- `apps/linux/src/controller.cpp`：`Controller::start` 的 invalid initial／restored folder validation、status 與 log。
- `apps/linux/tests/app_test.cpp`：XDG／環境／explicit precedence、同 destination atomic failure、深層遞迴、symlink loop、取消、missing 與權限錯誤。
- `apps/linux/tests/controller_test.cpp`：missing／file／unreadable／restored startup、10,000 項目 single reset、stale navigation、immutable viewer snapshot、close/reopen、Unicode rename 結果。
- `apps/linux/tests/domain_test.cpp`：共用純資料案例、平台命名限制、legacy JSON、Unicode basename、operation source dedup。
- `apps/linux/tests/imaging_test.cpp`：六種格式、動畫 GIF／WebP、alpha、corruption、同輸入獨立結果、32 MiB／256 筆近期快取、2,000 筆 dirty prune。
- `test-data/windows-native-cases.json`：共用純資料與平台命名規則欄位；無 fixture manifest。
- `test-data/README.md`：將真實共用 acceptance ID 對應到現有 source tests 與 runtime generators。
- `core-implementation-report.md`：本報告。

未修改 CMake、QML、TODO、既有 README；沒有 commit、push 或網路操作。

## 驗證

- `python3 -m json.tool test-data/windows-native-cases.json`：exit 0。
- `cmake -S apps/linux -B /tmp/piclens-core-worker-build -G Ninja -DCMAKE_BUILD_TYPE=Debug -DBUILD_TESTING=ON`：exit 0。CMake 只回報缺少 Vulkan headers 的可選警告。
- `cmake --build /tmp/piclens-core-worker-build -j2`：exit 0。
- `env PICLENS_DATA_ROOT=/tmp/piclens-core-correction-focused-data QT_QPA_PLATFORM=offscreen QT_QPA_PLATFORMTHEME= ctest --test-dir /tmp/piclens-core-worker-build -R '^(domain|app)$' --output-on-failure`：exit 0，2/2 passed，0.24 秒。
- `env PICLENS_DATA_ROOT=/tmp/piclens-core-correction-full-data QT_QPA_PLATFORM=offscreen QT_QPA_PLATFORMTHEME= ctest --test-dir /tmp/piclens-core-worker-build --output-on-failure`：exit 0，4/4 passed，39.31 秒。
- `/tmp/piclens-core-worker-build/app_test -v1`：7 passed、0 failed、0 skipped；atomic failure 使用同一 settings destination 並保留原始 bytes。
- `git diff --check`：exit 0。

Startup regression 的 red-before-green 證據保留自本次實作驗證：修正前 `controller_test missingStartupPathKeepsPickerState` 與 `controller_test fileStartupPathKeepsPickerState` 各 exit 1，因舊行為建立 invalid `rootPath`；加入 `Controller::start` 驗證後，兩個案例均 PASS。這是本次唯一允許的 runtime fix 證據。

## Covered TODO IDs 與仍未證明的 clauses

- A0.3：已以 `windows-native-cases.json` 與 `test-data/README.md` 對應 `SORT-01`、`DATA-01`、`FILE-03` 等真實案例，並記錄 Windows／Arch 大小寫、separator 與 basename 限制。仍未證明 Windows consumer 實際執行、完整跨平台輸出一致性、二進位 fixture hash 與每個平台的實機結果。
- A1.1：已驗證 JPG、JPEG、PNG、BMP、WebP、GIF、動畫 GIF／WebP、透明 alpha、損壞圖片與原始尺寸。仍未證明乾淨部署環境的 codec／plugin 組合，也未做 QML 畫面的動畫提示驗收。
- A2.1：已驗證舊 JSON 欄位、numeric enum、normalization、損壞檔 quarantine、XDG／環境／explicit precedence，以及同一 settings destination 的 atomic failure 原始 bytes 保留。仍未證明寫入中斷、完整設定檔權限矩陣、命令列 `--data-root` precedence 與升級生命週期。
- A2.3：已驗證 80 層遞迴、空資料夾、symlink loop 不展開、預先取消、missing path 與 unreadable child error。仍未驗證 active scan 中途取消的精確時序與大量錯誤的完整 UI 回饋。
- A2.6：已驗證 controller 導覽結果不讓 stale folder 覆蓋目前資料夾，並在 reload／搜尋後清除選取。仍未以可控慢速 worker 逐一證明 sort、recursive、refresh 交錯下的所有 queued-result ordering。
- A3.1：已用真實 `Controller`／`Rows` 驗證 10,000 項目 load／search 的 single model reset。仍未證明實際 10,000 檔案磁碟載入、GridView container 有界限與 QML reuse。
- A4.2：已驗證同輸入 request 的兩個結果可獨立完成、既有可見縮圖排程與取消路徑。相同 callback 與 pixels 不證明 helper process 去重；仍未證明 QML materialized／unload lifecycle 的完整排程與去重行為。
- A4.3：已驗證 path、mtime、size、edge identity 在 cold／warm／source change 下不混用結果。仍未證明 QML render-thread submission 與所有 generation 交錯。
- A4.4：已驗證冷載入、warm PNG、RGBA frame、原圖不進縮圖 cache 與 conversion temporary cleanup。仍未逐一觀察 Imaging timeout、取消、shutdown 每條路徑的暫存清除。
- A4.5：已驗證近期快取 32 MiB／256 筆邊界、startup prune 2,000 筆與 dirty 約 5 秒 prune。仍未由公開 API 證明單一 background prune task 的內部執行緒數，以及兩輪之間新寫入的細節。
- A4.6：已驗證 queue bound、stalled timeout、cancel、quiesce、shutdown 與後續工作恢復。仍未驗證同一 Imaging instance 中 stalled 與 good worker 並行時的完整混合進度。
- A5.1：已驗證 selection order 第一張、immutable viewer snapshot、A-B-A、close、focus signal 與 reopen。仍未由 core 測試證明內嵌 QML viewer、Escape 與真實 focus return。
- A5.3：已驗證 1024 preview／完整原圖 frame、原圖 tiles、動畫 unsupported 與解碼失敗回報。仍未驗證 preview 失敗後保留既有 preview 的 UI 狀態。
- A5.6：已驗證 A-B-A、reload 不改 snapshot、來源消失的 Imaging failure path、close/reopen 基本 lifecycle。仍未證明 scene graph 資源釋放、超限取消與相鄰 preload 上限。
- A6.1：已驗證 visible plan、source stamp、collision recheck、不可覆寫 rename、partial result 與 cancellation。仍未完成真正 TOCTOU 多程序時序與完整確認 UI snapshot acceptance。
- A6.2：已驗證 49／50 confirmation boundary、取消零修改、source retain、既有 WebP conversion 與 collision。仍未補齊 JPG quality 100 與 WebP lossless 的 bit-level／metadata 證據。
- A6.3：已驗證 visible scope，以及 same-basename cleanup 保留 JPG／JPEG／WebP。仍未驗證真實 Arch desktop trash 成功案例。
- A6.4：已驗證 Linux 大小寫語意、Unicode basename、同名／target collision、source stamp change 與 no-replace rename。仍未證明 controller end-to-end 的權限失敗回饋。
- A6.5：已驗證缺少 helper、timeout、取消、Unknown 結果與 source 保留。仍未驗證真實 Arch desktop recycle bin／`gio trash` 整合。
- A6.8：已驗證跨副檔名最小序號、preview 後 recheck 與 collision skip。仍未驗證 QML drag session、threshold、autoscroll、capture-lost。
- A6.9：已驗證逐項 source／target／reason、成功／略過／取消／失敗結果與 controller toast 資料。仍未驗證 6／12 秒 toast 與主動詳情入口。
- A6.10：已驗證 source missing、preview 後 collision、partial completion、cancellation 與 helper unknown。仍未完成權限失敗、App 關閉中的結果不確定狀態與完整檔案核對。

未在本次 core route 宣稱完成 A1.3–A1.5、A2.5、A3.2–A3.6、A4.1、A5.2、A5.4–A5.5、A6.6–A6.7 及所有 A7–A10；這些需要 QML、native desktop、效能、封裝或部署證據。

Self-check: Corrected invented fixture metadata and ineffective atomic-failure evidence only.
