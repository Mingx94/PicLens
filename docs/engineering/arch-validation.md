# Arch Qt 版轉移驗收

狀態：Qt 實作與封裝程序已準備，版本 4.0.0，C++20，最低 Qt 6.8。Windows MSYS2 Qt 6.11.1 四個 suite 通過，真實 App 完整原圖提交已確認。目前沒有 Arch distro，**尚未取得 Arch build、CTest、makepkg、桌面或安裝生命週期證據**。TODO 可勾選有證據的實作／平台無關子項，但不得把 Windows 結果算成 Arch 驗收。

本頁配合 [TODO.arch](../../TODO.arch.md)、[產品規格](../product/product-spec.md)、[共用驗收案例](../product/acceptance.md)、[不變條件](runtime-invariants.md)與[資料延續性](data-continuity.md)。沒有觀測到的項目一律「待驗證」，不以單次啟動推定完整功能。

## 目前可用的 Windows 開發證據

以下為 2026-09-12 Windows 開發驗證。`artifacts` 與 build 紀錄不放入 source tarball，交付目錄另附證據。

| 證據 | 已確認內容 | 不能推定的結果 |
|---|---|---|
| `apps/linux/build/windows-preview/Testing/Temporary/LastTest.log` | domain、imaging、app、controller，4/4 Passed，19.54 秒；包含連續 Shift 選取及三張預覽實際配置不超過 12 MiB 回歸 | Linux 條件分支、Arch Qt 外掛／系統整合 |
| `apps/linux/tests/domain_test.cpp` | 共用設定／排序案例、搜尋、選取、檔案計畫、49/50 邊界、取消／衝突保護 | `Q_OS_LINUX` 內成功改名、提交競態、部分完成與 trash 程序案例 |
| `apps/linux/tests/imaging_test.cpp` | 六種副檔名、動畫／超限、冷暖快取、原圖分塊邊界像素、WebP 像素比對、取消／佇列／逾時／回收、原圖提交 signal | Arch codec 安裝完整性、實機 GPU／DPI、完整效能 |
| `apps/linux/tests/app_test.cpp` | profile 原子寫入／損壞隔離、掃描取消、10,000 筆單次 model reset | Linux 權限與 symlink、真實 GridView delegate 上限 |
| `apps/linux/tests/controller_test.cpp` | 導覽／root、縮圖完成、選取 Viewer 序列、搜尋清選取、50 張取消零輸出 | 原生 picker、拖放與所有 QML 輸入情境 |
| `artifacts/arch-qt-final/viewer.png`、`viewer.json` | Qt 6.11.1、Release、Windows plugin；完整原圖畫面與提交 signal | 截圖模式會主動要求暖身繪製；隱藏／遮蔽視窗的提交延遲不能當一般效能。500 ms 目標尚未驗收 |
| `artifacts/arch-qt-final/gallery.png`、`dark.png` | 1600×1000 淺色與 800×600 深色畫面、兩張 PNG 縮圖 | 完整格式／輸入／DPI 矩陣 |
| `artifacts/arch-qt-final/virtual.json` | 10,000 筆合成 model 定時跨頁捲動，最多 27 個 delegate（含 pooled）、25 個可見項目；投影 19 ms | 不解碼的合成資料；不代表萬張真實圖片的磁碟／記憶體效能 |

`artifacts/qml-native-smoke/viewer-metrics.json` 的早期紀錄為 offscreen，不能混用成 Windows 原生或 Arch 桌面證據。主 agent 已確認真 App 原圖繪製；以上另以 `platformPlugin=windows` 的提交紀錄界定可追溯範圍。

正式 no-replace 檔案提交僅支援 Linux `renameat2(RENAME_NOREPLACE)`。Windows 此路徑 fail closed，不採覆寫 fallback；因此 Windows suite 成功不能證明 Linux mutation 通過。Arch 的安全寫入、回收筒與失敗處理必須用隔離 fixture 另驗。

來源快照與真實 SHA-256 由交付目錄的 `SOURCE-MANIFEST.json`／`SHA256SUMS` 記錄。封裝腳本允許新建 `repo/dist/<handoff-name>`，並排除 dist 內容。Linux 二進位套件仍須在 Arch 建置。

預覽像素最大邊為 1022，含每邊 1 pixel 的取樣邊界後最多 1024；三張預覽實際配置合計不超過 12 MiB。像素傳輸使用本機暫存目錄；背景執行緒同步讀取傳輸檔期間可能延後逾時計時器，部署時不可把暫存目錄指向網路磁碟。

