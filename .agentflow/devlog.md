# STATUS

Project: PicLens

Notebook: .agentflow/devlog.md — root.

Current commit: implementation `3d486321151e654de43dab62793a938cd435721e` on main, pushed to origin/main.

Tests/scenarios: Arch Debug 6/6 43.81s, Release 6/6 42.61s, isolated clean Arch makepkg 6/6 42.85s, independent review 6/6 43.46s; native Wayland/PTY/package lifecycle and profile retention passed within documented limits.

Configuration: ag.json — schema v7; validated for codex this round.

Proven: 48 of 53 Arch TODO items completed and removed; startup/search/drop fixes and diagnostics implemented; final independent review and Host gate PASS for implementation 3d486321; package available in dist/arch-validation-A003.

Open: A2.2 native picker interaction; A7.3 KDE Wayland/IME; A7.4 native X11; A7.5 200% and native accessibility tools; A8.2 representative mixed-image corpus performance. These five remain in TODO.arch.md. Prior Windows W7.3-W7.5 remain unchanged.

Next: obtain the specified desktop/tool/corpus evidence for the remaining five Arch items; remove TODO.arch.md only when all pass. Current executable A-003 work and review are complete.

Artifacts: .agentflow/artifacts/A-003-arch-todo — tracker, briefs, reports and bounded evidence; docs/linux/arch-validation.md summarizes results and limits. Rootless userland shares the host kernel; no full VM, KDE/X11 or representative image-performance claim.

Archived eras: none.

Streams: none.

---

# → Ask / A-001

godev

## [RUN-001] Event — route selection (during round A-001)

- 已完成 bounded intake。首次檢查確認缺少 `ag.json`，且未改動檔案。
- 依新專案規則執行初始化；建立 `.agentflow/devlog.md`、`ag.json`，並更新 `.gitignore`。
- 第二次 intake 驗證 `ag.json` schema v7、`main` 分支與 root notebook。
- Route: direct。此輪只處理 Agentflow 啟動與初始化，不變更 PicLens 產品程式碼。

## [RUN-002] Event — initialization validation (during round A-001)

- `git diff --check`: PASS。
- `ag.json` JSON 解析：PASS。
- `resume-intake.js`: PASS；確認 schema v7、`main` 分支與 `.agentflow/devlog.md`。
- 變更範圍只含 `.agentflow/devlog.md`、`.gitignore`、`ag.json`；未執行產品測試，因未改 PicLens 程式碼。

## [RUN-003] Event — cross-check attempt 1 failed (during round A-001)

- `external-runner-v1` 建立獨立、無 remote 的 disposable clone，並啟動 `gpt-5.6-luna/max`。
- 外部執行環境實際套用 `read-only` sandbox；worker 無法建立唯一允許的 `agentflow-review.md`。
- Process exit 0 但報告缺失，故主流程未接受結果；clone diff identity 未變。
- 這是第 1 次已啟動嘗試。下一次保留相同 brief、模型與 effort，只修正執行 sandbox 參數。

## [RUN-004] Event — cross-check accepted (during round A-001)

- 第 2 次 worker start 完成；profile `codex-default`，model `gpt-5.6-luna/max`，stage `A-001-agentflow-init-cross-check`。
- `external-runner-v1` 證據：獨立 clone、無 remote、stdin closed、exit 0、無 timeout；clone 只新增允許的 `agentflow-review.md`。
- 報告 SHA-256 `71A99D858732C3E5FDA0753CBC938C6EBF9D12359BF88AF655EF22DA88AB0CEB`；Outcome、Minimality、Conformance 各唯一一次且皆 PASS。
- 主流程重查 commit `85727206c92e62a13f24eb16742f8d561bf4bd50`、`git show --check`、報告界線與 runner record：PASS。Host gate: PASS。
- 限制：disposable clone 不證明 OS-level confinement、inherited credentials 或 remote-provider cancellation。
- Disposable clone 已移到資源回收筒；專案內保留 brief、plan、診斷、runner record 與 review report。

## [RUN-005] Event — review record normalization (during round A-001)

- 第一次 Reply preflight 拒絕完成，因 review report 缺少機械欄位 `Verdict:` 與 `Reviewed implementation commit:`。
- 實質報告已對相同 commit 回傳 Outcome、Minimality、Conformance 全部 PASS；依 closeout stop rule，不重跑模型審查。
- 只將既有 `Reviewed commit` 欄位正規化為完整 `Reviewed implementation commit`，並加入由三項 PASS 直接導出的 `Verdict: PASS`；沒有變更實質證據或結論。

# ← Reply / A-001

* _2026-09-12 20:27:30 Asia/Taipei (Codex/GPT-5)_

## [SUMMARY]

- 成功：PicLens 已完成 Agentflow 初始化。
- 已建立 `.agentflow/devlog.md` 與 `ag.json`，並在 `.gitignore` 加入 `.claude/`、`.codex/`、`.worktrees/`。
- 初始化 commit 是 `85727206c92e62a13f24eb16742f8d561bf4bd50`。
- 沒有變更 PicLens 產品程式碼，也沒有執行產品測試。

## [FINAL REPORT]

### 原始要求：`godev`

結果：成功。

- 首次 intake 確認專案尚未有 `ag.json`，且當時沒有改檔。
- 依新專案流程初始化 Agentflow。設定為 schema v7，root notebook 是 `.agentflow/devlog.md`。
- 驗證通過：`git diff --check`、`ag.json` JSON 解析、`resume-intake.js`。
- 實作 commit：`85727206c92e62a13f24eb16742f8d561bf4bd50`（`chore: initialize agentflow`）。
- 獨立 targeted cross-check 已完成。Outcome、Minimality、Conformance 均為 PASS；Host gate: PASS。
- 第一次 review 因外部 sandbox 被降成 read-only，沒有產生報告，未被接受。第二次成功，且 clone 只新增允許的報告檔。
- Disposable clone 已移到資源回收筒，可復原。審查 brief、plan、診斷、runner record 與報告保留在 `.agentflow/artifacts/A-001-agentflow-init/`。
- 限制：沒有驗證 PicLens 執行行為；本輪也不主張 OS-level confinement、inherited credentials 或 remote-provider cancellation。

Cross-check review: `.agentflow/artifacts/A-001-agentflow-init/cross-check-report.md`
Cross-check implementation: 85727206c92e62a13f24eb16742f8d561bf4bd50

## Questions (batched — each with a suggested default)

- None.

---

# → Ask / A-002

godev
完成 windows TODO
完成就移除

## [RUN-001] Event — route selection and Windows TODO inventory (during round A-002)

