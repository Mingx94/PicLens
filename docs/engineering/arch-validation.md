# Arch Qt 版轉移驗收

狀態：2026-09-12 已在 Omarchy 4.0.3（Arch 系）／Hyprland Wayland 執行 Qt 4.0.0 本機驗收。Debug 與 Release 建置、各 4/4 CTest、Wayland／XWayland 隔離啟閉、圖片與 Linux 檔案規則已有證據。**整體驗收尚未完成**；乾淨 Arch、KDE Plasma、獨立 X11、完整輸入／DPI 與真實舊版升級仍待驗。Windows 結果不代替 Linux 證據。

本頁配合 [TODO.arch](../../TODO.arch.md)、[產品規格](../product/product-spec.md)、[共用驗收案例](../product/acceptance.md)、[不變條件](runtime-invariants.md)與[資料延續性](data-continuity.md)。沒有觀測到的項目一律「待驗證」，不以單次啟動推定完整功能。

## 2026-09-12 Omarchy 本機驗收

來源基準為 `146187b10dc13291998596dced8075b2d0c11411`。首次執行時工作樹乾淨；之後修正 CMake 的測試環境及 domain 測試的 fixture 定位，再回填文件。程式功能未變更。證據目錄為 `artifacts/arch/2026-09-12-local/`，下表的相對檔名皆位於此處。完整來源快照的 SHA-256 另見下方封裝紀錄。

環境：Omarchy 4.0.3、Linux 7.2.3-arch1-3、Qt 6.11.2、GCC 16.2.1、CMake 4.4.3、Hyprland 0.56.2、Mesa 26.2.2、Intel i5-8250U／UHD Graphics 620、8 GiB 級記憶體。這是現有桌面，不是乾淨 Arch chroot。版本與硬體明細見 `environment.json`。

| 項目／案例 | 實測結果 | 證據與限制 |
|---|---|---|
| A0 建置／CTest | GNU Make Debug 與 Release 建置成功；修正後 Release 4/4，18.90 秒；Debug 4/4，18.15 秒 | `build-release.log`、`build-debug-resume.log`、`ctest-release-fixed.log`、`ctest-debug.log`；CMake >=3.25／Qt >=6.8 的最低版本未逐版測試 |
| START-01、JOB-01 基礎 | Wayland 與 XWayland 空視窗 exit 0；正常關閉紀錄，檢查時無本次 App／worker 殘留 | `wayland-empty/`、`xwayland-empty/`；不是獨立 X11，也未操作原生 picker |
| A0.5 CLI | help／version 成功；未知選項、缺值、非法數字、負時間、過小寬高、負項目數皆 exit 2 | `cli-codec-results.json`；不是所有路徑與輸出失敗組合 |
| THUMB-01、FILE-01 codec | JPG／JPEG／PNG／BMP／WebP／GIF 與透明 PNG 成功；動畫 GIF／WebP、損壞 PNG 拒絕完整解碼 | `cli-codec-results.json`、`gallery.png`、`gallery.json`；11 個圖庫項目包含 1 個資料夾，7 張靜態縮圖完成；WebP 像素比對與 JPG 編碼見 imaging suite |
| SCAN-01、FILE-03、DATA-01 | 符號連結迴圈不重複掃描、100 層掃描、無權限回報且繼續、大小寫與中文空白路徑、改名與來源消失、設定寫入失敗保留原位元組、XDG／環境變數／CLI 優先序通過 | `acceptance-probe.cpp`、`acceptance-probe.log`；使用全新合成 fixture |
| FILE-01～03、RESULT-01、JOB-01 | Linux 不可覆寫提交競態、成功改名、部分完成後取消、trash helper 失敗／逾時與回收通過 | domain／imaging／controller suites；完整 QML 確認與取消操作仍待驗 |
| FILE-03 回收介面 | FileOperations 實際呼叫 gio；中文、空白、`&` 檔名成功；原檔消失，隔離 Trash/files 與 trashinfo 存在，內容相符 | `trash-probe.cpp`、`trash-probe.log`；同一檔案系統、隔離 XDG_DATA_HOME 與無效測試 DBus 位址；未操作個人回收筒，跨磁碟及桌面還原仍待驗 |
| VIEW-03、VIEW-04、PERF-01 初步 | 同 Viewer 在 1200×800 PNG／900×1200 JPG 間切換；冷 profile 7/7 完整提交，40～289 ms；暖 profile 新程序 7/7，80～227 ms；兩輪皆零超過 500 ms、零未完成 | `viewer-cold.json`、`viewer-warm.json`；合成圖片，未清 OS 檔案快取，量測時未開截圖；不代表真實混合圖庫效能驗收 |
| GRID-01 初步 | 10,000 筆合成 model 定時跨頁捲動；最多 38 個 delegate（含 pooled）、12 個可見項目；投影 64 ms；主程序採樣峰值 RSS 約 195 MiB | `virtual.json`、`virtual.png`、`desktop-results.json`；無圖片解碼，RSS 不含 helper／GPU，不能當萬張真實圖庫結果 |
| UI-01 畫面 | 淺／深色、繁中、縮圖與 Viewer 截圖已檢查 | `gallery.png`、`gallery-dark.png`、`viewer.png`；Hyprland 平鋪把兩種要求尺寸都配置成 960×1054，因此不能勾選 1600×1000／800×600 精確尺寸與 DPI |
| A9 暫存安裝 | App 與 worker mode 755，desktop／圖示／授權／metadata mode 644；desktop-file-validate 與 appstreamcli 成功；ldd 無缺庫 | `installed-files.txt`、`install-stage.log`、`desktop-validate.log`、`appstream-validate.log`、`ldd-*.log`；AppStream 有 content-rating／developer-info 兩項資訊提示 |

