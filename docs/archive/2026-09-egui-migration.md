# egui migration design

Status: completed and archived on 2026-09-03.

This document records the completed GPUI-to-egui migration. It does not define current product behavior or engineering requirements. [Product specification](../product-spec.md) and [runtime invariants](../runtime-invariants.md) remain authoritative.

## Target structure

```text
crates/piclens-domain/     product rules and framework-light models
crates/piclens-infra/      filesystem, settings, image and OS adapters
crates/piclens-desktop/    egui/eframe shell and composition root
crates/piclens-gpui/       old shell, retained until parity and cutover
```

The target dependency direction is `piclens-desktop -> piclens-infra -> piclens-domain`. UI framework types must not enter domain or infrastructure public contracts unless an adapter cannot reasonably avoid them.

## Runtime flow

PicLens adopts the event-driven pattern described by Fastpotify's [architecture overview](https://github.com/crmne/fastpotify#how-it-is-built). Its relevant source examples are [model.rs](https://github.com/crmne/fastpotify/blob/main/src/model.rs), [backend.rs](https://github.com/crmne/fastpotify/blob/main/src/backend.rs), and [ui/mod.rs](https://github.com/crmne/fastpotify/blob/main/src/ui/mod.rs).

```text
egui view -> Action -> App reducer -> Command -> background backend
                   ^                         |
                   +--------- Event <-------+
```

- A view reads model state and appends `Action` values. It does not perform filesystem access, image decode, process launch, or persistent writes.
- The App applies actions after drawing. An action can update local state, open a dialog, or emit a backend command.
- A `Command` contains all identity needed to reject late work. It uses a monotonically increasing generation for collection-wide replacement and a request ID for individual work.
- The backend owns blocking work and bounded queues. It sends `Event` values to the App and calls `egui::Context::request_repaint()` after each successful send.
- The App drains events in `eframe::App::logic`, validates generation and request identity, and then updates model state.
- Idle workers block on channel receive. The UI requests timed repaint only for a real deadline, animation, or unfinished operation.

## Decisions

### Frontend crate

The frontend is named `piclens-desktop`, not `piclens-egui`. The crate is a desktop product shell; egui remains an implementation detail. It is now the root workspace default member. `piclens-gpui` remains a workspace member only until the archived migration record and final removal step are complete.

### egui version and renderer

The first implementation pins egui and eframe 0.36.1. eframe 0.36 uses separate `App::logic` and `App::ui` phases and makes wgpu its default renderer. PicLens enables only wgpu, AccessKit, Wayland, and X11 support. Glow is not enabled. Windows and Linux runtime verification is still required before the renderer decision is final.

### Background execution

The initial backend uses bounded standard-library channels and explicitly owned threads. Its single coordinator owns at most one cancellable library-scan worker. A newer load cancels and joins the previous scan before starting another. PicLens work is primarily filesystem traversal, image decode, and helper processes. Tokio is not added without a concrete async service that benefits from it.

Thumbnail and viewer work reuse the existing cancellation and bounded decoder rules. The egui gallery owns eight fixed thumbnail threads behind a 256-job queue. Each job uses the killable 15-second decoder-child timeout. One owned cache thread runs the existing dirty-cache pruning check every five seconds; each dirty pass keeps the newest 2,000 PNG files. The backend does not create one unbounded thread per thumbnail or file.

### State ownership

| Owner | State |
|---|---|
| `model.rs` | Page, loaded data, selection, dialogs, status, viewer snapshot |
| `app/` | Action reducer, generation counters, request identity, frame lifecycle |
| `backend.rs` | Command/event channels, worker ownership, shutdown |
| `images.rs` | Thumbnail keys, image-loader entries, texture lifetime |
| `ui/` | Immediate layout, hover response and per-frame action collection |
| `piclens-infra` | Scan, settings JSON, cache files, decode, trash, reveal and conversion |
| `piclens-domain` | Sort, path, zoom, rename and file-operation rules |

Persistent product state must not be hidden in egui widget memory. Short-lived widget state can use stable egui IDs.

## Migration safety

- `piclens-gpui` remains runnable until the egui package passes parity review.
- Both frontends read the same normalized settings schema and data-root override.
- Only one frontend runs against a profile during a smoke or migration test.
- Tests that rename, trash, or convert use a disposable copied fixture.
- GPUI code is removed only after package scripts, CI, documentation, and release metadata point to `piclens-desktop`.

## Feature parity inventory

