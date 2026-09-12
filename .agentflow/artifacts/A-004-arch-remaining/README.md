# A-004：完成剩餘 Arch 驗收

承接「繼續完成」以及「完成的項目記得移除／都完成的話檔案就可以移除了」。最終結果見 [acceptance-summary.json](evidence/acceptance-summary.json)。23 個檢查在 X11 與 KDE Wayland 全數為 true，無 errors；剩餘 5 項已移除並刪除 TODO 檔案。

## 測試與環境

- [Debug](evidence/final-debug-ctest.log)：6/6，45.78 秒。[Release](evidence/final-release-ctest.log)：6/6，51.86 秒。[CLI PTY](evidence/cli-final-pty.json)：8 個案例。[QML accessibility](evidence/a11y-focused.log)：4 passed，0 failed（含 init/cleanup）。
- [版本](evidence/desktop-package-versions.txt)：KWin／Plasma 6.7.5，Qt 6.11.2，Xvfb 21.1.24，Openbox 3.6.1，fcitx5 5.1.22／chewing 5.1.13，Orca 50.2。
- [X11 journey](evidence/x11-2-journey.json)、[KDE Wayland journey](evidence/kde-2-journey.json)：獨立 DBus、PID、network、HOME／XDG。X11 使用 Xvfb／Openbox；KDE 是真實 KWin／Plasma Wayland client，KWin 的顯示後端巢狀連到私有 Xvfb。均為軟體繪圖、共用主機 kernel，並非完整開機 VM 或實體顯示器。
- 輸入由私有 XTest 經 compositor 傳遞；注音 `5j/` 組字加空白／Enter 送出「中」，沒有注入 QInputMethodEvent。AT-SPI 由真正 Qt bridge 取得。[X11 Orca](evidence/x11-orca-speech.txt)、[KDE Orca](evidence/kde-orca-speech.txt) 為 screen reader 真實語音文字輸出；未宣稱測過實體喇叭或點字設備。
- [X11 gallery](evidence/x11-2-selected.png)、[KDE gallery](evidence/kde-2-selected.png)、[KDE Viewer](evidence/kde-2-viewer.png)、[KDE picker](evidence/kde-2-native-picker.png)、[KDE drop dialog](evidence/kde-2-drop-confirmation.png)：Qt 200%，主視窗 1200×800 logical／2400×1600 pixel；圖片、文字與控制項已人工檢視。

## 真實缺口與修正