- Owner Ask 已逐字寫入 A-002；bounded intake 證明目前在 `main`，`HEAD == origin/main`，原始工作樹乾淨。
- `TODO.win.md` 只剩 W7.3、W7.4、W7.5；三項都是互動 Windows 桌面驗收，不是已知缺少產品功能。
- Route: direct。工作拆成環境、IME/UIA、高對比/DPI、picker/reveal/icon、文件與完成閘門五項；不碰 Arch TODO，也不把既有自動化結果冒充實機證據。
- Tracker: `.agentflow/artifacts/A-002-windows-todo/tracker.md`；tracker contract 已驗證。


## [RUN-002] Event — Windows baseline complete (during round A-002)

- T-1 complete。系統為 2560×1440、AppliedDPI=96（100%）、HighContrast=false；已安裝 `zh-Hant-TW` Microsoft Bopomofo IME。
- `dotnet restore --locked-mode`、Release build 與 Release tests 均 exit 0；build 0 warnings/errors，66 tests passed。
- 既有 tests 只作為回歸基線；W7.3 至 W7.5 仍需真實互動桌面證據。下一步是以隔離 profile/fixture 啟動最新 Release build。


## [RUN-003] Event — native Windows automation blocked (during round A-002)

- 最新 Release PicLens 已用隔離 fixture/profile 啟動；process 11508 可正常建立。
- 統一 CUA runtime 只暴露 browser API，沒有 native `getApp`；依 computer-use skill 載入 `@oai/sky` 後，`sky.list_apps()` 明確失敗：`Trusted RPC service is not configured: sky`。
- 因此不能送出實際 Bopomofo composition、切換高對比/DPI 後觀察 renderer、或操作 picker/reveal/taskbar；不以單元測試或 CLI 截圖取代這些證據。
- 隔離 PicLens process 11508 已停止。Tracker 判定 blocked；產品程式碼與 `TODO.win.md` 均未變更。

## [RUN-004] Event — owner clarified completed TODO removal (during round A-002)

- Owner 補充：`完成就移除`。
- Scope 已納入 tracker T-5：只有 W7.3 至 W7.5 全部取得真實互動證據後，才刪除已變空的 `TODO.win.md` 並同步必要文件連結；目前保留。
- 這項補充不解除 native Computer Use RPC blocker，也不授權把自動化測試當成實機驗收。

## [RUN-005] Event — blocked-record cross-check accepted (during round A-002)

- external-runner-v1 建立獨立、無 remote 的 disposable clone；profile `codex-default`，model `gpt-5.6-sol/low`，exit 0，stdin closed，無 timeout。
- Clone 只新增允許的 `agentflow-review.md`；報告 SHA-256 `77D267B9D74A516A319D35B4632A5DF66694D2D87EBC251325DE122EAD801CDE`。
- 報告針對 implementation tip `cbb33d80c4b3a40a8ad2ea4628969c85cd3ea9d2` 回傳 Outcome、Minimality、Conformance 各唯一一次且皆 PASS；並明說 owner outcome 仍 blocked。
- Coordinator 重查 commit、range diff、TODO 狀態、報告界線與 runner record：PASS。Host gate: PASS。
- Disposable clone 已移到資源回收筒，可復原；限制為 OS-level confinement 與 remote-provider cancellation 未證明。


# ← Reply / A-002

* _2026-09-12 20:43:35 Asia/Taipei (Codex/GPT-5)_

## [SUMMARY]

- 已納入「完成就移除」：W7.3～W7.5 全部通過後，會刪除已變空的 `TODO.win.md` 並同步必要連結。
- 目前仍受阻，不能先刪：三項都缺真實互動桌面證據。
- Windows Release build 通過，0 warnings/errors；66 tests passed。
- 原生 Computer Use 無法連線：`Trusted RPC service is not configured: sky`。

## [FINAL REPORT]

### 原始要求：`godev`／「完成 windows TODO」／「完成就移除」

結果：移除條件已記錄，但 Windows TODO 尚未完成，因此 `TODO.win.md` 仍保留。

- 目前只剩 W7.3、W7.4、W7.5：實際 IME＋輔助工具、Windows 高對比＋150%／200% DPI、原生 picker／檔案總管 reveal／工作列與 exe 圖示。
- 我已用目前 HEAD 重建 Release。Build 為 0 warnings、0 errors；66 個 Windows tests 全部通過。
- 環境證據：2560×1440、AppliedDPI=96（100%）、HighContrast=false，已安裝 `zh-Hant-TW` Microsoft Bopomofo IME。
- 最新 PicLens 已用隔離 profile 與兩張 fixture 圖啟動成功，未接觸使用者圖片；測試 process 已停止。
- Computer Use runtime 沒有 native `getApp`。依 computer-use skill 載入 `@oai/sky` 後，服務明確回覆 `Trusted RPC service is not configured: sky`。
- 因此目前不能可信地送出 Bopomofo composition、切換顯示設定後觀察 renderer，或點擊 picker/reveal 並觀察工作列。
- 產品程式碼、`TODO.win.md`、系統 DPI 與高對比設定都沒有改動。
- 紀錄 commits：`7d58b208`、`cbb33d80`。獨立 narrow cross-check 的 Outcome、Minimality、Conformance 都是 PASS；Host gate PASS。這只證明受阻紀錄誠實，不表示 W7.3～W7.5 完成。

Cross-check review: `.agentflow/artifacts/A-002-windows-todo/cross-check-report.md`
Cross-check implementation: cbb33d80c4b3a40a8ad2ea4628969c85cd3ea9d2

## Questions (batched — each with a suggested default)

- 請啟用這個任務的原生 Computer Use（trusted `sky` RPC）後回覆「繼續」；若無法啟用，要改由你手動跑三項互動驗收嗎？
- Suggested default: 啟用 native Computer Use 後回覆「繼續」，我會完成 W7.3～W7.5，接著移除 `TODO.win.md`、更新連結、驗證並提交。
- ans:


---

# → Ask / A-003

godev
完成 Arch TODO

## [RUN-001] Event — Arch intake and verification route (during round A-003)

- 2026-09-12 Asia/Taipei。Owner Ask 已逐字寫入；HEAD `743199b619be46971323eb797acd89f5294e0e1c`、main、初始工作樹乾淨。
- 初次 intake 使用錯誤 notebook 路徑失敗；改用設定中的 `.agentflow/devlog.md` 後成功，不重做初始化。
- Route: selected_advisors。allow-ag: on。先由 requirements/codewalk advisor 回答每個 Arch TODO 的實作、測試缺口與可驗收範圍；coordinator 獨立執行現有建置、測試與隔離診斷。尚未選定需 Design Go 的 consequential source change。
- 本機為 Omarchy 4.0.3（Arch 衍生）、Hyprland Wayland、Qt 6.11.2；不是 KDE Plasma、原生 X11 或乾淨 OS。zhtw-mcp 不可用，使用已知台灣用詞。
- Release validation 已開始，session 47166，證據 `/tmp/piclens-arch-validation.IHj5Y4Ng`。