### 已排除的環境問題

第一次沙箱內 Wayland 啟動為 SIGABRT。core 的堆疊位於 QGuiApplication 建立 platform／event dispatcher 階段；相同二進位檔與隔離 profile 在核准的沙箱外 Wayland 啟動正常。詳見 `wayland-first-crash.log`。這筆失敗仍保留，不當作成功執行。

原本 CTest 只設定 `QT_QPA_PLATFORM=offscreen`。主機繼承的 `QT_QPA_PLATFORMTHEME=gtk3` 仍嘗試開啟螢幕，造成 imaging／controller 假失敗。現於 CMake test ENVIRONMENT 清空該值，僅影響測試子程序；以父程序仍為 gtk3 的條件重跑，Debug／Release 全數通過。失敗原始紀錄為 `ctest-release.log`，修正後為 `ctest-release-fixed.log`。未改使用者桌面設定。

首次 makepkg 因 Ninja 未安裝而停止。使用者已安裝 Ninja 1.13.2-3，後續固定來源重跑；不使用跳過相依檢查的 makepkg，也不略過 check()。

makepkg 的 `-ffile-prefix-map` 會把 `__FILE__` 對應成 `/usr/src/debug/piclens/...`。domain 測試原先依此尋找 fixture，因此 check() 出現 `read fixture`，其餘 3/4 通過。已改用既有 CMake `PICLENS_REPO` 定義定位共用 JSON；另以相同路徑對應條件編譯執行通過，再由新來源快照完整重建。失敗紀錄保留於 `makepkg.log`，最小修正驗證為 `domain-remapped.log`。

### 本次封裝與交付

最終來源 SHA-256：`aa36172260354dffeb68df369afcb5ef19df2d5b8d83fa102bfbdc70f09f1349`。主套件 `piclens-4.0.0-1-x86_64.pkg.tar.zst` 的 SHA-256：`07a1ddeaf4d7c076a0199192579413eee257969abe3fda79a1146a1f9c67c079`。另產生 `piclens-debug-4.0.0-1-x86_64.pkg.tar.zst`。

