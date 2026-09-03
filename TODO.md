# PicLens egui migration TODO

本文件追蹤 PicLens 從 GPUI 遷移到 egui／eframe 的階段性目標。產品行為以 [產品規格](docs/product-spec.md) 為權威，工程限制以 [Runtime invariants](docs/runtime-invariants.md) 為權威。遷移不得默認改變產品範圍、設定格式、資料位置或檔案操作語意。

目標架構採用 Fastpotify 的事件驅動模式：UI 在繪製時只產生 `Action`，App 在繪製後套用 action；需要副作用的工作轉成 `Command` 送到 background backend，完成後以 `Event` 回傳並要求 egui repaint。

```text
egui view -> Action -> App reducer -> Command -> background backend
                   ^                         |
                   +--------- Event <-------+
```

遷移期間保留 `piclens-domain` 與 `piclens-infra`。`piclens-desktop` 已在階段 7 成為唯一 frontend；完成紀錄保存在 [egui 遷移歸檔](docs/archive/2026-09-egui-migration.md)。

## 完成規則

- [x] 每個已勾選項目都有可重現的測試、smoke、量測或決策紀錄。
- [x] 階段退出前，受影響的 Cargo format、build、check、test 與 clippy gates 全部通過。
- [x] Runtime 行為變更使用隔離的 `PICLENS_DATA_ROOT` 執行真實 app smoke，並檢查 app log。
- [x] Headless render 只證明 layout 可建立；它不取代真實輸入、accessibility、平台整合或像素驗證。
- [x] 遷移期間不刪除 GPUI frontend，也不改變預設 app，直到「階段 7：正式切換」完成。

## 階段 0：凍結基線與架構決策

目標：先建立可比較的 GPUI 行為基線，並固定 egui frontend 的責任邊界。