## [RUN-002] Event — audit dispatch (during round A-003)

- 2026-09-12 Asia/Taipei。啟動 external-runner-v1 requirements/codewalk audit，immutable brief `.agentflow/artifacts/A-003-arch-todo/audit-brief.md`。
- Profile codex-default、better、gpt-5.6-sol/low；cli-provider off 只用 Codex。codex executable 可用；登入狀態待 runner 實證。
- 獨立 no-remote clone `/tmp/piclens-arch-audit/clone`；唯一允許寫入 `arch-audit-report.md`。stdin closed、診斷最多 4096 bytes，不設工作逾時。clone 不等同 OS confinement，credentials/network/provider cancellation 未證明。

## [RUN-003] Event — baseline and Wayland evidence (during round A-003)

- 2026-09-12 22:58 Asia/Taipei。Release `/tmp/piclens-arch-validation.IHj5Y4Ng`、Debug `/tmp/piclens-arch-debug` 各四組 CTest 全通過（17.88／18.40 秒）；DESTDIR 安裝、desktop-file-validate、AppStream 驗證通過（兩項 advisory）。
- 第一個 native smoke exit 134；coredump 顯示 QGuiApplication 平台初始化 fatal，無記憶體不足證據。允許同一隔離程序連線桌面後 smoke exit 0，platform=wayland，正常關閉及背景回收已記錄 `/tmp/piclens-arch-validation.U1VwtjWA`。
- audit 首次於模型啟動前因 Codex runtime readonly 初始化失敗，clone 無修改；相同 brief 以 runtime 權限重試，session 58763。
- 10000 診斷項目真實 OpenGL renderer 啟動/截圖 exit 0；GPU Intel UHD 620 / Mesa 26.2.2，僅屬合成 model 診斷。該次只覆寫 data-root，Qt pipeline cache 仍寫預設 XDG cache；後續診斷補齊 XDG 隔離，不將該次當完全隔離快取量測。
- Tracker 已驗證；實際變更只有 `.agentflow/devlog.md` 與 A-003 工作記錄，符合預期範圍；產品程式碼未修改。

## [WIP-001] Checkpoint (during round A-003)

- **Finished:** Debug/Release 建置、各四組 CTest、暫存安裝與 Wayland 空視窗啟動關閉通過。
- **Running now:** 外部 audit 正逐項核對 Arch TODO；coordinator 驗證 10000 項目及 Viewer 原生畫面。
- **Still to do:** 補足可執行驗收與文件；有實作缺口時提出最小修改；KDE、原生 X11、乾淨 OS 與安裝生命週期尚無證據。
- **Next work action:** 收取 audit，檢查 renderer 截圖與 metrics，再依證據更新待辦。
- **Checks:** [x] tracker.md | [x] devlog RUN | [x] scope matches tracker

## [RUN-004] Event — audit accepted and bounded implementation (during round A-003)

- 2026-09-12 23:01 Asia/Taipei。外部 audit 第二次 exit 0、僅新增 arch-audit-report.md；coordinator 讀取完整報告並核對來源。Raw SHA256 ec8540225db4a315895e609c133ca795fbfec5ef121447f4280bb42d3d6673a2。報告錯用未來時間與開頭格式，保留 raw；canonical 僅機械修正第一段 stamp/heading，實質內容不變，不為此重派 reviewer。
- 路線重新判定：direct bounded source/test work。Owner「完成 Arch TODO」涵蓋測試與已確認修正。Advisor 將所有可觀察行為改變稱 consequential 並非 host 決策；start() 無效路徑防護是局部可逆修正、不寫入/遷移持久資料、不跨新增信任邊界，依 direct route 無需另問 Design Go。
- 實作拆為兩個獨立 clone：core worker 只補核心測試/共用 fixture 與 start() 無效路徑防護；QML worker 只加真實 QML integration suite/CMake test target。兩者 brief 已凍結，均 codex-default/basic/gpt-5.6-luna/xhigh，external-runner-v1。寫入範圍彼此不重疊，host 驗證/import 後整合。
- 現有 A-B-A cold/warm 各7次完整原圖提交，0 unpainted；cold 58–186ms，warm 62–181ms。這只是合成兩圖診斷，不能宣稱代表性圖庫 PERF-01 完成。

## [RUN-005] Event — implementation transport recovery (during round A-003)

- 2026-09-12 23:09 Asia/Taipei。兩個實作共用的 parent runner session 30237 意外 exit 143，未寫 runner result；原因未確認，不能聲稱任務完成。
- core clone 僅修改 start()/controller tests；23:06 focused controller log 7 passed。Core 程序已不存在，尚無報告。
- QML 程序 PID 20246 仍活著且 cwd/argv 符合本次 clone，尚無 tracked edits；因 parent/transport 已遺失，以精確 process group TERM 結束後保留 clone。這是已證實 transport failure 恢復，不是因安靜或執行時間而重派。
- Recovery briefs 保留原範圍，加上禁止 broad pkill/killall；將各自使用獨立 runner 和 signal 記錄續作。每個 stage 計第二次模型啟動，尚未達三次上限。
- 新增 host 證據：gio trash 對隔離特殊字元 PNG 成功，來源消失且隔離 trash 內容一致；透明 PNG grid/Viewer alpha 各像素吻合預期。

## [RUN-006] Event — recovered dispatch and documentation (during round A-003)

- 2026-09-12 23:11 Asia/Taipei。更正 RUN-005：準備送出 QML group TERM 時程序已退出（No such process），本次 kill 未送達；clone 沒有 QML 修改或報告。原因仍未知。
- core session 92735、qml session 75402 以各自 runner 恢復；各保留原本 scope。後續定期讀取 runner session 避免長時間不收取輸出。
- 平台文件/元件英文 Close 修正以第三份 immutable brief 獨立派送，僅 README/testing guide 與 ComponentPanel label，不修改其他 runtime 行為。profile/basic/model/effort 同 core worker。
- 已確認 Docker API 權限不足；sudo -n 唯讀查詢回需密碼。已向 owner 詢問現成 KDE/nativeX11/cleanArch 可用環境，其餘工作持續。

## [RUN-007] Event — native and package proof retained (during round A-003)

- 2026-09-12 23:13 Asia/Taipei。A-003/evidence 保存環境、baseline Release CTest、DESTDIR、Wayland app log、CLI PTY、10000 synthetic metrics、cold/warm Viewer、六圖 manifest、原生 alpha 混合與實際 gio trash 證據。
- 一般帳號 makepkg 完整 build/check/package exit0；四 suite 17.52秒。套件 b6d35e5215c9b1e7d87f62564e7ffc6bdcd3b39c16a5c6e562c58f67f2a62264。這是 baseline/development-host 產物，尚非最終 source commit 或乾淨 OS。
- pacman -Qip/-Qlp、bsdtar modes 與 app/helper ldd 已檢查；相依皆找到，所需 app/helper/desktop/icon/license 安裝路徑存在。未安裝至系統、未公開發布。