| Surface | Required behavior | Main authority | Target owner |
|---|---|---|---|
| Startup | Restore last picker folder; empty state when unavailable | Product specification | App + infra settings |
| Folder navigation | Stable tree root, history, side buttons, refresh | Product specification | Model + gallery UI |
| Gallery | Fixed grid, visible-only tiles, 10,000-item projection | Runtime invariants | Gallery UI + backend |
| Search and sort | In-memory search, four sort modes, natural order | Product specification | Domain + model |
| Selection | Click, Ctrl, Shift, Ctrl+Shift, order and anchor | Runtime invariants | Model + gallery actions |
| Thumbnails | Bounded workers, cancellation, timeout and cache pruning | Runtime invariants | Backend + image loader |
| Viewer | Immutable sequence, navigation, zoom, pan and focus return | Runtime invariants | Viewer model + UI |
| Viewer previews | One active job, adjacent preload, three textures/12 MiB | Runtime invariants | Backend + image loader |
| Animation formats | Identify GIF/WebP animation; show unsupported state | Product specification | Infra + gallery/viewer UI |
| Context actions | Reveal, rename and trash with correct selection scope | Product specification | Actions + dialogs |
| Conversion | JPG, lossless WebP and same-basename cleanup | Runtime invariants | Infra + backend |
| Batch results | Continue on failure and show per-item results | Product specification | Backend + result dialog |
| Drag rename | Threshold, preview, target, sequence plan and confirm | Runtime invariants | Gallery UI + domain |
| Settings | Atomic compatible JSON, quarantine corrupt input | Runtime invariants | Infra settings |
| Diagnostics | Context-rich startup, navigation, image and file-op logs | Runtime invariants | App + backend + infra |
| Accessibility | Names, roles, states, actions and focus restoration | Product/testing docs | All UI modules |
| Packaging | MSI, portable, DEB and RPM paths | Release docs | Scripts + CI |

## 2026-09-03 parity review

本次 review 對照產品規格、runtime invariants、GPUI frontend、egui frontend、共用 domain／infra、測試與發佈腳本。`cargo test --workspace --locked` 通過 164 項測試：`piclens-desktop` 81、`piclens-gpui` 46、`piclens-domain` 23、`piclens-infra` 14。測試過程只有 Windows incremental cache 無法 finalize 的 note，沒有測試失敗。

| 範圍 | 結論 | 差異或後續工作 |
|---|---|---|
| 啟動、folder picker、startup restore、history 與 folder tree | 功能相符 | clean Windows／Ubuntu／Fedora 的 wgpu 啟動仍屬平台驗證，不是本機 test 的結論。 |
| 搜尋、排序、recursive mode、thumbnail size 與 selection | 功能相符 | egui 與 GPUI 都支援暫時性 search、recursive 與 sidebar CLI override。 |
| Thumbnail pipeline 與 cache pruning | contract 相符 | 真實大型圖庫的取消、資源與長時間 pruning 行為仍需 Release metrics。 |
| Viewer snapshot、navigation、zoom、pan、preload 與 texture budget | contract 相符 | 像素品質、高 DPI 與 compositor presentation 仍需真實 app 檢查。 |
| Context action、rename、trash、conversion、batch result 與 drop rename | 功能相符 | 實際 OS recycle bin、drag/drop 與 helper process 行為仍由平台 smoke 驗證。 |
| 快捷鍵、滑鼠側鍵、focus 與 accessibility | 功能相符 | egui 會在鍵盤游標移到虛擬化 viewport 外時，將目標 row 捲入畫面。 |
| Settings schema、路徑與相容讀取 | 功能相符 | egui 會讀取既有視窗大小，resize 停止後由 background backend 保存正規化後的新大小。 |
| 原生選單列 | 接受差異 | 產品規格未要求原生選單列；egui 以視窗內控制、context menu 與同等快捷鍵提供產品操作。 |
| Screenshot、performance workload 與 metrics | 功能相符 | egui 支援 automated screenshot、60 次持續捲動 workload，以及 CPU、記憶體、thumbnail、search、scroll metrics；大型圖庫的 Release 測量與外部 GPU／storage 記錄仍待階段 8 驗證。 |
| 預設 frontend、文件、package 與 CI | 已切換 | Root default member、現行文件、效能腳本、封裝腳本與 release workflow 都指向 `piclens-desktop`；GPUI crate 與遷移紀錄仍待最後移除及歸檔。 |

這份 review 完成 code、contract 與 test 層的差異盤點。它不取代真實輸入、視覺、高 DPI、效能、clean-runner package lifecycle 或 hosted release 驗證；這些項目仍維持未勾選。

## Evidence still required

