# PicLens GPUI TODO

Source: Waku (`egoist/waku`) GPUI patterns, checked against [product-spec](docs/product-spec.md) and [runtime-invariants](docs/runtime-invariants.md).

Adopt patterns only. Do not copy Waku source (GPL-3.0). For GPUI APIs, follow the Zed revision in `Cargo.lock`.

Done: `app/` split, theme global, bundled CJK fonts, native menus, image context menus, background thumbnail decode.

---

## Next (product-spec first)

Do in this order. Keep the app building after each item.

- [x] Mouse side buttons use the same folder history as Alt+← / Alt+→ (`MouseButton::Navigate`).
- [x] Open-folder picker must not block the GPUI loop. Prefer `cx.prompt_for_paths`.
- [x] Scan folder and child folders on a background task. Guard results with the existing generation counter.
- [x] Virtualize the gallery with `list()` / `ListState`. Start with list mode, then grid as rows.
- [x] Schedule thumbnails only for tiles in the viewport. Drop work for items that leave the screen.
- [x] Give viewer and rename overlays their own key context, restore previous focus, and focus the overlay after it joins the dispatch tree.

## Later

- [x] Show batch file-op results as notifications (`Root::render_notification_layer` is already mounted).
- [x] Gallery keys: Home, End, PageUp, PageDown.
- [x] Persist sidebar collapsed state and window size.
- [x] Add GPUI-layer unit tests for navigation, escape, and selection (no window).
- [x] Viewer pointer pan and wheel zoom with pointer anchor.
- [x] In-app drag-drop rename (preview, drop target, confirm). Follow Zed / former Qt drag session, not Waku attachments.
- [x] Deep folder-tree expand.
- [x] Overlay scrollbar for long galleries (gpui-component first).
- [x] Honor `reduce_motion` if viewer or overlay animation is added.

## Not planned

- Command palette.
- Dark theme (design system is light only).
- i18n beyond Traditional Chinese.
- Waku computer-use, webview, or SQLite session store.
- MSI / DEB packaging (tracked separately in release docs).