## [RUN-008] Event — implementation scope checkpoint (during round A-003)

- 2026-09-12 23:14 Asia/Taipei。T-1逐項盤點完成，T-2/T-3持續；tracker 已驗證。
- 主工作區實際仍只變更 notebook/artifacts；core clone 只變更已授權 controller.cpp/controller_test.cpp；QML/docs 尚在處理。預期範圍已列 CMake/QML tests、fixtures、平台 README 與 ComponentPanel 單一標籤，無其他來源變更。
- 目前 handles：core 92735、QML 75402、docs 99365；初始實作 transport 中斷後已恢復。原生 GUI、gio、封裝的 baseline 證據持續保留，不能替代整合後重測或缺少的 KDE/nativeX11/cleanOS 證據。

## [WIP-002] Checkpoint (during round A-003)

- **Finished:** 全部 Arch TODO 的 source/test 缺口盤點；Debug/Release 四 suite、Wayland 空視窗、10000 synthetic model、六格式縮圖、Viewer A-B-A/alpha、實際隔離 gio trash、一般 makepkg 均通過 baseline。
- **Running now:** core worker 補驗收測試與無效啟動資料夾修正；QML worker 建立 integration suite；docs worker 補 CLI/metrics 文件與元件關閉標籤。
- **Still to do:** 獨立檢查 clone、整合與測試、按證據更新 TODO、最終 cross-check、Git 提交推送；KDE/native X11/clean Arch/安裝生命週期仍需環境。
- **Next work action:** 收取三份實作報告並核對允許寫入清單，對交付內容跑主工作區測試。
- **Checks:** [x] tracker.md | [x] devlog RUN | [x] scope matches tracker

## [RUN-009] Event — tracker proof correction (during round A-003)

- 2026-09-12 23:15:31 Asia/Taipei。RUN-008/WIP-002 寫入前的 tracker 驗證實際失敗，原因 T-1 完成列缺 Proof: 欄位；先前「已驗證」敘述不準確。
- 已補 audit-report.md 證据並更正 Operation running=yes；本次 tracker-contract 驗證 PASS。T-1 完成事實與工作範圍不變，沒有重跑產品測試。

完成的項目記得移除
都完成的話檔案就可以移除了

## [RUN-010] Event — owner clarified TODO removal (during round A-003)

- 2026-09-12 23:21:03 Asia/Taipei。Owner 補充已逐字寫入當前 Ask：完成的項目記得移除／都完成的話檔案就可以移除了。
- T-3 已納入移除條件：有對應證據的項目逐項移出；全部驗證通過才刪除 TODO.arch.md 並更新相關引用。未驗證項目繼續保留。
- Tracker scope change 首次漏 Source:/Effect: 格式而失敗，補齊後驗證 PASS。三個 workers 持續原本範圍。

## [RUN-011] Event — documentation and native label accepted (during round A-003)

- 2026-09-12 23:28:48 Asia/Taipei。docs external-runner exit0，clone 只修改 README/testing guide/ComponentPanel 與報告；host 已讀報告與完整 diff。
- Host 匯入指定三檔，恢復 diff 中不必要移除的既有 Windows presets 與 validate.sh 入口；將 worker 已撰寫的主機基準事實移入 README。未納入未查證的 worker build 數字作 host 證據。
- 主工作區 Release 增量建置 exit0；原生 Wayland 深色元件截圖 exit0，已肉眼核對「關閉」按鈕。git diff --check 通過。
- Core/QML 擴充測試仍在執行，尚無完成報告；TODO 尚未移除，以整合後證據判定。

## [RUN-012] Event — diagnostic contract completion (during round A-003)

- 2026-09-12 Asia/Taipei。文件與 ComponentPanel 已提交 87f2666。原有 Git hook 指向不存在的 .agents 路徑，已備份至 /tmp 並只修正同一 devlog-guard.js 路徑；保留 guard 後 commit 成功。
- Owner「完成 Arch TODO」及 A8.1/效能文件要求仍有可執行程式缺口：preview-ready、first-thumbnail 與 CPU/RSS/成功-max-target-miss 診斷。派出獨立 bounded metrics worker；不因僅文件化缺漏就將 A8.1 視為完成。
- 最小設計：使用既有 lifetime_/viewer clock 與 paints_，摘要直接推導；preview 時間需在現有結果交付點採樣，外部工具無法還原；Linux self 資源快照明列單位、origin、未涵蓋 GPU/child 同時配置。拒絕新增通用 telemetry framework/新 CLI flags/排程器。
- Route: direct，新增觀測欄位不更改既有欄位語意、持久資料或檔案操作。Immutable brief 已記錄各概念必要性與較小替代；model basic/gpt-5.6-luna/xhigh。與 core start()、QML tests 採不同修改區段，host 以 diff 整合。

## [RUN-013] Event — core inspection and diagnostic scope checkpoint (during round A-003)

- 2026-09-12 23:39 Asia/Taipei。Core runner 第二次正常 exit0，僅六個允許檔案及報告；host 讀完整 diff。動畫 WebP 已是含 ANMF 的有效雙影格，另以 QImageReader 支援動畫及兩影格驗證。
- Host 拒絕虛構路徑的 fixtureManifest 與只檢查該 metadata 的測試；atomic failure 測試目前查無關 profile，不能證明原子替換保留舊資料。第三次 bounded correction brief 已凍結，只修正這兩類證據，不新增產品功能。
- Tracker 已加入 metrics controller.h/main.cpp 邊界並驗證 PASS。Live source diff 仍只有 notebook；docs 已提交 87f2666。Core clone 只在原允許六檔，QML 只在 CMake/test，metrics 按獨立 diagnostic 範圍進行。

## [WIP-003] Checkpoint (during round A-003)

- **Finished:** 平台文件與繁中關閉按鈕已提交、實機畫面通過；Core 首版四組測試通過，host 已完成逐檔檢查。
- **Running now:** Core 修正兩處無效驗證；QML 操作測試與效能診斷欄位仍在各自 clone 實作。
- **Still to do:** 整合後 Debug/Release 與原生診斷、依證據移除 TODO、最終 cross-check、Git 提交推送；外部桌面及乾淨 OS 驗收仍缺環境。
- **Next work action:** 驗證修正版 core，收取 QML/metrics 報告並合併不重疊區段。
- **Checks:** [x] tracker.md | [x] devlog RUN | [x] scope matches tracker

## [RUN-014] Event — QML accepted and native acceptance continued (during round A-003)