- A wgpu startup and image-texture smoke on clean Windows, Ubuntu, and Fedora environments.
- Real accessibility and input checks. Headless render tests do not replace these checks.
- Release measurements for gallery and viewer workloads on a representative disposable library.

## Local implementation evidence

On 2026-09-02, workspace check, build, 110 tests, and clippy with warnings denied passed. The 31 `piclens-desktop` unit/headless tests cover reducer ordering, actions that enqueue actions, bounded command delivery, request-identity rejection, backend errors, scan cancellation, shutdown ownership, collection reset, reload, stale library results, in-memory search, four sort modes, settings compatibility, loaded/error states, a 10,000-item virtualized grid with fewer than 100 materialized thumbnail requests, stable selection anchor and order, plain/Ctrl/Shift/Ctrl+Shift selection action mapping, thumbnail source identity, generation rejection, unload cancellation, and animated-image exclusion. The 10 `piclens-infra` tests include decoder cancellation, timeout, and cache pruning. An isolated Windows wgpu app smoke loaded one valid PNG through the background scan and thumbnail workers, produced one PNG in the isolated thumbnail cache, processed the root viewport close request, and exited with status 0 without using the two-second fallback. This proves local startup, background folder loading, thumbnail decode/cache production, and graceful smoke close. It is not evidence of texture paint quality, accessibility, interaction, visual correctness, cache pruning timing under load, or clean Windows/Linux runner compatibility; those checks remain open.

On 2026-09-03, an isolated Windows wgpu smoke loaded 240 copied PNG fixtures and ran temporary search, the 60-step continuous-scroll workload, automated screenshot capture, and schema 2 metrics in one process. The process exited with status 0, saved a 1280×800 PNG, recorded 96 completed thumbnail requests plus search, scroll, CPU, working-set, window, and display-scale fields, and logged screenshot save and workload completion before shutdown. This validates the automated diagnostics path in a Debug build. It does not replace Release measurement on a representative library, external GPU and storage records, visual comparison, or clean-runner validation.

On 2026-09-03, an explicitly authorized copy of the existing Windows PicLens profile was used for an isolated egui compatibility smoke. The 2,004-file copy matched the source manifest before launch. egui restored the saved folder, sort, recursive mode, 180-pixel thumbnail size, expanded sidebar, and 2176×1224 window; existing cache names and the complete prior log prefix were preserved. The run appended only to the copied log, created no new quarantined settings file, and exited without an app-log warning or error. The production profile and settings hashes remained unchanged after the run. The copied profile and screenshot were moved to the Recycle Bin after validation and are not repository content.

On 2026-09-03, the root default member, current documentation, performance and package scripts, and Windows release workflow were switched to `piclens-desktop`. The updated Windows MSI built successfully with zero warnings and zero errors; its staged `PicLens.exe` matched `target/release/piclens-desktop.exe` by SHA-256. PowerShell, XML, Git Bash syntax, Cargo metadata, default `cargo run -- --help`, workspace format, build, check, 164 tests, Clippy with warnings denied, and `git diff --check` all passed. The package remains unsigned. Linux DEB/RPM build and package lifecycle still require their native runners.

The Stage 7 isolated Windows Release smoke used 120 copied images for cold and warm gallery runs, then eight copied images for continuous Viewer navigation. The gallery runs produced schema 2 search and scroll metrics plus valid 1280×800 screenshots. The Viewer run painted all 17 checked selections, reported zero unpainted selections and zero 500ms target misses, and produced a valid 1280×800 Viewer screenshot. All runs used isolated profiles, exited successfully, and added no `WARN` or `ERROR` entry to the app log. The generated data under `target/` contains only disposable copies of the repository icon.

## Completion

On 2026-09-03, `piclens-gpui` and the four GPUI-only repository skills were removed after the default frontend, current documents, scripts, packaging, and workflow pointed to `piclens-desktop`. The workspace now contains `piclens-domain`, `piclens-infra`, and `piclens-desktop`. `Cargo.lock` contains no GPUI, gpui-component, Zed, or scap package.

After removal, workspace format, build, check, 118 tests, Clippy with warnings denied, and `git diff --check` passed. The unsigned Windows MSI rebuilt with zero warnings and zero errors. Its staged `PicLens.exe` matched the Release `piclens-desktop.exe` by SHA-256. A final isolated Windows Release smoke loaded eight copied images, completed search and continuous scrolling, saved a valid 1280×800 screenshot and schema 2 metrics, and exited with no app-log warning or error. DEB/RPM build, clean-runner package lifecycle, signing, tag creation, push, and hosted release remain separate release work.