## 轉移順序與證據

1. 主 agent 完成原始碼與 CMake 安裝表整合。固定工作樹後執行 [New-Handoff.ps1](../../packaging/arch/New-Handoff.ps1)，核對 manifest。保存 source SHA-256、base commit、未提交快照說明。
2. 將交付移至乾淨 Arch x86_64。人工安裝工具，保存 `cat /etc/os-release`、`uname -a`、`pacman -Q`、CPU／GPU／RAM、Qt 版本、renderer、桌面版本與 `XDG_SESSION_TYPE`。
3. `sha256sum -c SHA256SUMS`、`makepkg --verifysource`、`makepkg`；保存完整命令、exit code、CTest 測試數與 log、成品 SHA-256。無測試、略過測試或僅 Windows 通過都不能視為此步完成。
4. 解開來源後執行 `bash packaging/arch/validate.sh build`，保存暫存安裝清單與 desktop／AppStream validator 結果。檢查 `/usr/bin/piclens` 與 `/usr/libexec/piclens/piclens-worker`、圖示、授權與 metadata；禁止 Rust、Windows DLL、開發機絕對路徑與私人 profile 混入。
5. 在 KDE Plasma／Wayland 與獨立 X11 工作階段各做隔離 smoke 與下列人工矩陣。記錄真實 Qt platform plugin；Wayland 上使用 xcb 只算 XWayland。
6. 使用者另行授權後，在可回復的 Arch VM／測試帳號執行安裝、升級、解除安裝。沒有做此步時 PACKAGE-01 與 DATA-01 的生命週期部分維持待驗證。

工具安裝與命令見 [Linux README](../../apps/linux/README.md)。`validate.sh inspect` 唯讀；`build`／`smoke` 只新建自己的 `/tmp` 目錄與資料，不自動安裝、回收、刪除或清理使用者檔案。程式測試本身仍需確認只使用合成 fixture。

## 桌面與輸入矩陣

| 環境／操作 | 檢查 | 狀態 |
|---|---|---|
| KDE Plasma／Wayland | 原生 wayland plugin、正常啟閉、picker／選單／dialog／reveal | 待驗證 |
| 真正 X11 工作階段 | xcb plugin、啟閉、鍵盤、picker、檔案管理器整合 | 待驗證 |
| 繁中 IME | 記錄 Fcitx5／IBus 與 Qt 整合套件版本；搜尋／改名的預編輯、候選視窗、送出、取消及焦點切換 | 待驗證 |
| 100%／150%／200% DPI | 每種平台記錄 compositor 縮放；跨螢幕移動、文字、圖示、圖片比例與 pointer 座標 | 待驗證 |
| 1600×1000／800×600 | 初始及最小視窗、窄工具列、長 Unicode 檔名、捲軸不遮卡片 | 待驗證 |
| 淺色／深色／輔助工具 | 繁中可讀、焦點、accessible 名稱／角色／狀態及鍵盤順序 | 待驗證 |
| 拖放與 Viewer | threshold、pointer grab、capture-lost／Escape、縮放平移不穿透 | 待驗證 |

IME 應使用測試環境已設定的輸入法；不由腳本修改全域環境變數或桌面設定。`QT_SCALE_FACTOR` 模擬不能取代 compositor 真實縮放。截圖須來自真實 App renderer；offscreen 測試不能當像素、輸入法或 accessibility 證據。

## 檔案、profile 與安裝生命週期

檔案動作僅用合成圖或授權副本，放入全新測試資料夾。`--data-root` **不會隔離 `--folder` 指向的圖片**。49／50 張、同名衝突、來源消失、取消、權限失敗與回收筒案例不可對個人圖庫執行。腳本不呼叫 `gio trash`；人工測試必須核對確認畫面與實際目的地，失敗不得改成永久刪除。XDG 覆寫不保證外部 DBus 服務與磁碟分割區回收筒隔離，回收測試優先使用可丟棄 VM／測試帳號。

profile 使用合成舊 JSON 或經授權副本，核對 numeric enum、缺欄位、未知欄位、正規化、損壞隔離、原子寫入與無權限。預設位置維持 `$XDG_DATA_HOME/PicLens` 或 `~/.local/share/PicLens`；命令列覆寫優先。不讀取或匯出個人 profile 到 repo。