- QML delivered clone complete suite independently passed 5/5, 21.01 秒；host 逐檔檢查後匯入 CMake/qml_test，提交 7b1ec0e。沒有修改 production QML。
- 原生 Wayland 同一 test binary 的 drag-cancel/Viewer PASS，第一個 Ctrl+F focus assertion FAIL；harness 尚未等待 window expose/activation，原因仍待證明。QML 第三次 correction 只處理 test 等待與 threshold/cancel 證據，禁止放寬 assertions 或修改產品。
- Host 對未改動 Imaging backend 補具體觀測：既有 shutdown/timeout/quiesce 三項測試 exit0，17 個觀察到的 helper 全部已退出，三個 imaging 暫存目錄全部清除。另以實際 Imaging API + 暫存 wrapper 同時排同 identity 两請求及正常圖片：僅一個 stalled helper，正常圖片先完成，兩個 stalled 結果逾時後終止/回收，無暫存殘留。evidence/mixed-imaging.json。
- Host 使用同一 FileOperations backend 的可丟棄素材核對 case-only rename、分解 Unicode 名稱、來源消失、權限失败保留來源、實際 gio cleanup 保留 JPG/JPEG/WebP；全部 exit0。所有 trash 導向隔離 XDG data，未接觸個人圖庫。
- Tracker 驗證 PASS，active handles core 84826/QML 21799/metrics 4687；新增命令驗證素材與結果只在 A-003/evidence，範圍不變。

## [RUN-015] Event — core and native input accepted (during round A-003)

- Core 第三次 correction exit0，host 比對全部七檔/README 與完整報告；獨立 CTest 4/4 PASS，38.87 秒。匯入後提交 a6d2158，原子寫入失敗現在檢查同一設定檔原始 bytes。
- QML 第三次 correction exit0；host native Wayland 三個 journeys 全通過（搜尋/右鍵、拖曳門檻與 ungrab、Viewer），2.272 秒。初次搜尋 failure 在等待 exposure/activation 後消失，屬 harness 輸入時序缺口。Host 已匯入修正版，offscreen 完整 suite 複核中。Report 說未再直接呼叫 endDrag 過廣：首段取消已用 Qt ungrab，autoscroll 尾段仍呼叫 endDrag；host 只接受實際 ungrab 那段證據。
- Host 原生 ImageItem pixel probe：游標定點前後像素 #825964 一致，平移像素隨游標移動，1.2倍/reset通過，清除畫面後 weak Frame 已釋放。evidence/render-anchor.json。
- Controller 備援/預載暫存 probe 確認 preview失敗仍載原圖、原圖失敗保留preview、目前原圖後下一/上一preview順序、權限失敗保留來源與12000ms toast；active batch shutdown 部分 probe 尚未進入encoder，不能聲稱通過，正在排除驗證時序。
- Tracker 驗證 PASS；actual changes 只有 qml_test 與 notebook/artifacts，metrics clone 只修改 frozen diagnostic path，範圍符合 tracker。

## [WIP-004] Checkpoint (during round A-003)

- **Finished:** 啟動修正與核心驗收測試已提交；原生 Wayland 搜尋、拖曳取消、Viewer、定點縮放與平移通過。
- **Running now:** Metrics worker 補預覽/繪製與 CPU/RSS 欄位；host 核對批次關閉與整合測試。
- **Still to do:** 完整整合後 Debug/Release/CLI、移除已完成 TODO、最終 cross-check 與提交推送；KDE/native X11/clean OS/安裝生命週期仍無環境。
- **Next work action:** 收取診斷實作並驗證欄位語意，完成剩餘本機驗收記錄。
- **Checks:** [x] tracker.md | [x] devlog RUN | [x] scope matches tracker

## [RUN-016] Event — final acceptance gaps and scoped corrections (during round A-003)

- 2026-09-13 Asia/Taipei。TODO disposition worker 已完成，尚待 host 核對；唯一仍跑的原 worker 為 metrics4687（已產報告/Debug及Release測試，但執行程序尚存活，未收取完成結果）。
- Metrics 首版 source inspection 發現 legacy scan/search由0改null，與既有schema1相容要求衝突；補正 brief 已凍結，恢复舊欄位語意，移除未要求且重複的固定GPU/像素界限欄位，補performance要求的lastCompletedBatch counts/duration。最小額外概念是一個batch timer和上次完成結果，不改檔案操作或排程；等待首版結束後才派第二次。
- 與產品規格逐節核對確認新缺口：搜尋欄有符合結果時沒有欄內清除按鈕。A3.3/A8.4與product-spec.md明確要求此入口；既有空狀態清除按鈕不能滿足。新的獨立小修正階段只允許Main.qml搜尋TextField與同一QML輸入測試，不重開已完成core/QML階段；direct local reversible UI route，不改持久資料或信任邊界。
- Host controller probe 等待上次refresh完成後重跑全部PASS：activeencoder存在、shutdown37ms回收、原檔保留且零暫存/輸出。第一個probe未進入encoder不列完成證據。QML最終offscreen host5/5 21.38秒後提交5958251。Tracker scope/paths驗證PASS。

## [RUN-017] Event — TODO dispositions, search fix and actual drop gap (during round A-003)

- TODO worker正常exit0；host讀完report/三檔diff，初步移除38項保留15項；原生KDE/X11/cleanOS/安裝及完整DPI缺口保留。Host機械修正把3個journeys寫成5個journeys的數量描述（含init/cleanup才5tests），移除不必要法律意見免責句。文件尚未最終提交。
- Searchclearworker正常exit0，只有Main.qml與qml_test允許路徑；host完整suite5/5 PASS42.20秒，原生Wayland真實輸入/點清除/CtrlF PASS。匯入並提交；report漏最後Self-check，保存raw後只補格式，不重派付費worker。
- Host在offscreen及原生Wayland驗證樹root與後代展開收合、樹/卡片導覽、側鍵前後、F5與快速切換清選取、picker持久路徑不變，全數PASS，evidence/navigation-native.json。暫存probe初版用QObjectownership找不到虛擬化樹delegate，改為visual tree尋找後才取得有效操作證據。
- 同一原生probe的實際拖放仍target未highlight/不開confirm；拖曳取消通過不能代替DropArea.onDropped。獨立drag-drop阶段只在Gallery.qml/既有QMLdragtest查明輸入或產品原因，明確禁止用controller.dropRename替代實際drop。尚未判定runtime根因。此為首次檢查已重現的actualdrop缺口，不重派先前已完成test-only階段。
- Metrics首次已正常結束；已依凍結相容性/批次量測brief啟動第二次30634。遠端fetch成功，HEAD..origin/main沒有新提交，origin/main仍743199b；尚未推送。
- Tracker包含Gallery.qml與驗證文件實際路徑並PASS；本機未提交檔案僅TODO/驗證文件/第三方清單及notebook，符合範圍。

## [WIP-005] Checkpoint (during round A-003)