交付目錄為 `dist/piclens-4.0.0-arch-validation-20260912/`，包含來源、主套件、debug 套件、固定 checksum 的 PKGBUILD、逐檔 SOURCE-MANIFEST.json、SHA256SUMS 與建置完成後補充的 VALIDATION.md。來源包為工作樹快照，並非已發布 tag；快照之後只回填文件，已核對封裝的 CMake／C++／QML／tests 與目前工作樹一致。

| 項目 | 結果與證據 |
|---|---|
| Ninja／makepkg | Ninja 1.13.2-3；`CMAKE_BUILD_PARALLEL_LEVEL=3 QT_QPA_PLATFORMTHEME=gtk3 makepkg` 成功，未略過相依或 check()；4/4 CTest、19.41 秒。見 `makepkg-final.log`、`makepkg-LastTest.log` |
| 來源與成品 | `sha256sum -c SHA256SUMS`、`makepkg --verifysource` 成功；主套件含 8 個應用檔案，App／worker 755，其他 644；無 Rust 執行檔或 Windows DLL；主機 `pacman -T` 無缺相依。見 `package-metadata.log`、`package-files.log`、`package-modes.log` |
| 成品版執行 | 從套件解出 App／worker，在原生 Wayland 使用 Mesa Intel UHD Graphics 620／OpenGL 4.6；exit 0，7/7 完整原圖提交，37～281 ms、零未完成。見 `packaged-wayland.log`、`packaged-wayland.json` |
| 隔離安裝生命週期 | fakeroot 加獨立 pacman root／DB：安裝、檔案完整性、同版重裝、移除成功，合成 profile／來源位元組保留。見 `package-lifecycle/results.json` 與 `summary.json`。此空 DB 使用 `-Udd`，執行依賴由主機另查核；不代表乾淨 OS、主機 desktop hooks 或真實舊版升級 |

成品啟動 log 另有 portal app ID 註冊警告（`Connection already associated with an application ID`）。此輪未操作原生 picker／reveal，尚未確認警告對它們的影響，OS-01 保持待驗。

### 尚缺的驗收

KDE Plasma／Wayland、真正 X11、原生 picker／reveal、繁中 IME、鍵盤／焦點、實際滑鼠拖放與 capture-lost、100%／150%／200% compositor DPI、輔助工具、跨磁碟回收、所有 generation 競態，以及代表性真實萬張圖庫仍缺證據。此輪未使用 Computer Use。一般桌面上的固定尺寸參數不能取代視窗管理器實際配置。

乾淨 Arch 建置、真正舊版套件升級與主機桌面安裝生命週期須另驗；不得用來源匯出、暫存安裝或同版重裝推定完成。公開發布與 AUR 上傳未執行。

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