- [x] 建立遷移設計紀錄，定義 `piclens-desktop -> piclens-infra -> piclens-domain` 的依賴方向。
- [x] 定義 `Action`、`Command`、`Event`、request identity 與 generation 的責任及命名規則。
- [x] Windows frontend 固定使用 `wgpu`；啟動、圖片 texture、縮放與 GPU Engine 已有實機證據。
- [ ] 在 Linux 驗證 `wgpu` 啟動、圖片 texture、縮放和 GPU 相容性後，完成跨平台 renderer 結論。
- [x] 決定背景執行模型；比較有界 worker pool 與 Tokio runtime，不為了模仿參考專案而引入無使用需求的 async runtime。
- [x] 記錄 GPUI 基線：啟動、選擇資料夾、搜尋、排序、選取、viewer、重新命名、回收筒及一項轉換操作。詳見 [GPUI migration snapshot](docs/archive/gpui-experiment.md#frozen-v012-behavior-baseline)。
- [x] 以代表性圖庫重建 `v0.1.2` Viewer 冷／暖快取與 500ms 指標，並記錄原版 Gallery workload 會被大量 thumbnail tasks 淘汰，故無法取得有效持續捲動值；不得修改 frozen baseline 來補造結果。詳見 [GPUI migration snapshot](docs/archive/gpui-experiment.md#reproduced-release-metrics)。
- [x] 列出 GPUI frontend 的功能、快捷鍵、dialog、focus、drag/drop、accessibility 與平台整合對照表。

退出條件：架構決策可供實作，且有足夠基線可判斷遷移是否造成行為或效能退化。

## 階段 1：建立可啟動的 egui frontend

目標：加入可獨立啟動與測試的 `piclens-desktop`，但不實作完整產品功能。

- [x] 新增 workspace member `crates/piclens-desktop`，使用鎖定版本的 egui／eframe。
- [x] 建立 `main.rs`、`app/`、`model.rs`、`backend.rs`、`images.rs`、`theme.rs` 與 `ui/` 模組骨架。
- [x] 完成 eframe 啟動、主視窗設定、繁體中文 bundled font、app icon 與 light theme。
- [x] 支援 `PICLENS_DATA_ROOT`、現有 settings、log 路徑與 `--folder`／smoke 類 CLI 入口。
- [x] 建立集中式 frame lifecycle：poll backend events、render UI、apply actions、同步狀態。
- [x] 只有動畫或未完成工作可要求連續 repaint；idle 時不得 busy repaint。
- [x] 建立 demo fixture，使 headless test 不需要讀取使用者檔案。
- [x] 加入最小 headless render test，涵蓋主 shell、空狀態與 error state。

退出條件：新 frontend 能在隔離 profile 啟動和關閉，idle 不持續佔用 CPU，且 workspace gates 通過。

## 階段 2：完成 Action／Command／Event 主幹

目標：讓 view、狀態轉移與背景副作用有清楚且可測試的邊界。

- [x] 在 `model.rs` 定義 framework-light 的頁面、dialog、selection、viewer 與 `Loadable<T>` 狀態。
- [x] View 在繪製時只收集 `Action`；不得直接掃描磁碟、解碼圖片或執行檔案操作。
- [x] App reducer 在繪製後依序套用 action，並正確處理 action 產生的新 action。
- [x] Backend 使用 channel 接收 `Command`、回傳 `Event`，並在送出事件後呼叫 `request_repaint()`。
- [x] Blocking filesystem、image decode 與 helper process 全部離開 UI thread。
- [x] 所有佇列、worker 數量、timeout、cache 與 shutdown 都有明確界限。
- [x] Generation 或 request identity 不相符的 event 不得更新 UI state。
- [x] Shutdown 會停止接收新工作、取消可取消工作並結束 background owner。
- [x] 以 reducer 與 backend contract tests 驗證 event ordering、stale result、錯誤及 shutdown。

退出條件：測試能證明 UI thread 不執行 blocking work，且快速切換狀態時過期 event 不會污染畫面。

## 階段 3：圖庫垂直切片

目標：完成從選擇資料夾到可捲動縮圖圖庫的第一條端到端路徑。

- [x] 實作資料夾選擇、startup restore、folder tree root 與 history navigation。
- [x] 實作目前資料夾、包含子資料夾、重新整理和單次 collection reset。
- [x] 實作固定 grid gallery 與虛擬化；10,000 個 item 不為不可見 tile 建立持久 widget state。
- [x] 實作搜尋、四種排序、自然排序、項目數與縮圖大小設定。
- [x] 實作普通、Ctrl、Shift、Ctrl+Shift 選取，以及穩定 range anchor 和 selection order。
- [x] 實作 thumbnail image loader；cache key 包含來源 identity 與尺寸。
- [x] 只排程 visible/materialized 靜態圖片；tile unload 和 generation change 可取消或淘汰工作。Viewer open 的暫停與恢復由階段 4 單獨追蹤。
- [x] 保留既有 thumbnail cache pruning 規則：單一 background owner、dirty 後每五秒清理、每輪保留快照中最新 2,000 筆。
- [x] Animated GIF/WebP 顯示不支援預覽，不嘗試播放。
- [x] 加入 gallery headless tests，以及 navigation、selection、search、sort、cancel 和 stale thumbnail tests。

退出條件：使用者能完成資料夾導覽、搜尋、排序、選取和持續捲動；縮圖工作符合所有界限，且不阻塞 UI thread。

## 階段 4：內嵌圖片檢視器

目標：達到目前 viewer 的導覽、輸入、記憶體與延遲契約。

- [x] 開啟 viewer 時建立 immutable visible-image sequence snapshot。
- [x] 實作上一張、下一張、Escape、鍵盤導覽、圖片名稱與 reveal-in-file-manager。
- [x] 實作 pointer-anchor zoom、zoom clamp、reset、drag pan 與 canvas input interception。
- [x] Viewer 開啟時取消並暫停被遮住的 gallery thumbnail requests；關閉後恢復可見縮圖排程。
- [x] 每次只持有一個載入或預載工作；目前 preview 完成後才依序預載下一張與上一張。
- [x] 只保留目前與相鄰兩張 preview texture，共最多三張、12 MiB；淘汰或 close 時釋放 texture。
- [x] 快速 A-B-A、close/reopen、generation change 與 shutdown 只接受 identity 相符的結果。
- [x] 清晰 preview 維持最長邊 1024 px，完成後直接以完整不透明度繪製。
- [x] 記錄冷／暖快取和同一 viewer 內連續切換的首次清晰繪製時間及超標次數。詳見 [Performance](docs/performance.md)。
- [x] 測試 viewer snapshot、preload order、stale result、texture budget、zoom/pan、focus restore 與 unsupported animation。

退出條件：viewer 功能與 runtime invariants 完成，且代表性 Release smoke 可產生可信的 500ms 指標。

## 階段 5：檔案操作、dialog 與拖放

目標：恢復所有會修改使用者檔案的功能，並維持保守操作規則。

- [x] 實作圖片 context menu 與 selection-aware action scope。
- [x] 實作單張重新命名、移至回收筒與 reveal-in-file-manager。
- [x] 實作 JPG、lossless WebP 轉換與同 basename 格式清除。
- [x] 實作確認、進度、結果與錯誤 dialog；confirmation 不得 auto-confirm。
- [x] 批次操作回報總數、成功、略過、失敗與逐項結果；單項失敗不停止其他項目。
- [x] Conversion 與 drop-target collision 略過；single rename existing target 拒絕；所有操作均不覆寫。Trash-like operation 不可 fallback 為永久刪除。
- [x] 實作 drag threshold、preview、drop target、autoscroll、cancel 和 capture-lost cleanup。
- [x] 實作 drop-target rename preview、最小可用序號與確認後才執行的流程。
- [x] 操作完成後 reload gallery，並清除 stale selection、anchor 和 drag session。
- [x] 使用 disposable copied fixture 測試成功、取消、collision、部分失敗及 helper timeout。

退出條件：所有檔案操作都可由 UI 完成，且測試證明取消不修改檔案、失敗不中斷批次、不覆寫、不永久刪除。

## 階段 6：互動、accessibility 與視覺完成度

目標：完成產品級鍵盤、focus、resize、視覺及平台行為。

- [x] 對照 GPUI 基線補齊快捷鍵、滑鼠側鍵、scroll、hover、tooltip、context menu 與 focus order。
- [x] 定義所有主要 control、gallery item、dialog 和 viewer control 的 accessibility name、role、state 與 action。
- [x] 驗證 Escape、dialog close、viewer close 與檔案操作後的 focus restore。
- [x] 驗證 1280×800、800×600、常用 display scale 與長繁體中文文字。
- [x] 對照現有 design system 完成 spacing、typography、color、selection、loading、error 與 disabled state。
- [x] 建立主要頁面、panel、dialog、empty、loading 和 error state 的 headless render suite。
- [x] 使用 Windows 真實 app 檢查 1280×800 layout、圖片品質、拖放、tooltip 與視覺狀態。
- [x] 使用 Windows 真實 app 完成高 DPI 像素驗證；證據見 [Performance](docs/performance.md#windows-高-dpi-像素證據--2026-09-03)。
- [x] 檢查 Windows idle、持續捲動、搜尋和 viewer navigation 的 CPU、GPU 與記憶體行為；證據見 [Performance](docs/performance.md#windows-egui-runtime-evidence--2026-09-03)。
- [x] 使用 disposable copied fixture 檢查 Windows 批次操作期間的 CPU、GPU 與記憶體行為；證據見 [Performance](docs/performance.md#windows-批次操作證據--2026-09-03)。

退出條件：功能對照表沒有未說明缺口；headless、互動、accessibility 與真實 app 視覺驗證各自完成。

## 階段 7：正式切換與移除 GPUI

目標：讓 egui frontend 成為唯一正式 app，並清理遷移期間的雙重路徑。

- [x] 執行完整 GPUI／egui parity review，所有差異都有產品核准或明確缺陷項目。詳見 [egui migration design](docs/archive/2026-09-egui-migration.md#2026-09-03-parity-review)。
- [x] 補齊 egui settings parity：視窗 resize 後以 background command 保存正規化後的視窗大小，不可在 UI thread 寫檔。
- [x] 補齊 egui 虛擬化圖庫的鍵盤捲動：游標移到未 materialize 的項目時，目標項目必須進入 viewport。
- [x] 補齊 egui 暫時性 `--search`、`--include-subfolders` 與 `--sidebar-closed` CLI override。
- [x] 補齊 egui automated screenshot 與持續捲動 workload。
- [x] 補齊 egui CPU、記憶體、thumbnail、search 和 scroll metrics。
- [x] 使用既有 settings、cache 和 log profile 驗證向前資料相容；不得破壞或靜默重設使用者設定。
- [x] 更新 root workspace default member、README、architecture、development、testing、design 與 release 文件。
- [x] 更新 screenshot、performance、package、smoke 與 CI scripts，使其執行 `piclens-desktop`。
- [x] 更新 Windows MSI、portable archive、DEB 與 RPM 的 binary、desktop entry、icon 和 metadata。
- [x] 完成 workspace 全套 format、build、check、test、clippy 與 `git diff --check`。
- [x] 以隔離 profile 執行完整真實 app smoke，並確認 log 沒有未說明錯誤。
- [x] 將 GPUI 遷移紀錄移到 `docs/archive/`，再移除 `piclens-gpui`、GPUI dependencies、skills 和 dead code。
- [x] 在移除後重新執行全套 workspace gates、package build 與 smoke。

退出條件：egui 是唯一 frontend；repository、文件、測試、腳本與 package 不再引用有效的 GPUI runtime 路徑。

## 階段 8：原生平台與 release 驗證

目標：取得本機 build 無法取代的安裝、互動、效能與發佈證據。

- [x] 在 clean Windows runner 執行 MSI install、launch、replace、uninstall 與 profile preservation lifecycle。腳本：`scripts/test-msi-lifecycle.ps1`；[Windows packages run 33756956796](https://github.com/Mingx94/PicLens/actions/runs/33756956796) 已使用 `-ConfirmSystemChanges` 通過。
- [ ] 在 clean Ubuntu runner 執行 DEB build 與 lifecycle。腳本：`scripts/build-deb.sh`、`scripts/test-linux-package-lifecycle.sh`。
- [ ] 在 clean Fedora runner 執行 RPM build 與 lifecycle。腳本：`scripts/build-rpm.sh`、`scripts/test-linux-package-lifecycle.sh`。
- [x] 使用原生 UI automation 檢查 1280×800 與 800×600 的 delayed tooltip、accessibility name、focus restore、dialog、drag、scroll 與檔案操作結果。
- [x] 在大型 disposable image library 執行 Release metrics，記錄 CPU/GPU、storage、display scale，並驗證持續捲動、search 與 viewer open。腳本：`scripts/measure-performance.ps1`；證據：`docs/performance.md`。
- [x] `v3.0.0` 沒有產品核准的正式效能門檻，因此不加入自動 performance gate；現有 metrics 維持觀測用途。
- [x] `v3.0.0` 沒有受保護的 code-signing identity，因此 release assets 維持 unsigned，並在 Release body 明確標示。
- [x] 經使用者明確授權後，建立匹配 Cargo version 的 annotated `v3.0.0` tag，指向 `ac544e4d` 並 push。
- [x] Hosted Windows release workflow 的 MSI lifecycle job 已成功；[GitHub Release v3.0.0](https://github.com/Mingx94/PicLens/releases/tag/v3.0.0) 包含 MSI、portable ZIP 與兩個 SHA-256 checksum files，下載後重新計算的雜湊均相符。

退出條件：所有支援平台都有 package lifecycle、真實互動與效能證據，且 release artifacts 完整可追溯。

本機 compilation、test、headless render、package build 或 launch-only smoke 都不會自動勾選需要真實平台或使用者授權的項目。