- **Finished:** 38項TODO已有移除草稿；搜尋欄清除按鈕與真實點擊測試完成；原生樹狀導覽、側鍵、F5與持久路徑驗收通過。
- **Running now:** Metrics相容性/批次觀測修正、actualDropArea拖放缺口診斷；host整理最終整合證據。
- **Still to do:** 整合後Debug/Release、nativeCLI/10000CPU-RSS、最終文件、cross-check與提交推送；缺少原生環境的待辦繼續保留。
- **Next work action:** 收取兩份交付並核對最小diff，再跑完整整合suite。
- **Checks:** [x] tracker.md | [x] devlog RUN | [x] scope matches tracker

continue

## [RUN-018] Event — interrupted process recovery (during round A-003)

- Owner continue 已逐字納入 Ask。中斷前四個 session 全部回報 Unknown process id，ps 無 worker/build/extract 程序；沒有把缺失輸出當成成功。
- Metrics 第二版已交付，host 完整五 suite 19.52 秒 PASS；匯入診斷欄位並保留啟動修正。Debug/Release 被中斷的 build 從同一 build tree 接續。
- Drag/drop 第一個 start 未交付報告；保留 clone diff，以第二個 frozen recovery brief 接續。此 stage 總上限三次。
- 官方 Arch bootstrap SHA256 符合公布值，使用本機 archlinux-keyring 解碼後 gpgv Good signature；首次直接读取 armored keyring 失敗未解壓。現在恢復已驗證 archive 解壓，rootless 建置仍未通過，不能移除 TODO。
- streams 規則已重讀；所有 dirty paths 都是本 Ask 的未提交工作，沒有外來工作或另開 stream 需求。

## [RUN-019] Event — integrated build and portable evidence (during round A-003)

- Debug/Release 中斷恢復後 build 均 exit0；尚待拖放修正及 metrics test 補正後的最終 suite。
- Host 發現 metrics test 非 Linux 分支把跨平台 timestamp 預期為 null，另正常 fixture 測試把 <500ms 當正確性假設；第三次 metrics start 僅修這兩項測試，禁止 runtime 新概念。
- bootstrap archive 含兩個根層 metadata 檔；嚴格路徑白名單補此確定格式。首次bsdtar因caps及唯讀目錄失敗；GNU tar延後還原目錄權限成功，未還原不需使用的 host capabilities。rootless pacman key初始化/信任庫PASS；下載要求chown未映射alpm UID而失敗，正檢查僅本隔離環境的下載帳號設定，簽章驗證維持啟用。
- Tracker 驗證 PASS，實際路徑包含既定 CMake/controller/main/metrics test、TODO/平台 docs 及 notebook；未變更 host 系統套件或個人設定。

## [WIP-006] Checkpoint (during round A-003)

- **Finished:** Debug/Release 重新建置成功；搜尋清除及樹狀導覽驗證已完成；官方 bootstrap 簽章與解壓成功。
- **Running now:** 實際拖放修正、metrics 跨平台測試補正、隔離 Arch 套件環境建置。
- **Still to do:** 最終整合測試與效能數據、更新 TODO、獨立 cross-check、提交推送；缺少平台的驗收仍保留。
- **Next work action:** 收取修正並完成 native 驗證，繼續乾淨環境封裝。
- **Checks:** [x] tracker.md | [x] devlog RUN | [x] scope matches tracker

## [RUN-020] Event — diagnostics and native sizing verified (during round A-003)

- Metrics portable test host9/9 PASS 1792ms，匯入後提交db76c15；worker timestamp格式只做機械轉換保留raw，無重派。依賴清單提交6ae9760。
- 真實PTY八案例PASS，包含/dev/full exit3、metrics JSON self/null/legacy0欄位與無效啟動資料夾log；evidence/cli-final-pty.json。
- 同一Release controller/MainQML做10000model、實際搜尋11結果、五次gallery捲動PASS：load64ms/search76ms/maxMaterialized38/maxVisible13/selfCPU872ms/RSS180011008/peak187039744 bytes/shutdown0ms；完整wait4程序4.77秒。synthetic model沒有解碼，非代表性圖庫。
- Hyprland0.56.2依最新官方dispatcher規格，只對本次測試PID使用float/resize：1600x1000與800x600，150% 800x600 logical→1200x900，exit0。host檢視三張真實Qt截圖，工具列/側欄/格狀比例正常。沒有修改桌面設定檔。
- rootless Arch pacman -Syu安裝必要工具鏈exit0，簽章驗證啟用；單UIDnamespace無法切alpm帳號，只在隔離pacman.conf省略DownloadUser。uid1000 builder與makepkg版本驗證PASS；尚未建置套件。
- Tracker PASS，source已提交；當前live diff僅TODO/驗證文件/notebook與A003證據，範圍一致。Drag recovery仍正常執行；Reveal對可丟棄特殊字元資料夾做native驗證中。

## [WIP-007] Checkpoint (during round A-003)

- **Finished:** 診斷功能已提交；8個PTY案例、10000筆搜尋捲動/CPU/RSS/shutdown、浮動視窗1600及800尺寸與150%畫面通過；乾淨Arch工具鏈準備完成。
- **Running now:** 實際拖放修正、原生Reveal操作驗證。
- **Still to do:** 最終Debug/Release與乾淨封裝、依據證據刪除TODO項目、獨立cross-check、提交推送；原生KDE/X11與代表性圖庫仍缺環境/資料。
- **Next work action:** 收取拖放報告並完成整合驗證，接著封裝最終source。
- **Checks:** [x] tracker.md | [x] devlog RUN | [x] scope matches tracker

## [RUN-021] Event — drag accepted and final suites green (during round A-003)

- Drag recovery exit0；host逐檔核對允許Gallery.qml/qml_test及report。最小產品修正：preventStealing與Drag.source改為ghost；117行真實Qt滑鼠/DropArea regression保留檔案bytes與取消證據。已匯入並提交27464f3。
- Host整合後Debug CTest6/6 PASS43.81秒、Release6/6 PASS42.61秒。原生獨立Controller/QML probe同一三圖片journey現在目標highlight、確認、多張最小序號與取消保留全部PASS，連同navigation、fallback/預載與encoder shutdown26ms均PASS。
- Worker新drop固定900x700布局case直接在Hyprland平鋪執行，target hint通過但confirmOpen失敗；將同一test PID浮動至其預期900x700後PASS。未改測試assertion，未把native平鋪失敗藏掉；evidence/drag-native-first.txt與drag-native-floating.txt。Worker以contentY140解釋原始host probe過廣：該數值是QMLfixture，不是host三圖probe；host只接受兩個已證明source修正及最終native實證。
- Reveal實际Nautilus開啟含臺灣/空白/;$()的資料夾，新window已核對並只關閉該window；offscreen開啟失敗保留選取/資料夾與log PASS。evidence/reveal-native.json。
- 最終runtime/test commit27464f3已freeze working-tree handoff，開始單UIDrootless Arch makepkg。工具鏈之外無host Qt plugin/config/cache掛載；無主機系統安裝。

