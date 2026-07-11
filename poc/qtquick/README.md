# PicLens Qt Quick PoC

這是與現有 Avalonia app 並存的小型 Qt 6 / Qt Quick PoC，用來驗證圖片瀏覽器最重要的效能路徑，不是完整移植。

## 已涵蓋

- Qt Quick `GridView` delegate reuse 與 viewport cache。
- C++ `QAbstractListModel`，在 worker thread 掃描單一資料夾。
- `QQuickAsyncImageProvider` 搭配最多四條 worker threads，依畫面尺寸解碼縮圖。
- 原生資料夾選擇器、縮圖尺寸調整與內嵌圖片預覽。
- UI 顯示圖片數量與資料夾掃描耗時。

PoC 暫不包含 folder tree、遞迴掃描、排序選項、檔案操作、選取、縮圖磁碟快取、viewer zoom/pan、設定保存與發佈封裝。

## 需求

- Qt 6.5 或更新版本，包含 Quick、Quick Controls 2 與 Concurrent。
- CMake 3.21 或更新版本。
- Ninja 或其他 CMake generator，以及和 Qt build 相容的 C++20 compiler。

目前 Windows 開發機已具備 MSYS2 UCRT64 Qt 6.11.1、CMake 與 Ninja。

## 建置與執行

在 repo root 執行：

```powershell
cmake -S poc/qtquick -B poc/qtquick/build -G Ninja -DCMAKE_BUILD_TYPE=Release
cmake --build poc/qtquick/build
./poc/qtquick/build/picLensQtQuickPoc.exe
```

也可以用命令列直接載入資料夾，方便重複測試同一組樣本：

```powershell
./poc/qtquick/build/picLensQtQuickPoc.exe --folder "D:\Pictures\benchmark-set"
```

Linux 使用相同的 configure/build 命令，執行檔沒有 `.exe`。

## 建議驗證方式

使用至少一個包含數千張 JPEG、PNG 或 WebP 的資料夾：

1. 觀察首次掃描時間與首屏縮圖出現速度。
2. 快速來回捲動，確認 UI thread 不被圖片解碼卡住。
3. 改變縮圖尺寸，觀察重新解碼時的互動流暢度與記憶體。
4. 開啟數張大型圖片，確認右側預覽不阻塞 grid scrolling。

需要檢查實際 scene graph backend 時，可以在 PowerShell 先設定：

```powershell
$env:QSG_INFO = "1"
./poc/qtquick/build/picLensQtQuickPoc.exe
```

## PoC 通過條件

- Release build 在目標 Windows 與 Linux 機器可以啟動。
- 大型資料夾掃描期間視窗維持可互動。
- 快速捲動不會因同步解碼產生明顯長停頓。
- 記憶體不會隨反覆捲動持續無界成長。

通過後再決定正式 migration boundary：保留 .NET domain/infrastructure 並建立 IPC 邊界，或將 presentation/backend 一併移植到 C++。
