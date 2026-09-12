2026-09-13T00:51:46+08:00 (gpt-5.6-luna/xhigh)

# Metrics portability 修正報告

本次只修正 metrics 測試，沒有修改 runtime、文件或其他行為。

## 修正內容

- `apps/linux/tests/metrics_test.cpp`
  - 將 `metricsTimestampUtc` 的非空 UTC 字串檢查移到平台條件外。Linux 的 CPU、RSS 與 peak RSS 可用性檢查仍保留；非 Linux 的不可用欄位仍檢查為 `null`。
  - `realViewerKeepsPreviewAndSharpSelectionIdentity` 不再固定要求 `viewerSharpTargetMisses == 0`。測試現在逐筆讀取 `fullPaintSamples[].milliseconds`，依 `viewerSharpTargetMilliseconds` 計算精確 miss 數，並驗證最大值。

## 驗證

使用既有 `/tmp/piclens-arch-metrics/build-metrics`，先重建 focused target：

```text
cmake --build /tmp/piclens-arch-metrics/build-metrics --target metrics_test -j2
[0/2] Re-checking globbed directories...
[1/5] Automatic MOC and UIC for target metrics_test
[2/4] Building CXX object CMakeFiles/metrics_test.dir/tests/metrics_test.cpp.o
[3/4] Linking CXX executable metrics_test
```

metrics focused test 只執行一次：

```text
ctest --test-dir /tmp/piclens-arch-metrics/build-metrics --output-on-failure -R '^metrics$' -j2
Test project /tmp/piclens-arch-metrics/build-metrics
    Start 5: metrics
1/1 Test #5: metrics ..........................   Passed    1.83 sec

100% tests passed out of 1

Total Test time (real) =   1.83 sec
```

沒有執行 broad repeated suite，也沒有 commit 或 push。

Self-check: 跨平台 timestamp、Linux-only unavailable 欄位與 sample-derived miss count 都只在測試層修正，未擴大 scope。
