# Tracker

## Identity

- **Work key:** A-004-arch-remaining.

- **Active Ask:** A-004.

- **Goal:** 完成剩餘5項Arch驗收；有證據才移除，全部通過才刪除TODO.arch.md.

- **Last update:** 2026-09-13 02:05:44 Asia/Taipei.

- **Evidence commit:** uncommitted.

## Overall state

- **State:** active.

- **Reason:** Work remains.

- **Total:** 4.

- **Completed:** 3.

- **Remaining:** 1.

## Accepted task checklist

- [x] **T-1:** 在隔離桌面/profile驗證原生picker與200% accessibility，必要時僅修正實際暴露的產品缺口；以真實對話框、輔助工具、狀態与畫面證明A2.2/A7.5。Source: A-004. Proof: .agentflow/artifacts/A-004-arch-remaining/evidence/acceptance-summary.json.

- [x] **T-2:** 建立私有KDE Wayland與獨立X11驗證環境，測試鍵盤、繁中IME、焦點、拖放/取消、選單、dialog、picker、reveal與回收筒；不變更使用者桌面或主機套件；記錄版本、真實輸入與限制。Source: A-004. Proof: .agentflow/artifacts/A-004-arch-remaining/evidence/kde-2-journey.json.

- [x] **T-3:** 取得可重現的代表性混合圖片素材，以Release量測冷暖快取、完整原圖繪製時間、超標與未完成樣本；保留素材來源、hash、尺寸及量測定義，不以preview冒充完成。Source: A-004. Proof: .agentflow/artifacts/A-004-arch-remaining/evidence/perf-final-cold-1.json; .agentflow/artifacts/A-004-arch-remaining/evidence/perf-final-warm.json.

- [ ] **T-4:** 只移除驗證通過項目，全部完成才刪TODO.arch.md並更新引用；保留證據、完成獨立review、提交與推送。Source: A-004.

## Accepted scope changes

- None.

## Current recovery

- **Current item:** T-4.

- **Last proven result:** KDE/X11 19項journey全通過；選取與排序a11y缺口已修正，更新後正複核。9檔混合圖庫冷暖各12次原圖paint均無超標/未完成，冷最大482ms.

- **Active blocker or running process:** 最後原生a11y、Debug/Release suite正開始.

- **Next safe action:** 收取最後原生與suite結果，移除所有已完成TODO並更新驗證文件.

- **Expected changed files:** TODO.arch.md、docs/linux/arch-validation.md、apps/linux/src/、apps/linux/qml/、apps/linux/tests/、apps/linux/CMakeLists.txt、.agentflow/devlog.md與A-004證據；runtime修改須先有實際失敗與需求對應.

## Completion proof

- **All accepted tasks checked:** no.

- **Blocking accepted decision:** none.

- **Operation running:** yes.

- **Next action remaining:** T-4.

- **Evidence status:** current.

- **Judgment:** active.

## Update meaning

- Saving this tracker is a recovery checkpoint, not a stop signal.

- Work continues with the next unfinished item unless an independent stop condition applies.
