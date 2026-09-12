* _2026-09-12 23:52:00 (gpt-5.6-luna/xhigh)_

## 結果

已完成 Qt/QML integration test-only 修正。測試載入正式 `qrc:/qml/Main.qml`，使用真實 `Controller`、`piclens-worker`、`ThumbProvider`、`ImageItem` 註冊與 production QML resources。沒有修改 Controller、ImageItem、production QML、既有測試、fixture、TODO 或 README。

本階段修正 `Harness::load()`：`show()` 後先等待 `QTest::qWaitForWindowExposed()`，再 `requestActivate()` 並等待 `QTest::qWaitForWindowActive()`，才開始送出鍵盤事件。offscreen 仍通過。

拖曳測試改用 `QGuiApplication::styleHints()->startDragDistance()`。它先用低於門檻的真實滑鼠事件確認不進入拖曳，再用高於門檻的事件啟動拖曳。取消時使用公開 Qt API `QQuickItem::ungrabMouse()`，驗證 QML `onCanceled` 清除 `dragging`、`dragPath`、pressed 狀態與選取狀態。沒有再直接呼叫 QML `endDrag()`。

## 變更路徑

- `apps/linux/CMakeLists.txt`：新增 `qml_test` target、正式 QML qrc、Lucide icon qrc、focused CTest entry 與 offscreen 環境。
- `apps/linux/tests/qml_test.cpp`：新增真實 QML UI journey、視窗曝光／啟用同步、threshold 與 Qt ungrab 測試。
- `qml-implementation-report.md`：本報告。

測試 profile、圖片與 worker 暫存輸出都使用 `QTemporaryDir` 或 `/tmp` build。沒有寫入個人 profile。

## Red-before-green

本階段沒有捕捉到新的 offscreen red-before-green：原有 offscreen QML CTest 在同步修正前已通過；本次修改後仍通過。host 提供的 native 初始 `Ctrl+F` 失敗，是測試在視窗曝光／啟用前送鍵的已知 journey 缺口；本次加入等待，但 clone 內的 Wayland 直接執行在 QPA／event-dispatcher 初始化前後以 `SIGABRT` 結束，沒有可用的 native assertion 結果。這不能證明產品有缺陷，也不能取代 host 的 native rerun。

先前階段曾修正 image provider lifetime、delegate 尚未實體化及 offscreen `DropArea.containsDrag` 假設等測試錯誤；均沒有 runtime fix 或 production regression 證據。

## 測試

- `cmake -S apps/linux -B /tmp/piclens-qml-worker-build -G Ninja -DCMAKE_BUILD_TYPE=Debug -DBUILD_TESTING=ON`：exit 0。
- `cmake --build /tmp/piclens-qml-worker-build -j2`：exit 0。
- `ctest --test-dir /tmp/piclens-qml-worker-build -R '^qml$' --output-on-failure`：exit 0，1/1 通過，2.81 秒。
- `ctest --test-dir /tmp/piclens-qml-worker-build --output-on-failure`：exit 0，5/5 通過，20.86 秒。包含 `domain`、`imaging`、`app`、`controller`、`qml`。
- `git diff --check`：exit 0。
- Native 嘗試：`QT_QPA_PLATFORM=wayland QT_QPA_PLATFORMTHEME= /tmp/piclens-qml-worker-build/qml_test searchSelectionAndContextMenuUseActualDelegates -v1`：exit 134，`SIGABRT`，沒有產生測試 assertion；這是本 clone disposable session 的 QPA／event-dispatcher 啟動阻礙。

CTest 的 `qml` entry 使用 offscreen。測試 binary 沒有硬編碼 platform；可另外以 `QT_QPA_PLATFORM=wayland` 執行。現有證據只支持 offscreen 的實際 QML journey，以及視窗輸入前等待曝光／啟用的 harness 修正；不宣稱 native Wayland、X11、KDE、IME、原生 accessibility 或 compositor lifecycle 已通過。

## TODO 對照與仍未證明的條款