來源快照與真實 SHA-256 由交付目錄的 `SOURCE-MANIFEST.json`／`SHA256SUMS` 記錄。封裝腳本允許新建 `repo/dist/<handoff-name>`，並排除 dist 內容。本次 Omarchy 二進位套件與限制見上方封裝紀錄；乾淨 Arch 建置仍待驗。

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
| START-01 | 空狀態、picker 取消、上次路徑還原、初始視窗 | 部分：Omarchy 空狀態啟閉通過；picker／精確尺寸待驗 |
| TREE-01、NAV-01 | root 固定、後代展開、歷史／側鍵、不覆寫 picker 路徑 | 部分：controller suite 通過；原生輸入待驗 |
| SCAN-01、SORT-01 | 遞迴、取消、權限／symlink、自然與四種排序、穩定相同值 | 部分：Linux suites 與權限／symlink probe 通過；完整非同步競態待驗 |
| SEARCH-01、GRID-01 | 記憶體搜尋、焦點、10,000 筆單次 reset、有界 delegate／捲動 | 部分：投影／reset suite 與合成 model 捲動通過；焦點／真實萬張待驗 |
| SELECT-01、MENU-01 | Ctrl／Shift anchor、選取順序、右鍵作用範圍、排除資料夾 | 部分：Linux suite 通過；QML 真實輸入待驗 |
| THUMB-01 | JPG/JPEG/PNG/BMP/WebP/GIF、透明／損壞／動畫、冷暖快取與取消 | 部分：codec 矩陣、縮圖畫面與 imaging suite 通過；完整快取壓力待驗 |
| VIEW-01、VIEW-02 | 固定序列、內嵌、fit 100%、縮放範圍／anchor、平移、Escape | 待驗證 |
| VIEW-03、VIEW-04 | 1024 預覽到完整原圖、分塊、256/12 MiB、A-B-A、相鄰預覽與釋放 | 部分：imaging suite 與 Wayland 連續原圖切換通過；完整競態矩陣待驗 |
| FILE-01 | JPG quality 100、WebP 解碼像素比對證明無損、原檔與略過規則 | 部分：Linux domain／imaging suites 通過；完整 UI 批次流程待驗 |
| FILE-02、FILE-03 | 可見投影、49/50 確認、取消零修改、basename／大小寫／Unicode、衝突、回收 | 部分：Linux suites、改名及隔離 gio 通過；UI 確認／跨磁碟待驗 |
| DRAG-01、DRAG-02 | threshold／預覽／自動捲動、跨副檔名最小序號、確認後競態、toast | 待驗證 |
| RESULT-01 | 逐項／總數、部分完成後取消、未知結果不誤報成功、失敗繼續 | 待驗證 |
| DATA-01 | 舊 JSON、副本相容、原子替換、損壞隔離；升級另留證據 | 部分：Linux suites、XDG／權限 probe 通過；真實舊版升級待驗 |
| OS-01、UI-01 | picker／reveal、繁中、鍵盤／焦點、DPI／accessibility | 待驗證 |
| JOB-01 | 最多 8 helper、15 秒逾時、kill/reap、generation/request ID、佇列與 shutdown | 待驗證 |
| PERF-01 | Release 冷暖快取、同 Viewer 連續切圖、完整繪製 500ms、未完成計入、RSS／GPU 限制 | 部分：合成圖冷暖 14/14 提交、40～289 ms；真實代表圖庫待驗 |
| PACKAGE-01 | 乾淨 makepkg、內容／hash、啟動、安裝／升級／解除安裝、profile 保留 | 部分：本機 makepkg、成品啟動、隔離安裝／同版重裝／移除通過；乾淨 OS／舊版升級／主機桌面整合待驗 |

## 證據填寫格式

| 日期／TODO ID／案例 ID | source SHA-256／成品 SHA-256 | OS／Qt／桌面／plugin／DPI | fixture 來源／隔離目錄 | 命令／exit code／人工操作 | 結果／log／截圖／限制 |
|---|---|---|---|---|---|
| 2026-09-12／本機基礎驗收 | 見本頁來源與封裝紀錄 | Omarchy 4.0.3／Qt 6.11.2／Hyprland／Wayland、XWayland | 合成圖片與隔離 profile | 詳見本頁實測表 | 部分通過；全項驗收仍待完成 |

已補上 `arch/v*` tag 發布 workflow；單一 job 在 Arch 容器建置、產生 SHA-256 並發布，不執行功能／桌面／安裝測試。A9.5 的流程實作完成；A8.5 的自動測試依精簡決定停用。尚未 commit、tag、push 或執行 hosted 發布。來源封裝成功、Arch 編譯成功、測試通過、桌面通過與生命週期通過，必須分別記錄。

2026-09-12 發布流程檢查：actionlint 1.7.7 檢查 Windows／Arch native workflow 通過；Bash 腳本與 workflow run 區段語法通過。版本核對以合成 Git 回應驗證接受正確 tag，拒絕版本不符、lightweight tag、錯誤 commit 與 dirty checkout。來源匯出 fixture 核對 archive、PKGBUILD 與逐檔 SHA-256 相符。這些是本機靜態／匯出檢查，不是 Arch 容器或 GitHub 實跑結果。
