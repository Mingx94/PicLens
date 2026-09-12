模型：gpt-5.6-luna/xhigh；時間：2026-09-13 01:02:09（Asia/Taipei，UTC+08:00）

# A6.7／A6.8 實際 QML 拖放排查報告

## 結論

原始 host journey 同時包含一個測試旅程問題，以及兩個實際 QML 輸入／拖放問題。

已完成最小修正：

- `apps/linux/qml/Gallery.qml`
  - 在圖片 `MouseArea` 加上 `preventStealing: true`。
  - 將 ghost 的 `Drag.source` 從祖先 `gallery` 改為 `ghost`。
- `apps/linux/tests/qml_test.cpp`
  - 新增真正的 Qt 滑鼠來源→目標→釋放 regression。
  - 移除暫時診斷輸出；沒有呼叫 `app.dropRename` 或 `endDrag` 代替實際 drop。

沒有修改 `Main.qml`、controller、檔案操作、CMake、依賴或其他文件。沒有 commit 或 push。

## 根因證據

### 1. GridView 會偷走 press

移除 `preventStealing` 的 focused red journey 結果：

```text
after-threshold dragging=true pressed=true
over-target dragging=false pressed=false grabber=QQuickGridView
FAIL: dropArea->property("containsDrag").toBool() returned FALSE
```

因此拖曳超過 threshold 後，`GridView/Flickable` 取得 pointer grab，來源 `MouseArea` 被取消，ghost、target hint 與 drop 都不可能完成。`preventStealing: true` 後，來源仍保持 pressed，拖曳狀態可持續到 release。

### 2. `Drag.source: gallery` 會把目標視為自己的子樹

只保留 `preventStealing`、仍使用 `Drag.source: gallery` 的 focused red journey 結果：

```text
after-threshold dragging=true pressed=true ghostDrag active=1 ghostSource=Gallery
over-target dragging=true pressed=true containsDrag=false dropSignals=0,0,0
FAIL: dropArea->property("containsDrag").toBool() returned FALSE
```

Qt Quick 的 `DropArea` 會拒絕 drag source 是該 DropArea 祖先的情況。`gallery` 正是所有 delegate／DropArea 的祖先。把 source 改為同層的 `ghost` 後，目標收到 `entered`，`containsDrag=true`，並可進入 `onDropped`。這符合 Qt `Drag` 與 `DropArea` 的公開語意；參考 [Qt Drag QML Type](https://doc.qt.io/qt-6/qml-qtquick-drag.html) 與 [Qt Quick `QQuickDropArea` 實作](https://codebrowser.dev/qt6/qtdeclarative/src/quick/items/qquickdroparea.cpp.html)。

### 3. 原始 probe 的 target 位置不適合證明 drop

原始測試使用 `GridView.contentY=140`。目標卡片下緣超出 viewport；public 幾何檢查仍顯示 point 在卡片內，但 Qt delivery 對 clipped／culled delegate 不會建立有效 DropArea target，因此會看到 ghost 移動、卻沒有 `containsDrag` 或 drop signal。

將正確的實際 journey 改成 `contentY=200`，讓來源與目標完整位於 viewport 後，現況輸出為：

```text
PASS: actualDragDropMouseJourneyOpensConfirmationAndCancelPreservesFiles()
Totals: 3 passed, 0 failed, 0 skipped, 0 blacklisted
```

這證明原始 host probe 的「沒有 target」不能單獨歸因於 runtime；修正測試幾何後，仍能重現並驗證上述兩個 runtime 問題。

## Regression 覆蓋

`actualDragDropMouseJourneyOpensConfirmationAndCancelPreservesFiles()` 使用 `QTest::mousePress`、兩次 `QTest::mouseMove` 與 `QTest::mouseRelease` 驅動完整旅程。它驗證：

- threshold 前不開始拖曳，threshold 後來源仍 pressed。
- `DropArea.containsDrag`、`entered`、`positionChanged`、ghost visibility 與 target hint visibility。
- 真正的 `dropped` signal 與 `confirmOpen`。
- 兩個選取來源都出現在確認預覽。
- 已占用的 `target-01.jpg`、`target-02.png` 會跨副檔名共用序號，預覽使用最小可用 `target-03.png`、`target-04.png`。
- 確認框出現前沒有改動來源或既有目標檔案。
- 取消後來源／既有目標內容仍完全相同，`target-03/04` 不存在。
- 取消後 `dragging`、`dragPath`、`containsDrag`、ghost、target hint 與 pointer pressed 狀態都清除。

原有 `dragDropUsesThresholdPreviewAndCancelWithoutMutation()` 保留 threshold、explicit ungrab／cancel 與 autoscroll 覆蓋；本次新 regression 沒有用那些動作代替真實 drop。

## 驗證輸出

建置：

```text
cmake --build /tmp/piclens-drag-drop-build --parallel 2
ninja: no work to do.
```

Focused green：

```text
QT_QPA_PLATFORM=offscreen ... /tmp/piclens-drag-drop-build/qml_test \
  actualDragDropMouseJourneyOpensConfirmationAndCancelPreservesFiles
PASS   : QmlIntegrationTest::actualDragDropMouseJourneyOpensConfirmationAndCancelPreservesFiles()
Totals: 3 passed, 0 failed, 0 skipped, 0 blacklisted, 419ms
```

Full five-suite run（focused green 後一次）：

```text
Test #1: domain      Passed
Test #2: imaging     Passed
Test #3: app         Passed
Test #4: controller  Passed
Test #5: qml         Passed
100% tests passed out of 5
Total Test time (real) = 36.97 sec
```

## 限制與交接

- worker sandbox 使用 offscreen／隔離的 XDG 目錄完成建置與測試。
- worker 無法存取 host native Wayland socket，因此未在本 turn 重跑 native Wayland；host 應在匯入這三個允許檔案後重跑原生 probe。
- 未要求或實作外部 OS drag-and-drop，也未新增 library。
- A6.7／A6.8 的本次實際 in-app drop confirmation journey 已修正並有 regression；沒有宣稱整份 Arch TODO 完成。

Self-check: Diagnose and fix only the actual in-app drop confirmation journey — f550ba75-c9a2-4a03-aa96-d195587a9706