- **A0.6**：有真實 QML entry、CTest、正式 resources 與 offscreen 開啟／關閉。仍未證明 Arch 真實桌面的 platform plugin、Wayland／X11 工作階段及可見視窗 lifecycle。
- **A1.3**：真實 `GridView` 載入 10,000 model rows，驗證一次 model reset、`reuseItems`、有界 `cacheBuffer`、捲到底部、縮放視窗後 delegate 數有界限。仍未做 Release 記憶體／效能量測，也未用 10,000 個磁碟圖片解碼驗證。
- **A3.1**：驗證正式 `Rows` model 的 10,000 筆單次 reset 與 QML 容器更新。仍未證明真實掃描流程的所有搜尋／排序組合都維持單次 reset。
- **A3.2**：驗證資料夾 delegate、項目數、GridView、側欄收合及 120／240 縮圖設定反映到 cell width。仍未做 renderer 截圖或完整視覺像素驗收。
- **A3.3**：以實際 `TextField` 與 `Ctrl+F` 驗證全選、輸入取代、完整路徑搜尋、清除後保留焦點，以及 root／tree 不變。仍未以磁碟 spy 證明沒有 disk scan；native keyboard、IME 尚未證明。
- **A3.5**：以實際 delegate 事件驗證已選／未選圖片右鍵作用範圍，以及資料夾右鍵時 rename／trash disabled。未執行 reveal、rename 或 trash 的 OS 整合。
- **A3.6**：實際縮圖載入、捲動、`setVisible`、filter 與 model reset 後驗證 image key、selected 欄位無殘留，並觀察真實 delegate 數量有界。內部 worker subscription、取消 request 與所有 pooled lifecycle 沒有額外公開觀測點，未逐一證明。
- **A5.1**：以真實 Viewer 驗證選取順序第一張、immutable snapshot、前後導覽、名稱與 Escape 返回 GridView focus。未驗證來源消失、重開同圖、快速 A-B-A 與完整預載／取消生命週期。
- **A5.2**：以真實 `ImageItem` 驗證 off-center wheel input、1.2 倍步進、0.1～8.0 clamp、reset、滑鼠拖曳不穿透選取、keyboard navigation 及 mouse grab。未以畫面座標證明 pointer-anchor 精確 invariant，也未證明 native IME／KDE／X11 input。
- **A6.7**：以真實 Gallery 事件驗證低於 threshold 不啟動、高於 threshold 啟動、拖曳狀態、目標 `DropArea` 存在且啟用、自動捲動，以及透過 `QQuickItem::ungrabMouse()` 觸發的 QML cancel cleanup。仍未證明原生 drag-and-drop 的 `onEntered`／`onDropped`、實際拖曳預覽畫面或 compositor capture-lost；Qt API ungrab 不等於 native desktop capture-lost。
- **A6.8**：以真 Controller `dropRename()` 驗證多選、basename 跨副檔名占用、最小序號 `03`／`04`、確認預覽與取消零修改。未驗證實際 `DropArea.onDropped` 交付、確認後執行、TOCTOU 衝突及取消後檔案操作結果。
- **A6.9**：真實 worker rename batch 驗證成功 toast、逐項 results、source／target／status、結果 dialog 與 6 秒 timer。未驗證失敗通知 12 秒、部分失敗繼續、取消／不確定結果及逐項錯誤注入。
- **A7.1**：真實 components launch context 載入元件展示，驗證繁中長檔名、明／暗 theme 變更、主要元件、空狀態，以及 offscreen 可取得時的按鈕 role/name/state。未宣稱原生 accessibility 工具、IME、完整設計系統與所有錯誤狀態。
- **A7.2**：驗證測試 launch context 的 900×700、minimum 800×600、縮到 800×600、窄版 layout 與可見 GridView。未驗證產品指定 1600×1000 實際啟動、DPI 100／150／200%、不同 renderer、捲軸不遮內容及真實桌面顯示。

未移除任何 TODO，也沒有宣稱其他 Arch TODO 已完成。沒有由目前證據確認的 production runtime bug；native Wayland 結果需由 host 在可用 compositor 下重跑確認。

Self-check: Qt/QML integration test-only authority.