## [RUN-022] Event — clean Arch package and lifecycle accepted (during round A-003)

- Rootless官方Arch userland以uid1000 builder makepkg exit0，6/6 CTest42.85秒，source5868e13e…、package593e3431…；runtime/test/packaging bytes已逐檔對照目前27464f3與SOURCE-MANIFEST一致。無host libs/plugins/cache/home掛載，build/check/package network disabled。evidence/clean-arch.json。
- 私有rootfs安裝4.0.0-1後，gio launch已安裝desktop entry，僅bind Wayland socket；host看到piclens新視窗、log恢復舊格式JSON資料夾並正常關閉。未掛GPU裝置，Mesa warnings保留，不列GPU效能證據。
- makepkg --repackage建立僅本地驗證的pkgrel2，repo版本維持1；pacman實際升級4.0.0-1→4.0.0-2。升級後smoke1item/1thumbnail；pacman-R移除app/helper/desktop/license。舊格式JSON安裝、啟動、升級、移除全程bytes相同hash2fb6b79a…；沒有主機系統安裝或公開發布。
- 最終nativeViewer direct1paint216ms，cold/warm各7paints最大336/269ms，0miss/0unpainted；仍是小型syntheticfixture，不移除代表性圖庫A8.2。
- T-2完成，tracker PASS。最終TODO/docs第二start67535正在依完整證據移除候選48項、預計保留5項；實際數量以交付與獨立review為準。source已提交，live變更僅已授權TODO/docs/notebook/證據，範圍一致。

## [RUN-023] Event — final disposition committed and review frozen (during round A-003)

- TODO finalization第二start正常exit0，僅三個允許檔案；host讀完整交付，候選10项均有證據，加前38項共48，剩5。已提交9b7ee941ef75be673397f7c3f05bf02a87945815。
- 匯入時拒絕新增OS-cache清除限制，A8.2恢復原始條款；A2.2保留操作證據缺口而非暗示入口尚未實作。A6.7句子分開三PNG native probe與QtTest其他coverage；shutdown明示另有probe/log而非不存在的metrics欄位。屬對原要求與已知證據的精確對照，無擴大scope。
- Full cross-check facts20檔1957行已freeze，behavior=true/broad=true/trust_boundary=false/consequential=false，plan full；原始Ask與三次介面/diagnostic修正必要性在brief，better/gpt-5.6-sol/low第1start59961獨立clone執行，只准report/buildoutputs。
- 已驗證的4.0.0-1套件、sourcearchive、PKGBUILD、SOURCE-MANIFEST與SHA256SUMS複製至忽略的dist/arch-validation-A003供查閱；所有checksum與clean結果一致，未包含升級測試用pkgrel2。

## [RUN-024] Event — full review found one documentation contract defect (during round A-003)

- 2026-09-13 01:21:12 Asia/Taipei。第1start59961正常exit0；獨立clone只新增report，完整6CTest43.46秒、focused QML4項603ms、metrics9項1757ms均PASS。完整報告保存cross-check-first-report.md，SHA25649f70fd140f3a86371236467239e5a485824489fc8efb8f5ed585e84beee4d09。
- Verdict/Outcome/Conformance BLOCKING，唯一問題README136仍說短寫入不回錯誤；host核對main.cpp53與metrics_test285確認矛盾。Owner「完成 Arch TODO」的A0.4/A0.5文件契約要求此修正，直接將該句改成短寫入與flush失敗皆3；同時將新增驗證紀錄的Markdown行尾空白換成空白段落。無runtime/test變更。
- 同一review stage第2start將只複核文件差異與既有完整review證據，仍用better/gpt-5.6-sol/low；不重跑未變更的產品測試。尚無Host gate PASS。原report提OS page cache只作已知量測範圍，不接受它成為A8.2新增要求。
- 遲補WIP涵蓋01:03至本次的文件收取、套件交付與review期間。Tracker驗證PASS；實際變更僅已授權README/validation文件、notebook與bounded證據，沒有rootfs/dist加入Git。

## [WIP-008] Checkpoint (during round A-003)

- **Finished:** 48項完成並移除；Debug、Release、乾淨Arch與獨立review的6組測試皆通過；套件安裝、升級、移除與profile保留已驗證。
- **Running now:** 修正獨立review指出的README錯誤契約，準備對文件修正做窄範圍複核。
- **Still to do:** 接受最終review、Host gate與Git提交推送；5項缺桌面環境或代表性素材的TODO繼續保留。
- **Next work action:** 提交兩份文件的修正，派出同一review stage第2start。
- **Checks:** [x] tracker.md | [x] devlog RUN | [x] scope matches tracker

## [RUN-025] Event — documentation correction verification started (during round A-003)

- 2026-09-13 01:22:33 Asia/Taipei。文件修正提交3d486321151e654de43dab62793a938cd435721e，相對9b7僅README一行及validation兩處段落換行；git diff --check PASS。
- 同一cross-check第2start73248，獨立clone /tmp/piclens-arch-crosscheck-correction/clone、profile/model/effort不變；frozen correction facts2檔8行、plan narrow，唯一允許report。既有full review與相同runtime/test的6CTest證據保留並直接供複核。
- Host讀取完整LastTest.log中六項PASS，保存evidence/cross-check-ctest.txt；第一clone status僅未追蹤report。最新fetch成功，HEAD..origin/main空；尚未推送。

## [RUN-026] Event — final independent review and host gate accepted (during round A-003)

- 2026-09-13 01:22:55 Asia/Taipei。Cross-check第2start73248正常exit0；clone僅新增允許report，無存活process group，報告SHA256 4d3846852c74bbdcb2e67d6c06d5199e5a38849de7c459be32b362d6d669f76f。
- 最終implementation3d486321151e654de43dab62793a938cd435721e，Verdict、Outcome、Minimality、Conformance各唯一PASS。Host讀完整報告、main.cpp53/README136與metrics test契約、獨立完整LastTest.log、clone diff與runner證據；相對fullreview9b7 runtime/test差異確實空，只有2份文件8行修正。
- Host gate: PASS。48項移除、5項保留仍為正確且有限制的交付，不聲稱全部53完成。OS-cache清除沒有加入A8.2條件。後續僅notebook/STATUS/tracker/證據收尾，不重跑產品測試或審查。

## [RUN-027] Event — implementation pushed and delivery scope reconciled (during round A-003)

