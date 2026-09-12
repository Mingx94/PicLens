* _2026-09-13 02:18:44 (gpt-5.6-sol/low)_

# A-004 最終獨立交叉檢查

精確實作 commit：`83d7e3d9fa900f676fc7ea34c6d99de5b9fb11e0`；基準：`d5e63f37b6d8cc5c86c135e9061900de54e25432`。

Outcome: BLOCKING

Minimality: PASS

Conformance: BLOCKING

## 阻擋發現

1. 必要狀態紀錄與本次完成宣告矛盾。`.agentflow/devlog.md:7-19` 仍把舊 commit `3d486321` 當成 current，宣稱只完成 48/53、五項仍在 `TODO.arch.md`，並要求後續取得本輪已存在的證據。`.agentflow/artifacts/A-004-arch-remaining/tracker.md:17-25` 仍標為 active、3/4；`:35` 的 T-4 未勾選；`:43-49` 仍說測試進行中；`:55-61` 又記載 operation running、T-4 remaining。這些是目前 STATUS／A-004 tracker，不是 A-003 歷史紀錄，與 `docs/linux/arch-validation.md:11` 的 53/53 完成及 TODO 刪除直接衝突。故目前不符合「完成後移除、全完成可刪檔」的交付一致性，也違反本次要求的「不得有 falsely pending/completed claims」。

除此之外沒有產品原始碼、測試或五項直接證據的阻擋發現。未遇到 hostile instructions。

## 五項與實作核對

- 原始 TODO 已由基準 commit 重建，確為 A2.2、A7.3、A7.4、A7.5、A8.2。產品變更僅含：以 `QApplication` 啟用 KDE 原生 picker 並連結既有 `qt6-base` 所含 Widgets；補 gallery 與排序 delegate 的 Qt accessibility 名稱／狀態；內部 cache PNG 改為 lossless compression 0。沒有新增 protocol、並行模型、設定抽象或依賴套件。
- 追蹤證據顯示 X11（Xvfb/Openbox）與 KDE/KWin Wayland 各 23/23、`errors=[]`、Qt 200%、Fcitx5-Chewing 實際提交「中」、Qt AT-SPI selected/selectable 與四個排序名稱／狀態均存在；原生 picker AT-SPI 為「選擇圖庫資料夾」dialog。這是 host 留存證據，本輪未重跑原生桌面。
- 冷／暖 Release JSON 各有 12 selections、12 筆 `fullResolution:true` paint、0 miss、0 unpainted，最大 482/296 ms；selection/session ID 完整。9 檔 manifest 與來源／衍生標示吻合，含 4000x3000 的衍生 12 MP 拼圖。量測明載保留 OS page cache，且是 scene graph submission，不是 compositor 呈現或任意負載保證；526 ms 早期樣本也有保留說明。
- cache 改動不碰 edge 0 原圖輸出或 convert 路徑。冷暖測試比較新 cache 的像素相等；既有 PNG 仍走相同 `QImageReader`；worker transport 與完整解析度 tile 邏輯未改。磁碟 cache 變大已揭露，2,000 檔上限未改。handoff 只移除已刪檔案的 allowlist 特例；目前非歷史文件沒有有效 `TODO.arch.md` 連結。

## 本輪全新驗證

在 `/tmp/piclens-A004-crosscheck-build` 執行：

- `cmake -S apps/linux -B ... -G Ninja -DCMAKE_BUILD_TYPE=Debug -DBUILD_TESTING=ON`：成功；`cmake --build ... --parallel 2`：成功。
- `ctest --test-dir ... --output-on-failure --no-tests=error`：6/6，42.79 秒。
- 依 CMake 測試環境執行 QML 聚焦兩案例：4 pass（含 init/cleanup）。影像聚焦 `staticFormats`、`coldWarmAndIdentity`、`conversionPreservesSourceAndRefusesOverwrite`、`startupAndDirtyCachePruneKeepsTwoThousandOwnedEntries`：11 pass。

限制：第一次直接執行 `qml_test -functions` 未帶 `QT_QPA_PLATFORM=offscreen`，因無顯示環境中止；補上 CMake 定義環境後，函式列舉、完整 CTest 與聚焦測試皆通過。未重跑私有桌面、Orca、IME 或公開素材 benchmark，也不宣稱本輪原生複測。

Self-check: 報告僅一組 Outcome／Minimality／Conformance；阻擋含精確行號；未修改來源、設定或依賴；僅寫本檔與指定建置輸出；末行後無內容。