1. 原 main 使用 QGuiApplication，KDE platform theme 不提供 native file dialog。改 QApplication 並加入 Qt Widgets link；依賴仍在 qt6-base。條件見 [KDE Plasma 6.7 原始碼](https://raw.githubusercontent.com/KDE/plasma-integration/Plasma/6.7/qt6/src/platformtheme/kdeplatformtheme.cpp)。初始 Quick fallback 操作記於 devlog RUN-003；原始同名 picker probe 後來被綠燈重跑覆寫，不能把現有綠燈檔案當成原始紅燈截圖。
2. [紅燈選取](evidence/red-x11-2-journey.json)／[QtTest 紅燈](evidence/a11y-red.log)：畫面已選取，AT-SPI 無 selected/selectable。Gallery 綁定這兩個狀態後通過。
3. [紅燈排序選項](evidence/red-sort-options-atspi.json)／[QtTest 紅燈](evidence/sort-a11y-red.log)：四個 list item 無名稱。直接綁定 Accessible.name／selected／selectable，裝飾箭頭設 ignored。只設定 ItemDelegate.text 不足以修正此 Qt bridge，最終沒有留下這個多餘中介。
4. [worker 階段量測](evidence/worker-stage-red.json)：同一 preview 不寫 cache 127 ms、寫 PNG cache 718 ms。內部 cache 改 compression 0 的 lossless PNG，仍可讀舊 cache，來源及匯出品質不變；代價是磁碟快取較大，2,000 檔上限保留。

私有測試工具曾因未初始化 machine-id、缺少 KDE_SESSION_VERSION、繁中按鈕名稱、AT-SPI stale proxy、DPI／Wayland client-local 座標與 xdotool clearmodifiers 影響滑鼠按住狀態而誤判。這些不是產品缺陷。KWin clientGeometry 由標準 scripting／DBus API 查詢。Dolphin 使用真實 xdg-open／kde-open 啟動，只有測試建立的程序會被清理。

## 混合素材與效能定義

[冷快取](evidence/perf-final-cold-1.json)／[暖快取](evidence/perf-final-warm.json)：Release，主機 Hyprland 原生 Wayland，Intel UHD 620／Mesa 26.2.2，實際視窗 960×1054、DPR 1。9 檔逐一開啟，再 A-B-A、close/reopen，共各 12 次選取。每筆等待真正完整原圖 framePresented；10 秒未完成也保留，不以 preview ready 代替 paint。

冷快取是新 App profile，沒有清除 OS page cache；來源 hash 讀取在選取前完成。暖快取使用同一來源和上一輪 profile/cache 副本，程序重新啟動。最終量測期間沒有本任務的建置、CTest 或私有桌面流程。冷／暖最大 482／296 ms，超標 0、未完成 0；只代表這組固定素材與環境。圖庫掃描與初始化在 Viewer 選取前，不列入 selection-to-paint 指標。CPU/RSS 只算主程序，非總系統/GPU成本。

[原版忙碌樣本](evidence/perf-red-busy.json)、[原版無私有桌面樣本](evidence/perf-red-idle.json) 保留。compression 0 的早期驗證仍與建置可能重疊，[逐檔 log](evidence/perf-green-cold.log) 有 WebP 532 ms 等待（paint 526 ms）的超標；該輪 results.json 隨暖跑被覆寫，不能當成完整 raw JSON。後續冷暖分開輸出，避免覆寫；沒有刪除慢的素材。

原始照片來源、作者、授權、下載 URL 與 SHA-256 見 [corpus-sources.json](evidence/corpus-sources.json)：

- Gull：Daniel Schwen／Dschwen，[CC BY 2.5](https://creativecommons.org/licenses/by/2.5/)，2272×1704。JPEG 副本及 BMP 來自此照片。
- Hopetoun falls：Photo by DAVID ILIFF，[CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/)，3072×2048。WebP 為此照片的無損格式衍生。
- Shaki waterfall：Sisianci，[CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/)，1280×959。PNG 為此照片的無損格式衍生。
- 12 MP 拼圖由上述照片縮放組合，並非原生 12 MP 拍攝；透明圖表由 benchmark 程式產生。拼圖與包含照片的驗證截圖之衍生部分採 CC BY-SA 3.0，上述照片作者署名一併適用，未暗示作者認可 PicLens。

Fronalpstock 與 transparency demo 原檔下載被伺服器拒絕，未繞過；不列入已測素材。圖片原檔、私有 rootfs、runtime、cache 與大型完整診斷 log 不納入 Git；來源 manifest 與產生方式可重跑。

## 重跑

1. 依 `packaging/arch/validate.sh build` 建立 Release，並在私有 Arch userland 安裝版本表中的桌面、fcitx5-chewing、Orca／python-atspi 等工具；這不是主機安裝腳本。
2. `python3 download-corpus.py` 校驗已固定的三個原檔。`cmake -S benchmark -B /tmp/piclens-A004-benchmark -DCMAKE_BUILD_TYPE=Release -DPICLENS_BUILD=/absolute/release/build`，再建置。
3. 以 offscreen 執行 `corpus_check /absolute/corpus-original /tmp/prepare /absolute/piclens-worker --prepare` 建立格式衍生、圖表與拼圖；以原生 Wayland 執行 `corpus_check /absolute/corpus /absolute/new-run-root /absolute/piclens-worker`。暖跑複製 cold profile 到新 run root，保留各自 results.json。
4. `PICLENS_ARCH_ROOTFS=/absolute/private/rootfs PICLENS_APP_BUILD=/absolute/release/build python3 run-private.py x11 2`，再以 `kde 2` 跑另一工作階段。腳本只讀取指定 build、隔離操作自己的測試檔案；全部 checks true 且無 errors 才 exit 0。

[source-files.json](evidence/source-files.json) 固定產品變更的檔案 hash；完整套件建置與最終 review 另見本目錄的證據。
