* _2026-09-13 00:10:24 (gpt-5.6-luna/xhigh)_

# 搜尋欄清除控制項修補報告

基線：`59582516b738a8c9279a2b6a3efdf7441d8b4378`。

## 變更

- `apps/linux/qml/Main.qml`：在 `searchField` 內加入既有 `ActionButton` 與 `x.svg`。搜尋非空時顯示，`app.busy` 時停用，點擊清空 `app.search` 並回復 `searchField` 焦點。加入右側留白避免文字重疊。
- `apps/linux/tests/qml_test.cpp`：以 `hint == "清除搜尋"` 從實際 `TextField` 子項尋找清除控制項，加入實際鍵盤輸入、滑鼠點擊、投影、焦點、樹、右側留白與 `Ctrl+F` 回歸檢查。

## 實際驗證

建置指令：

```text
cmake -S apps/linux -B /tmp/piclens-search-clear-build -G Ninja -DCMAKE_BUILD_TYPE=Debug -DBUILD_TESTING=ON
cmake --build /tmp/piclens-search-clear-build -j2
```

基線紅燈：

```text
run_dir=$(mktemp -d /tmp/piclens-search-clear-run-XXXXXX)
mkdir -p "$run_dir/xdg-data" "$run_dir/xdg-config" "$run_dir/xdg-cache"
env QT_QPA_PLATFORM=offscreen QT_QPA_PLATFORMTHEME= XDG_DATA_HOME="$run_dir/xdg-data" XDG_CONFIG_HOME="$run_dir/xdg-config" XDG_CACHE_HOME="$run_dir/xdg-cache" /tmp/piclens-search-clear-build/qml_test searchClearControlUsesActualInputAndPreservesProjection
```

結果：exit 1；`clear` 找不到，符合缺少 in-field control 的基線預期。

修補後 focused 綠燈：以上同一測試通過，`3 passed, 0 failed`。

完整五項 offscreen suite：

```text
run_dir=$(mktemp -d /tmp/piclens-search-clear-run-XXXXXX)
mkdir -p "$run_dir/xdg-data" "$run_dir/xdg-config" "$run_dir/xdg-cache"
env XDG_DATA_HOME="$run_dir/xdg-data" XDG_CONFIG_HOME="$run_dir/xdg-config" XDG_CACHE_HOME="$run_dir/xdg-cache" ctest --test-dir /tmp/piclens-search-clear-build --output-on-failure
```

結果：`100% tests passed out of 5`；`domain`、`imaging`、`app`、`controller`、`qml` 全部通過，總時間 42.00 秒。

## 限制

- focused 與完整 suite 都使用 `QT_QPA_PLATFORM=offscreen`。sandbox 無法存取原生 Wayland；原生三段 journey 由 host 執行與驗收。
- 此 disposable clone 不證明 OS confinement；credentials、network、provider limits 仍由 host 負責。

Self-check: Add only the missing in-field search clear action and its real input regression.