- 2026-09-13 01:23:37 Asia/Taipei。第一次push因全域credential helper指向已移除gh2.98.0而失敗；目前gh2.100.0與既有keyring登入有效。只對本次git命令覆寫helper路徑，沒有修改全域設定或要求重新登入。第二次push exit0，main743199b→3d486321，HEAD與origin/main相同。
- 本輪3項執行工作完成，原始53項驗收仍有5項缺平台或素材證據，已保留TODO；不以完成本輪記錄宣稱全部原始目標完成。所有worker已結束，產品與文件全部提交推送，剩notebook/STATUS/tracker/證據的本輪記錄提交。
- Source/runtime/tests自完整6CTest與review後未變，最終文件差異已通過第2start窄範圍review及Host gate。實際待提交檔案只在.agentflow，無foreign changes，無rootfs/dist套件快取進入Git。

# ← Reply / A-003

* _2026-09-13 01:24:25 (Codex/GPT-6)_

## [SUMMARY]

- 結果有限制：原始 53 項已完成並移除 48 項，5 項仍缺指定平台或素材證據，保留 `TODO.arch.md`。
- 已修正無效啟動資料夾、搜尋欄清除按鈕與實際拖放，補齊診斷欄位、CLI 文件及核心／QML／metrics 測試。
- 最終 Debug、Release、隔離乾淨 Arch 各 6/6 CTest 通過；套件安裝、升級、解除安裝及舊設定保留通過。
- 獨立完整審查找到一處 README 契約錯誤，修正後窄範圍複核通過；Host gate: PASS。
- 實作與文件 `3d486321151e654de43dab62793a938cd435721e` 已推送到 `origin/main`。驗證紀錄與 notebook 正完成最後記錄提交。

## [FINAL REPORT]

### `godev`／完成 Arch TODO

結果：本輪可執行工作已完成；原始 Arch TODO 尚有 5 項未完成，不能宣稱全部驗收通過。

- 啟動時先拒絕不存在、非資料夾或不可讀的路徑，保留原設定路徑與空狀態。搜尋欄加入清除按鈕，清除後維持焦點與 projection。拖曳以 `preventStealing` 保持 mouse grab，並以 ghost 作為 `Drag.source`，讓實際 DropArea 放下流程開啟確認視窗；取消仍保留來源檔案。
- Metrics schema v1 保留舊 scan／search 初始數值 `0`，新增欄位以 `null` 表示沒有樣本。補上首次縮圖、preview／full paint、selection／session ID、500 ms 超標與未繪製計數、批次結果與主程序 CPU／RSS。metrics 開檔、短寫入或 flush 失敗回傳 `3`，README 已對齊。
- 新增或補強真實 QML 輸入、拖放、設定原子寫入、掃描、codec、取消／競態與 metrics 測試。共用測試案例描述不使用虛構 fixture 路徑，第三方清單與測試指南已更新。

| 驗證 | 結果 |
|---|---|
| 最終主機 Debug CTest | 6/6，43.81 秒 |
| 最終主機 Release CTest | 6/6，42.61 秒 |
| 隔離乾淨 Arch makepkg／CTest | build、check、package 通過；6/6，42.85 秒 |
| 獨立完整 review 的 Debug CTest | 6/6，43.46 秒；另通過指定 QML 與 metrics 複核 |
| 真實 CLI PTY | 8 個案例通過，含輸入輸出、exit code、`/dev/full` 與成功 JSON |
| 原生 Wayland | 導覽、搜尋清除、Viewer、實際拖放取消、Reveal、100%／150% 尺寸通過 |
| 隔離套件生命週期 | 安裝 → desktop entry 啟動 → 本機測試升級 → 解除安裝；原設定 bytes 不變 |

完整結果與證據連結見 [Arch 驗證紀錄](/home/michael/Work/PicLens/docs/linux/arch-validation.md)。原生固定 900×700 拖放測試在平鋪 compositor 改成 960×1054 時失敗；只浮動該測試 PID 到預期尺寸後通過。同一動態三張 PNG 原生流程也通過，不把此結果泛稱為所有原生 QML 測試均通過。

乾淨建置採官方簽章 Arch bootstrap、rootless 單一 UID 與隔離 userland，共用主機 kernel；沒有掛入主機 library、plugin、cache、config 或 home。建置／測試／封裝停用網路。已安裝 desktop entry 的啟動只分享一個 Wayland socket，未掛 GPU 裝置。這不代表完整開機 VM、KDE 或 X11 驗證。

10,000 筆 generated model 的載入／搜尋／捲動通過，未解碼圖片；self CPU 872 ms、RSS 約 172 MiB、peak 約 178 MiB。Viewer 小型合成素材 cold／warm 各 7 次繪製，最大 336／269 ms，均無超標或未繪製樣本；這是 App cache 冷暖語意。它們不能取代代表性混合圖片圖庫，CPU／RSS 也未包含 GPU、compositor 或子程序成本。

### 完成項目移除；全部完成才移除檔案

已移除有證據的 48 項，保留以下 5 項於 [TODO.arch.md](/home/michael/Work/PicLens/TODO.arch.md)：

- A2.2：原生 folder picker 選取、取消、啟動還原與空狀態操作。
- A7.3：KDE Plasma／Wayland、繁中 IME 及桌面整合。
- A7.4：原生 X11 工作階段。
- A7.5：200% 縮放與完整 Qt accessibility／原生輔助工具。
- A8.2：代表性混合圖片圖庫的 Release 冷暖快取與原圖 500 ms 繪製目標。

上述項目仍需要相應桌面／輔助工具或代表性素材的實際證據。全部通過後才刪除檔案並同步引用，沒有把已完成清單留在 TODO 中。

### `continue`

已接續中斷工作，收取拖放與 metrics 修正，完成乾淨 Arch 封裝和生命週期測試。可安裝的 [4.0.0-1 Arch 套件](/home/michael/Work/PicLens/dist/arch-validation-A003/piclens-4.0.0-1-x86_64.pkg.tar.zst) 已備妥，旁邊保留來源快照、PKGBUILD、SOURCE-MANIFEST 與 SHA256SUMS。套件 SHA-256：`593e3431f308a34758264ddc8c9eaac06498565d1f003ff6dee5d8e9051eb602`。升級測試的 `pkgrel=2` 僅存在隔離測試環境；repository 仍為 1。沒有安裝至主機系統或發布 AUR／release。

最終實作與文件 commit 為 `3d486321151e654de43dab62793a938cd435721e`，已推送至 `origin/main`。首次推送因全域 helper 指向舊 gh 路徑失敗；使用現有 gh 與既有登入、只對該命令覆寫 helper 後成功，未改全域設定。最後僅提交 notebook、tracker 與驗證證據，不重跑未變更的產品測試。

Cross-check review: `.agentflow/artifacts/A-003-arch-todo/cross-check-report.md`
Cross-check implementation: 3d486321151e654de43dab62793a938cd435721e
Host gate: PASS

## Questions (batched — each with a suggested default)

- None.

---

# → Ask / A-004

+
