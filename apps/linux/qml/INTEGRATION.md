# QML 接線與驗證

正式入口為 `Main.qml`，同目錄 QML 已由 CMake 收入 qrc。使用 Qt 6.8 以上的 QtQuick、QtQuick.Controls.Basic、QtQuick.Layouts、QtQuick.Dialogs，以及程式註冊的 `PicLens.Native 1.0`。

## 已對齊 controller.h / controller.cpp

- `app`、`launchWidth`、`launchHeight`、`showComponents` 為 context properties。
- Controller 的 properties 共用 `changed()` NOTIFY。QML 使用本地唯讀 binding 的變更通知管理 Viewer 焦點與 Toast Timer，不訂閱不存在的 `viewerOpenChanged`、`toastOpenChanged` 或 `toastTextChanged` signals。
- Gallery 直接接 `scrollTo(int row)`，呼叫 GridView.positionViewAtIndex(row, GridView.Contain)；接 `focusGallery()` 返回鍵盤焦點。`viewerIndex` 僅用於 Viewer，代表排除資料夾的 snapshot 索引。
- `sortIndex`：0 名稱升冪、1 名稱降冪、2 修改時間最舊優先、3 修改時間最新優先。
- `thumbnailSize`：120～240、step 20。
- `zoomBy()` 使用乘數 1.2 或 1/1.2；`resetZoom()` 回到符合畫布的 1 倍。百分比顯示 app.zoom。
- `confirm(true/false)` 處理一般確認與 renameOpen。renameStem 只含檔名主體；Controller 保留副檔名。
- `select` 接 Qt keyboard modifiers 原值與 rightClick；controller 決定 Ctrl／Shift 選取語意。Ctrl+F 聚焦搜尋並 selectAll。
- Toolbar 的 JPG／WebP／同名格式清理套用目前搜尋與篩選結果，以 count 而非 selectionCount 判斷可用。cleanup 不是移除中繼資料，而是保留 JPG／JPEG、WebP 並清理其他同名格式。
- 右鍵選單只有 reveal、rename、trash。資料夾停用 rename／trash，保留 reveal。
- `dropRename(target)` 使用目前選取來源；超過系統拖曳門檻才呼叫 setDragActive(true)。取消不投遞 drop；釋放才投遞。結束／取消均清除 dragActive。接收目標為其他圖片，不接受外部檔案拖入。
- 操作 methods 已有契約所需的預設參數。檔案修改範圍、確認、結果翻譯、取消與錯誤恢復由 controller 負責。

## 原生 Viewer

`viewerCanvas` 中已放入 ImageItem，anchors.fill: parent，Component.onCompleted 呼叫 app.attachItem(this)。原生 item 持有 frame 並處理 wheel／pan／mouse；QML 不再覆蓋 MouseArea。Controller 連接 zoomChanged 與 framePresented，QML 按鈕／鍵盤透過 app.zoomBy 和 resetZoom 操作，避免雙向 zoom binding 回授。錯誤文字只在 viewerError 非空時顯示。

## 元件展示

正式入口使用 `--components`，由 showComponents 顯示 ComponentPanel：主要／次要／危險／停用按鈕、輸入欄、排序、核取方塊、縮圖滑桿、選取卡片、錯誤、空狀態及唯讀結果。展示控制項不呼叫檔案操作。

請搭配隔離 `--data-root` 與空的 `--folder`，避免 controller 啟動時恢復使用者圖庫。明暗及窄視窗可用 `--dark --width 800 --height 600`。Showcase.qml 保留 mock-controller fixture，現在依賴應用註冊的原生 module，不能再用未註冊該 module 的通用 qml runner 直接載入。

## 資源與縮圖

SVG 對應 qrc:/icons/{name}.svg，由正式 CMake 打包。ToolButton.icon.color 隨主題著色，不使用 shader。

Provider URL 為 image://thumb/{imageKey}，cache=false、asynchronous=false。GridView reuseItems=true，cacheBuffer 最多 600。每 120ms 從實體 delegate 篩出可見 paths，送至 setVisible。離開圖庫清空需求；pooled 清空 source，reused 接新來源。

## 本次驗證

後續 QML 修正已留待重新整合驗證：主要按鈕明確設定 icon／buttonText／highlight 前景色；新增 NeutralComboBox、NeutralCheckBox、NeutralSlider；文字統一為回收筒。已選圖片的一般按下延後至未拖曳的 release 才單選，Ctrl／Shift 保留原選取語意；資料夾導覽排至 release 後。Viewer 左右鍵只在 zoom <= 1.01 切圖，工具列移至頂部，畫布以工具列高度加上下間距留白。縮圖 source 改為唯一宣告式 binding，移除 imageKeyChanged／folderChanged／pooled／reused 的 imperative source 寫入，以免初始化或回收移除 binding。

- Windows MSYS2 Qt 6.11.1，CMake Release 正式 piclens 與 piclens-worker 建置通過；輸出 `F:/PicLens/artifacts/qml-native-build/`。使用隔離輸出是因原 windows-preview 目錄拒絕寫入。未修改 C++。
- 正式執行檔 offscreen Viewer 載入 fixture PNG，exit 0；metrics 記錄 1 次 fullResolution 原圖提交，unpaintedSelections=0。證據：`F:/PicLens/artifacts/qml-native-smoke/viewer-metrics.json`。此指標為 scene graph 提交，不是 OS compositor 顯示時間。
- 正式執行檔 `--components --dark --width 800 --height 600` offscreen 啟動，exit 0。兩次執行未輸出 QML／qrc 警告。
- 尚未驗證 Linux 桌面像素、原生 FolderDialog、實際滑鼠拖放、輸入法或輔助工具。未執行檔案修改作業，也未驗證 Qt 6.8 的實際建置。
