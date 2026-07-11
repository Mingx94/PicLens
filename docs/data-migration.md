# Avalonia to Qt data continuity

Qt cutover 不需要搬移使用者設定目錄；兩個 runtime 刻意共用同一個 app-data contract。

## Stable locations

沒有 `PICLENS_DATA_ROOT` override 時，Avalonia 與 Qt 都使用平台 local application data root 下的 `PicLens`：

```text
PicLens/
  piclens-settings.json
  Logs/PicLens.log
  Thumbnails/*.png
```

- Windows：`%LOCALAPPDATA%\PicLens`。
- Linux：平台 local data location，通常是 `$XDG_DATA_HOME/PicLens` 或 `~/.local/share/PicLens`。
- Tests/release smoke 可用 `PICLENS_DATA_ROOT` 或 Qt diagnostic `--data-root` 隔離，不可污染真實 profile。

## Settings compatibility

兩邊使用相同 camel-case JSON fields：

```json
{
  "lastFolderPath": "C:\\Images",
  "sort": { "key": 1, "direction": 1 },
  "includeSubfolders": true,
  "thumbnailSize": 240
}
```

`sort.key` / `sort.direction` 維持 numeric enum contract。Qt tests 直接讀取 Avalonia schema；legacy .NET tests 直接讀取 Qt schema，涵蓋 last folder、modified-time descending sort、recursive mode 與 thumbnail size。Qt 仍忽略已淘汰的 `favoriteFolders` / `version` fields，寫回時會移除未知 fields；這些 fields 不是目前 committed product state。

因此 Qt 首次啟動會還原既有設定；若 cutover 後需要暫時 rollback，Avalonia 也能讀取 Qt 最近寫入的設定。Corrupt settings 的 quarantine 命名契約在兩邊相容。

## Logs

兩邊都 append 到 `Logs/PicLens.log`。文字格式不要求 binary compatibility；每筆 entry 自帶 timestamp、level、context 與 details。Qt cutover 不刪除舊 log，installer uninstall/upgrade 也不應刪除 local app data。

## Thumbnail cache

兩邊共用 bounded `Thumbnails` directory，但 cache key 的 timestamp encoding 不同（.NET ticks、Qt milliseconds），所以既有 thumbnails 不保證 cache hit。這是可安全重建的 derived data：

- Qt 會逐步建立自己的 SHA-256 PNG entries。
- Shared bounded pruning 可能清除最舊的 Avalonia 或 Qt entries，但不碰原始圖片。
- Cutover、upgrade、uninstall 不需也不應主動清空整個 cache。

## Cutover verification

正式切換前需在真實 profile 的副本驗證：

1. Avalonia 寫入設定後，Qt 還原同一 folder/sort/recursive/size。
2. Qt 更新設定後，Avalonia rollback 能讀取。
3. Qt 產生新 thumbnails 時不修改原始圖片。
4. MSI upgrade/uninstall 保留 `%LOCALAPPDATA%\PicLens`。

Windows 可用隔離 copy 執行前三項 runtime smoke：

```powershell
pwsh -NoProfile -File qt/scripts/verify-data-continuity.ps1
pwsh -NoProfile -File qt/scripts/verify-data-continuity.ps1 `
  -SourceProfile "$env:LOCALAPPDATA\PicLens"
```

Script 絕不直接啟動 app 指向來源 profile；它先將來源完整複製到
`artifacts/data-migration/profile-copy`，以正式檔名契約啟動 Qt，驗證 Avalonia JSON
schema、sort/recursive/thumbnail state、既有 thumbnail sentinel、來源圖片 hash 與原始
profile manifest。沒有 `-SourceProfile` 時使用 synthetic profile，適合 CI 與日常回歸；
正式 cutover 仍需在使用者授權後以真實 profile 副本執行。

2026-07-11 已在使用者授權後以 `%LOCALAPPDATA%\PicLens` 的完整本機副本執行 packaged Qt
smoke；2 張來源圖片、原始 profile manifest 與 cache sentinel 均保持不變，Qt 正確還原
modified-time descending、recursive 與 240 px 設定。Machine-readable evidence 位於忽略版控的
`artifacts/data-migration/profile-continuity.json`。