安裝生命週期由使用者在測試 VM 明確執行：檢視成品後 `sudo pacman -U /absolute/path/to/actual.pkg.tar.zst`，從 desktop entry 與終端機啟動；使用有真實來源的舊套件建立 profile 副本，再安裝新套件驗證升級。沒有舊套件時，只能標示「合成設定相容性」，不能宣稱升級通過。最後依使用者授權 `sudo pacman -R piclens`，核對套件檔案移除與 profile／來源圖片仍保留；不要使用遞迴移除、清空回收筒或清除 HOME。

## 功能矩陣

下表是最低檢查摘要；各 ID 仍以共用驗收對照的完整條件為準。

| 案例 | 最低檢查 | Arch 狀態 |
|---|---|---|
| START-01 | 空狀態、picker 取消、上次路徑還原、初始視窗 | 待驗證 |
| TREE-01、NAV-01 | root 固定、後代展開、歷史／側鍵、不覆寫 picker 路徑 | 待驗證 |
| SCAN-01、SORT-01 | 遞迴、取消、權限／symlink、自然與四種排序、穩定相同值 | 待驗證 |
| SEARCH-01、GRID-01 | 記憶體搜尋、焦點、10,000 筆單次 reset、有界 delegate／捲動 | 待驗證 |
| SELECT-01、MENU-01 | Ctrl／Shift anchor、選取順序、右鍵作用範圍、排除資料夾 | 待驗證 |
| THUMB-01 | JPG/JPEG/PNG/BMP/WebP/GIF、透明／損壞／動畫、冷暖快取與取消 | 待驗證 |
| VIEW-01、VIEW-02 | 固定序列、內嵌、fit 100%、縮放範圍／anchor、平移、Escape | 待驗證 |
| VIEW-03、VIEW-04 | 1024 預覽到完整原圖、分塊、256/12 MiB、A-B-A、相鄰預覽與釋放 | 待驗證 |
| FILE-01 | JPG quality 100、WebP 解碼像素比對證明無損、原檔與略過規則 | 待驗證 |
| FILE-02、FILE-03 | 可見投影、49/50 確認、取消零修改、basename／大小寫／Unicode、衝突、回收 | 待驗證 |
| DRAG-01、DRAG-02 | threshold／預覽／自動捲動、跨副檔名最小序號、確認後競態、toast | 待驗證 |
| RESULT-01 | 逐項／總數、部分完成後取消、未知結果不誤報成功、失敗繼續 | 待驗證 |
| DATA-01 | 舊 JSON、副本相容、原子替換、損壞隔離；升級另留證據 | 待驗證 |
| OS-01、UI-01 | picker／reveal、繁中、鍵盤／焦點、DPI／accessibility | 待驗證 |
| JOB-01 | 最多 8 helper、15 秒逾時、kill/reap、generation/request ID、佇列與 shutdown | 待驗證 |
| PERF-01 | Release 冷暖快取、同 Viewer 連續切圖、完整繪製 500ms、未完成計入、RSS／GPU 限制 | 待驗證 |
| PACKAGE-01 | 乾淨 makepkg、內容／hash、啟動、安裝／升級／解除安裝、profile 保留 | 待驗證 |

## 證據填寫格式

| 日期／TODO ID／案例 ID | source SHA-256／成品 SHA-256 | OS／Qt／桌面／plugin／DPI | fixture 來源／隔離目錄 | 命令／exit code／人工操作 | 結果／log／截圖／限制 |
|---|---|---|---|---|---|
| 尚無 Arch 執行證據 | — | — | — | — | 待驗證 |

已補上 `arch/v*` tag 發布 workflow；單一 job 在 Arch 容器建置、產生 SHA-256 並發布，不執行功能／桌面／安裝測試。A9.5 的流程實作完成；A8.5 的自動測試依精簡決定停用。尚未 commit、tag、push 或執行 hosted 發布。來源封裝成功、Arch 編譯成功、測試通過、桌面通過與生命週期通過，必須分別記錄。

2026-09-12 發布流程檢查：actionlint 1.7.7 檢查 Windows／Arch native workflow 通過；Bash 腳本與 workflow run 區段語法通過。版本核對以合成 Git 回應驗證接受正確 tag，拒絕版本不符、lightweight tag、錯誤 commit 與 dirty checkout。來源匯出 fixture 核對 archive、PKGBUILD 與逐檔 SHA-256 相符。這些是本機靜態／匯出檢查，不是 Arch 容器或 GitHub 實跑結果。
