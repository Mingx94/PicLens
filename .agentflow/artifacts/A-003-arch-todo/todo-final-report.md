* _2026-09-13 01:12:20 (gpt-5.6-sol/low)_

# Arch TODO 最終 disposition

以 runtime／tests commit `27464f3fa33bf999540fc3745f07e3b596b2c74d` 與原始 53 項 TODO 為基準。本輪只修改 `TODO.arch.md`、`docs/linux/arch-validation.md`、`todo-final-report.md`。沒有修改 source、test、config、dependency、README 或 THIRD-PARTY，也沒有 commit、push、安裝主機套件或發布。

## 結果

本輪移除 10 項：A2.5、A6.6、A6.7、A7.2、A8.1、A8.3、A8.4、A9.3、A9.4、A10.3。連同前一輪移除的 38 項，共完成 48 項。

保留 5 項：A2.2、A7.3、A7.4、A7.5、A8.2。原因如下：

- A2.2：仍缺原生 picker 的選取／取消與啟動入口整合證據。
- A7.3：仍缺 KDE Plasma／Wayland 與繁中 IME 的完整桌面驗證。
- A7.4：仍缺原生 X11 工作階段。
- A7.5：仍缺 200% 與原生輔助工具驗證。
- A8.2：現有 Viewer 素材是 synthetic fixture，且 cold／warm 只區分 App cache。不是代表性混合本機圖片或 OS cold cache。

A8.4 依原始條款判定完成。它要求逐節 audit 與競態、取消、資料安全案例，不是要求所有其他原生平台 TODO 先完成。尚缺的平台條款已分別留在 A2.2、A7.3、A7.4、A7.5；素材效能缺口留在 A8.2。

## 文件與檢查

- 待辦：[TODO.arch.md](TODO.arch.md)。
- 持久驗證文件：[docs/linux/arch-validation.md](docs/linux/arch-validation.md)。
- 本報告：[todo-final-report.md](todo-final-report.md)。
- 證據索引：[A-003 evidence](.agentflow/artifacts/A-003-arch-todo/evidence/)。

文件使用相對連結。內部文件連結與列出的證據目標已檢查。驗證文件明列 runtime commit、日期、平台、數值、clean Arch 與 lifecycle 範圍，也保留 synthetic model、GPU／子程序、bwrap userland、Wayland-only、無公開發布等限制。

clone 不是 OS sandbox。執行環境仍可能繼承網路、credentials 與絕對路徑可見性；本輪未使用這些能力改動外部狀態。

Self-check: 只移除 10 個已有原始條款證據的 TODO，保留 5 個未證實的原生平台與代表性素材項目。
