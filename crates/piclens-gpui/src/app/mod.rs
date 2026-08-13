//! Main window: library, thumbs, sidebar, selection, viewer, file operations.
//!
//! Render is split across child modules so the shell stays readable.
//! State and commands stay here.

mod gallery;
mod overlays;
mod render;
mod shell;

use std::collections::{BTreeSet, HashMap, HashSet};
use std::ops::Range;
use std::path::PathBuf;
use std::sync::Arc;

use gpui::*;
use gpui_component::input::{InputEvent, InputState};
use gpui_component::notification::Notification;
use gpui_component::WindowExt;
use piclens_domain::{
    apply_layout_persist, path_equals, AppSettings, DropTargetBatchRenamePlan, ImageListItem,
    ImageSequenceSnapshot, ListItem, ListQuery, Point, SortDirection, SortKey, SortState,
    ZoomState, clamp_zoom, is_fit_view, pan_offset, reset_zoom_state, zoom_at_point,
    DEFAULT_THUMBNAIL_SIZE,
};
use piclens_infra::{
    apply_drop_rename, cleanup_same_basename, convert_to_jpg, convert_to_lossless_webp,
    ensure_thumbnail, info, plan_drop_rename, rename_image, reveal_in_file_manager,
    scan_child_folders, scan_folder, trash_paths, warn, JsonSettingsStore,
};

use crate::actions::{
    CleanupSameBasename, ClearSelection, CloseOverlay, ConvertJpg, ConvertWebp, CycleSort,
    DropRenamePlan, FocusSearch, GalleryEnd, GalleryHome, HistoryBack, HistoryForward,
    MoveSelectionDown, MoveSelectionLeft, MoveSelectionRight, MoveSelectionUp, OpenFolder,
    OpenViewer, Refresh, RenameSelection, RevealInFileManager, SelectAll, ToggleGalleryMode,
    ToggleIncludeSubfolders, ToggleSidebar, TrashSelection, ViewerNext, ViewerPrev, ZoomIn,
    ZoomOut, ZoomReset,
};
use crate::drag_rename::{
    drag_begin, drag_cancel, drag_finish, drag_move, is_dragging, DragFinish,
    DragPhase,
};
use crate::folder_tree::{
    apply_tree_children, toggle_expand, visible_tree_rows, ExpandAction, TreeRow,
};
use crate::history::FolderHistory;
use crate::interaction::{
    apply_selection, batch_notice_kind, batch_result_message, clear_selection, gallery_jump_index,
    next_escape_target, page_key_outcome, BatchNoticeKind, EscapeTarget, GalleryJump,
    PageKeyOutcome,
};
use crate::scan_apply::{apply_folder_scan, FolderScanPayload};
use crate::thumbs::{item_range_for_rows, thumb_queue_update};

const MAX_THUMB_IN_FLIGHT: usize = 8;

#[derive(Clone, Copy, PartialEq, Eq)]
enum GalleryMode {
    Grid,
    List,
}

pub struct PicLensApp {
    settings_store: Arc<JsonSettingsStore>,
    settings: AppSettings,
    folder_path: Option<String>,
    items: Vec<ListItem>,
    visible: Vec<ListItem>,
    child_folders: Vec<String>,
    tree_children: HashMap<String, Vec<String>>,
    tree_expanded: HashSet<String>,
    selected: BTreeSet<String>,
    selection_order: Vec<String>,
    history: FolderHistory,
    status: String,
    search: Entity<InputState>,
    search_text: String,
    sidebar_collapsed: bool,
    gallery_mode: GalleryMode,
    viewer: Option<ViewerState>,
    rename: Option<RenameState>,
    drop_rename: Option<DropTargetBatchRenamePlan>,
    drag: DragPhase,
    hover_path: Option<String>,
    viewer_canvas_bounds: Option<Bounds<Pixels>>,
    viewer_panning: Option<Point>,
    generation: u64,
    /// Source path -> cached PNG path for tiles.
    thumbs: HashMap<String, PathBuf>,
    thumb_pending: HashSet<String>,
    thumb_failed: HashSet<String>,
    /// Prevents stacking concurrent thumb pump tasks.
    thumbs_pump_scheduled: bool,
    gallery_list: ListState,
    viewport_rows: Range<usize>,
    focus_handle: FocusHandle,
    viewer_focus: FocusHandle,
    rename_focus: FocusHandle,
    overlay_restore_focus: Option<FocusHandle>,
    /// Held so tasks cancel on drop / generation bump (do not detach).
    async_tasks: Vec<Task<()>>,
    _subscriptions: Vec<Subscription>,
    /// Set on release so late callbacks skip UI updates.
    shutting_down: bool,
}

struct ViewerState {
    sequence: ImageSequenceSnapshot,
    zoom: ZoomState,
    message: Option<String>,
    /// Safe on-disk image for `img()` (always a decoded PNG path when present).
    display_path: Option<PathBuf>,
}

struct RenameState {
    path: String,
    input: Entity<InputState>,
}

impl PicLensApp {
    pub fn new(window: &mut Window, cx: &mut Context<Self>, initial_folder: Option<String>) -> Self {
        let settings_store = Arc::new(JsonSettingsStore::new());
        let settings = settings_store.load();
        let search = cx.new(|cx| InputState::new(window, cx).placeholder("搜尋名稱或路徑…"));

        let mut app = Self {
            settings_store,
            settings: settings.clone(),
            folder_path: None,
            items: Vec::new(),
            visible: Vec::new(),
            child_folders: Vec::new(),
            tree_children: HashMap::new(),
            tree_expanded: HashSet::new(),
            selected: BTreeSet::new(),
            selection_order: Vec::new(),
            history: FolderHistory::default(),
            status: "請選擇資料夾".into(),
            search: search.clone(),
            search_text: String::new(),
            sidebar_collapsed: settings.sidebar_collapsed,
            gallery_mode: GalleryMode::Grid,
            viewer: None,
            rename: None,
            drop_rename: None,
            drag: DragPhase::Idle,
            hover_path: None,
            viewer_canvas_bounds: None,
            viewer_panning: None,
            generation: 0,
            thumbs: HashMap::new(),
            thumb_pending: HashSet::new(),
            thumb_failed: HashSet::new(),
            thumbs_pump_scheduled: false,
            gallery_list: ListState::new(0, ListAlignment::Top, px(256.0)),
            viewport_rows: 0..0,
            focus_handle: cx.focus_handle(),
            viewer_focus: cx.focus_handle(),
            rename_focus: cx.focus_handle(),
            overlay_restore_focus: None,
            async_tasks: Vec::new(),
            _subscriptions: Vec::new(),
            shutting_down: false,
        };

        // Keep shell focused so global keybindings work after open.
        app.focus_handle.focus(window, cx);
        app.bind_gallery_scroll(cx);
        let bounds_sub = cx.observe_window_bounds(window, |this, window, _cx| {
            if this.shutting_down {
                return;
            }
            this.persist_window_size(window.bounds().size);
        });
        app._subscriptions.push(bounds_sub);

        let search_sub = cx.subscribe_in(&search, window, |this, state, event, _window, cx| {
            if this.shutting_down {
                return;
            }
            if matches!(event, InputEvent::Change) {
                this.search_text = state.read(cx).value().to_string();
                this.recompute_visible();
                this.sync_gallery_list();
                this.request_thumbs(cx);
                cx.notify();
            }
        });
        app._subscriptions.push(search_sub);

        // Cancel async work when this view is released (window close).
        let release = cx.on_release(|this, _cx| {
            this.shutting_down = true;
            this.generation = this.generation.wrapping_add(1);
            this.async_tasks.clear();
            this.thumb_pending.clear();
        });
        app._subscriptions.push(release);

        let restore = initial_folder.or(settings.last_folder_path.clone());
        if let Some(path) = restore {
            if PathBuf::from(&path).is_dir() {
                app.open_folder(path, true, false, cx);
            }
        }
        app
    }

    fn cancel_async_work(&mut self) {
        self.generation = self.generation.wrapping_add(1);
        self.async_tasks.clear();
        self.thumbs_pump_scheduled = false;
        self.thumb_pending.clear();
    }

    fn spawn_task(&mut self, task: Task<()>) {
        // Bound growth; completed tasks drop when replaced by generation cancel.
        if self.async_tasks.len() > 64 {
            self.async_tasks.drain(0..32);
        }
        self.async_tasks.push(task);
    }

    fn bind_gallery_scroll(&mut self, cx: &mut Context<Self>) {
        let entity = cx.weak_entity();
        self.gallery_list.set_scroll_handler(move |event, _window, cx| {
            let range = event.visible_range.clone();
            let _ = entity.update(cx, |this, cx| {
                if this.viewport_rows != range {
                    this.viewport_rows = range;
                    this.request_thumbs(cx);
                }
            });
        });
    }

    fn gallery_columns(&self) -> usize {
        if self.gallery_mode == GalleryMode::List {
            1
        } else {
            self.grid_columns_estimate().max(1)
        }
    }

    fn gallery_row_count(&self) -> usize {
        let cols = self.gallery_columns();
        if self.visible.is_empty() {
            0
        } else {
            self.visible.len().div_ceil(cols)
        }
    }

    fn gallery_row_height(&self) -> Pixels {
        if self.gallery_mode == GalleryMode::List {
            px(56.)
        } else {
            px(self.thumb_size() as f32 + 28.)
        }
    }

    fn sync_gallery_list(&mut self) {
        let rows = self.gallery_row_count();
        self.gallery_list
            .reset_with_uniform_height(rows, self.gallery_row_height());
        let first = rows.min(24);
        self.viewport_rows = 0..first;
    }

    fn viewport_item_range(&self) -> Range<usize> {
        item_range_for_rows(
            self.viewport_rows.clone(),
            self.gallery_columns(),
            self.visible.len(),
        )
    }

    fn capture_overlay_focus(&mut self, window: &mut Window, cx: &App) {
        self.overlay_restore_focus = window.focused(cx);
    }

    fn restore_overlay_focus(&mut self, window: &mut Window, cx: &mut Context<Self>) {
        if let Some(handle) = self.overlay_restore_focus.take() {
            handle.focus(window, cx);
        } else {
            self.focus_handle.focus(window, cx);
        }
    }

    fn focus_overlay_after_join(
        &mut self,
        handle: FocusHandle,
        window: &mut Window,
        cx: &mut Context<Self>,
    ) {
        cx.on_next_frame(window, move |_this, window, cx| {
            handle.focus(window, cx);
        });
    }

    fn persist_settings(&mut self) {
        if let Err(err) = self.settings_store.save(&self.settings) {
            warn(format!("settings save failed: {err}"));
        }
    }

    fn persist_sidebar(&mut self) {
        self.settings = apply_layout_persist(&self.settings, Some(self.sidebar_collapsed), None);
        self.persist_settings();
    }

    fn persist_window_size(&mut self, size: Size<Pixels>) {
        let width = f32::from(size.width).round().max(0.0) as u32;
        let height = f32::from(size.height).round().max(0.0) as u32;
        if self.settings.window_width == Some(width) && self.settings.window_height == Some(height)
        {
            return;
        }
        self.settings = apply_layout_persist(&self.settings, None, Some((width, height)));
        self.persist_settings();
    }

    fn clear_selection(&mut self) {
        clear_selection(&mut self.selected, &mut self.selection_order);
    }

    fn tree_roots_from_children(&mut self) {
        self.tree_children.clear();
        self.tree_expanded.clear();
    }

    fn tree_rows(&self) -> Vec<TreeRow> {
        visible_tree_rows(&self.child_folders, &self.tree_children, &self.tree_expanded)
    }

    fn toggle_tree_path(&mut self, path: String, cx: &mut Context<Self>) {
        match toggle_expand(&mut self.tree_expanded, &path) {
            ExpandAction::Collapse => cx.notify(),
            ExpandAction::NeedChildren => {
                if self.tree_children.contains_key(&path) {
                    cx.notify();
                    return;
                }
                self.load_tree_children(path, cx);
            }
        }
    }

    fn load_tree_children(&mut self, path: String, cx: &mut Context<Self>) {
        let gen = self.generation;
        let path_for_bg = path.clone();
        let task = cx.spawn(async move |this, cx| {
            let children = cx
                .background_spawn(async move {
                    scan_child_folders(&path_for_bg)
                        .unwrap_or_default()
                        .into_iter()
                        .map(|folder| folder.path)
                        .collect::<Vec<_>>()
                })
                .await;
            let _ = this.update(cx, |this, cx| {
                if this.shutting_down || this.generation != gen {
                    return;
                }
                apply_tree_children(&mut this.tree_children, &path, children);
                cx.notify();
            });
        });
        self.spawn_task(task);
    }

    fn recompute_visible(&mut self) {
        let q = self.search_text.trim().to_lowercase();
        if q.is_empty() {
            self.visible = self.items.clone();
            return;
        }
        self.visible = self
            .items
            .iter()
            .filter(|item| {
                item.name().to_lowercase().contains(&q) || item.path().to_lowercase().contains(&q)
            })
            .cloned()
            .collect();
    }

    fn thumb_size(&self) -> u32 {
        self.settings
            .thumbnail_size
            .max(DEFAULT_THUMBNAIL_SIZE)
            .max(80) as u32
    }

    /// Schedule a thumb pump after the current update stack unwinds.
    /// Never call from `render` — that re-enters entity locks and floods RefCell errors.
    fn request_thumbs(&mut self, cx: &mut Context<Self>) {
        if self.shutting_down || self.thumbs_pump_scheduled {
            return;
        }
        self.thumbs_pump_scheduled = true;
        let task = cx.spawn(async move |this, cx| {
            let _ = this.update(cx, |this, cx| {
                if this.shutting_down {
                    return;
                }
                this.thumbs_pump_scheduled = false;
                this.pump_thumbs(cx);
            });
        });
        self.spawn_task(task);
    }

    /// Queue background thumbnail work for viewport static images (bounded).
    fn pump_thumbs(&mut self, cx: &mut Context<Self>) {
        if self.shutting_down {
            return;
        }
        let size = self.thumb_size();
        let gen = self.generation;
        let mut cached_or_failed: HashSet<String> = self.thumbs.keys().cloned().collect();
        cached_or_failed.extend(self.thumb_failed.iter().cloned());
        let to_start = thumb_queue_update(
            &self.visible,
            self.viewport_item_range(),
            &cached_or_failed,
            &mut self.thumb_pending,
            MAX_THUMB_IN_FLIGHT,
        );

        for path in to_start {
            let path_for_bg = path.clone();
            let task = cx.spawn(async move |this, cx| {
                let result = cx
                    .background_spawn(async move { ensure_thumbnail(&path_for_bg, size) })
                    .await;
                let _ = this.update(cx, |this, cx| {
                    if this.shutting_down {
                        return;
                    }
                    this.thumb_pending.remove(&path);
                    if this.generation != gen {
                        return;
                    }
                    match result {
                        Ok(cache_path) => {
                            this.thumbs.insert(path, cache_path);
                        }
                        Err(err) => {
                            this.thumb_failed.insert(path.clone());
                            warn(format!("thumb failed for {path}: {err}"));
                        }
                    }
                    this.request_thumbs(cx);
                    cx.notify();
                });
            });
            self.spawn_task(task);
        }
    }

    fn open_folder(
        &mut self,
        path: String,
        remember_picker: bool,
        push_history: bool,
        cx: &mut Context<Self>,
    ) {
        self.cancel_async_work();
        self.folder_path = Some(path.clone());
        self.items.clear();
        self.visible.clear();
        self.child_folders.clear();
        self.tree_children.clear();
        self.tree_expanded.clear();
        self.clear_selection();
        self.viewer = None;
        self.rename = None;
        self.drop_rename = None;
        self.thumbs.clear();
        self.thumb_failed.clear();
        self.sync_gallery_list();
        if push_history {
            self.history.push(path.clone());
        }
        if remember_picker {
            self.settings.last_folder_path = Some(path.clone());
            self.persist_settings();
        }
        self.status = "載入中…".into();
        let gen = self.generation;
        let query = ListQuery {
            folder_path: path.clone(),
            include_subfolders: self.settings.include_subfolders,
            sort: self.settings.sort,
        };
        let path_for_bg = path.clone();
        let task = cx.spawn(async move |this, cx| {
            let outcome = cx
                .background_spawn(async move {
                    let items = scan_folder(&query);
                    let child_folders = match &items {
                        Ok(_) => scan_child_folders(&path_for_bg)
                            .unwrap_or_default()
                            .into_iter()
                            .map(|f| f.path)
                            .collect(),
                        Err(_) => Vec::new(),
                    };
                    (items, child_folders)
                })
                .await;
            let _ = this.update(cx, |this, cx| {
                if this.shutting_down {
                    return;
                }
                match outcome.0 {
                    Ok(items) => {
                        let applied = apply_folder_scan(
                            this.generation,
                            gen,
                            &mut this.items,
                            &mut this.child_folders,
                            FolderScanPayload {
                                items,
                                child_folders: outcome.1,
                            },
                        );
                        if !applied {
                            return;
                        }
                        this.tree_roots_from_children();
                        this.recompute_visible();
                        this.sync_gallery_list();
                        this.status = format!("已載入 {} 個項目", this.visible.len());
                        info(format!("opened folder: {path}"));
                        this.request_thumbs(cx);
                        cx.notify();
                    }
                    Err(err) => {
                        if this.generation != gen {
                            return;
                        }
                        this.status = format!("無法開啟資料夾：{err}");
                        warn(this.status.clone());
                        cx.notify();
                    }
                }
            });
        });
        self.spawn_task(task);
        cx.notify();
    }

    fn pick_folder(&mut self, _window: &mut Window, cx: &mut Context<Self>) {
        let receiver = cx.prompt_for_paths(PathPromptOptions {
            files: false,
            directories: true,
            multiple: false,
            prompt: Some("開啟資料夾".into()),
        });
        let task = cx.spawn(async move |this, cx| {
            match receiver.await {
                Ok(Ok(Some(paths))) => {
                    if let Some(path) = paths.into_iter().next() {
                        let path = path.to_string_lossy().replace('\\', "/");
                        let _ = this.update(cx, |this, cx| {
                            if this.shutting_down {
                                return;
                            }
                            this.open_folder(path, true, true, cx);
                        });
                    }
                }
                _ => {}
            }
        });
        self.spawn_task(task);
    }

    fn refresh(&mut self, cx: &mut Context<Self>) {
        if let Some(path) = self.folder_path.clone() {
            self.open_folder(path, false, false, cx);
        }
    }

    fn navigate_history(&mut self, back: bool, cx: &mut Context<Self>) {
        if let Some(path) = self.history.step(back).map(str::to_string) {
            self.open_folder(path, false, false, cx);
        }
    }

    fn toggle_include_subfolders(&mut self, cx: &mut Context<Self>) {
        self.settings.include_subfolders = !self.settings.include_subfolders;
        self.persist_settings();
        self.refresh(cx);
    }

    fn cycle_sort(&mut self, cx: &mut Context<Self>) {
        self.settings.sort = match (self.settings.sort.key, self.settings.sort.direction) {
            (SortKey::Name, SortDirection::Asc) => SortState {
                key: SortKey::Name,
                direction: SortDirection::Desc,
            },
            (SortKey::Name, SortDirection::Desc) => SortState {
                key: SortKey::ModifiedAt,
                direction: SortDirection::Asc,
            },
            (SortKey::ModifiedAt, SortDirection::Asc) => SortState {
                key: SortKey::ModifiedAt,
                direction: SortDirection::Desc,
            },
            (SortKey::ModifiedAt, SortDirection::Desc) => SortState {
                key: SortKey::Name,
                direction: SortDirection::Asc,
            },
        };
        self.persist_settings();
        self.refresh(cx);
    }

    fn sort_label(&self) -> &'static str {
        match (self.settings.sort.key, self.settings.sort.direction) {
            (SortKey::Name, SortDirection::Asc) => "名稱 ↑",
            (SortKey::Name, SortDirection::Desc) => "名稱 ↓",
            (SortKey::ModifiedAt, SortDirection::Asc) => "時間 ↑",
            (SortKey::ModifiedAt, SortDirection::Desc) => "時間 ↓",
        }
    }

    fn adjust_thumb_size(&mut self, delta: i32, cx: &mut Context<Self>) {
        let next = self.settings.thumbnail_size + delta;
        self.settings.thumbnail_size = piclens_domain::normalize_thumbnail_size(f64::from(next));
        self.persist_settings();
        self.cancel_async_work();
        self.thumbs.clear();
        self.thumb_failed.clear();
        self.sync_gallery_list();
        self.request_thumbs(cx);
        cx.notify();
    }

    fn select_path(&mut self, path: &str, additive: bool) {
        apply_selection(
            &mut self.selected,
            &mut self.selection_order,
            path,
            additive,
        );
    }

    fn selected_images(&self) -> Vec<ImageListItem> {
        self.selection_order
            .iter()
            .filter_map(|path| {
                self.items.iter().find_map(|item| match item {
                    ListItem::Image(img) if path_equals(&img.path, path) => Some(img.clone()),
                    _ => None,
                })
            })
            .collect()
    }

    fn visible_image_paths(&self) -> Vec<String> {
        self.visible
            .iter()
            .filter_map(|i| i.as_image().map(|img| img.path.clone()))
            .collect()
    }

    fn apply_batch(
        &mut self,
        label: &str,
        batch: &piclens_domain::FileOperationBatchResult,
        window: &mut Window,
        cx: &mut Context<Self>,
    ) {
        match batch_result_message(label, batch) {
            Some(message) => {
                self.status = message.clone();
                info(message.clone());
                if let Some(kind) = batch_notice_kind(batch) {
                    let note = match kind {
                        BatchNoticeKind::Success => Notification::success(message),
                        BatchNoticeKind::Warning => Notification::warning(message),
                        BatchNoticeKind::Error => Notification::error(message),
                    };
                    window.push_notification(note, cx);
                }
            }
            None => {
                self.status = format!("{label}：沒有可處理的項目");
            }
        }
    }

    fn open_viewer(&mut self, path: &str, window: &mut Window, cx: &mut Context<Self>) {
        let images: Vec<ImageListItem> = self
            .visible
            .iter()
            .filter_map(|item| item.as_image().cloned())
            .collect();
        let current_index = images
            .iter()
            .position(|img| path_equals(&img.path, path))
            .map(|i| i as i32)
            .unwrap_or(-1);
        if current_index < 0 {
            return;
        }
        let is_animated = images
            .get(current_index as usize)
            .map(|img| img.is_animated)
            .unwrap_or(false);
        let message = if is_animated {
            Some("此動畫圖片目前不支援預覽。".into())
        } else {
            None
        };
        let display_path = if is_animated {
            None
        } else {
            self.thumbs.get(path).cloned()
        };
        self.viewer = Some(ViewerState {
            sequence: ImageSequenceSnapshot {
                source_folder_path: self.folder_path.clone().unwrap_or_default(),
                include_subfolders: self.settings.include_subfolders,
                sort: self.settings.sort,
                images,
                current_index,
            },
            zoom: reset_zoom_state(),
            message,
            display_path,
        });
        if !is_animated {
            self.load_viewer_display(path.to_string(), cx);
        }
        self.capture_overlay_focus(window, cx);
        self.focus_overlay_after_join(self.viewer_focus.clone(), window, cx);
        cx.notify();
    }

    /// Decode a bounded safe PNG for the viewer (never feed raw corrupt files to `img`).
    fn load_viewer_display(&mut self, path: String, cx: &mut Context<Self>) {
        if self.shutting_down {
            return;
        }
        let gen = self.generation;
        let path_for_bg = path.clone();
        let path_for_ui = path;
        let task = cx.spawn(async move |this, cx| {
            let result = cx
                .background_spawn(async move { ensure_thumbnail(&path_for_bg, 1024) })
                .await;
            let _ = this.update(cx, |this, cx| {
                if this.shutting_down || this.generation != gen {
                    return;
                }
                let Some(viewer) = this.viewer.as_mut() else {
                    return;
                };
                let idx = viewer.sequence.current_index as usize;
                let current = viewer
                    .sequence
                    .images
                    .get(idx)
                    .map(|i| i.path.as_str())
                    .unwrap_or("");
                if !path_equals(current, &path_for_ui) {
                    return;
                }
                match result {
                    Ok(cache) => {
                        viewer.display_path = Some(cache);
                        viewer.message = None;
                    }
                    Err(err) => {
                        viewer.display_path = None;
                        viewer.message = Some(format!("無法載入圖片：{err}"));
                        warn(format!("viewer decode failed for {path_for_ui}: {err}"));
                    }
                }
                cx.notify();
            });
        });
        self.spawn_task(task);
    }

    fn close_viewer(&mut self, window: &mut Window, cx: &mut Context<Self>) {
        self.viewer = None;
        self.restore_overlay_focus(window, cx);
        cx.notify();
    }

    fn viewer_step(&mut self, delta: i32, cx: &mut Context<Self>) {
        let Some(viewer) = self.viewer.as_mut() else {
            return;
        };
        let len = viewer.sequence.images.len() as i32;
        if len == 0 {
            return;
        }
        let next = (viewer.sequence.current_index + delta).rem_euclid(len);
        viewer.sequence.current_index = next;
        viewer.zoom = reset_zoom_state();
        let (message, path) = viewer
            .sequence
            .images
            .get(next as usize)
            .map(|img| {
                if img.is_animated {
                    (Some("此動畫圖片目前不支援預覽。".into()), img.path.clone())
                } else {
                    (None, img.path.clone())
                }
            })
            .unwrap_or((None, String::new()));
        viewer.message = message.clone();
        viewer.display_path = if message.is_some() {
            None
        } else {
            self.thumbs.get(&path).cloned()
        };
        if message.is_none() && !path.is_empty() {
            self.load_viewer_display(path, cx);
        }
        cx.notify();
    }

    fn start_rename(&mut self, window: &mut Window, cx: &mut Context<Self>) {
        let images = self.selected_images();
        if images.len() != 1 {
            self.status = "重新命名僅適用單張選取圖片。".into();
            cx.notify();
            return;
        }
        let path = images[0].path.clone();
        let name = images[0].name.clone();
        let input = cx.new(|cx| InputState::new(window, cx).default_value(name));
        self.rename = Some(RenameState { path, input: input.clone() });
        self.capture_overlay_focus(window, cx);
        cx.on_next_frame(window, move |this, window, cx| {
            this.rename_focus.focus(window, cx);
            if let Some(draft) = this.rename.as_ref() {
                draft.input.update(cx, |state, cx| {
                    state.focus(window, cx);
                });
            }
        });
        cx.notify();
    }

    fn commit_rename(&mut self, window: &mut Window, cx: &mut Context<Self>) {
        let Some(draft) = self.rename.take() else {
            return;
        };
        self.restore_overlay_focus(window, cx);
        let new_name = draft.input.read(cx).value().to_string();
        let result = rename_image(&draft.path, &new_name);
        self.status = match result.status {
            piclens_domain::FileOperationStatus::Renamed => "已重新命名。".into(),
            piclens_domain::FileOperationStatus::Skipped => "重新命名已略過。".into(),
            _ => result
                .message
                .unwrap_or_else(|| "重新命名失敗。".into()),
        };
        self.refresh(cx);
    }

    /// Last selected image is the drop target; earlier selections are sources.
    fn plan_drop_rename_from_selection(&mut self, cx: &mut Context<Self>) {
        let images = self.selected_images();
        if images.len() < 2 {
            self.status = "請選取來源圖片，最後一張為目標圖片。".into();
            cx.notify();
            return;
        }
        let target = images.last().unwrap().path.clone();
        let sources: Vec<String> = images[..images.len() - 1]
            .iter()
            .map(|i| i.path.clone())
            .collect();
        let plan = plan_drop_rename(&sources, &target);
        if plan.items.is_empty() {
            self.status = "沒有可重新命名的項目。".into();
            cx.notify();
            return;
        }
        self.drop_rename = Some(plan);
        cx.notify();
    }

    fn commit_drop_rename(&mut self, window: &mut Window, cx: &mut Context<Self>) {
        let Some(plan) = self.drop_rename.take() else {
            return;
        };
        let batch = apply_drop_rename(&plan);
        self.apply_batch("拖放重新命名", &batch, window, cx);
        self.refresh(cx);
    }

    fn reveal_focus(&mut self, cx: &mut Context<Self>) {
        let path = self
            .selected_images()
            .first()
            .map(|i| i.path.clone())
            .or_else(|| {
                self.viewer.as_ref().and_then(|v| {
                    v.sequence
                        .images
                        .get(v.sequence.current_index as usize)
                        .map(|i| i.path.clone())
                })
            });
        match path {
            Some(path) => match reveal_in_file_manager(&path) {
                Ok(()) => self.status = "已在檔案管理器中顯示。".into(),
                Err(err) => {
                    self.status = format!("無法在檔案管理器顯示：{err}");
                    warn(self.status.clone());
                }
            },
            None => self.status = "請先選取圖片。".into(),
        }
        cx.notify();
    }

    // --- Keyboard action handlers ---

    fn on_open_folder(&mut self, _: &OpenFolder, window: &mut Window, cx: &mut Context<Self>) {
        self.pick_folder(window, cx);
    }

    fn on_refresh(&mut self, _: &Refresh, _: &mut Window, cx: &mut Context<Self>) {
        self.refresh(cx);
    }

    fn on_history_back(&mut self, _: &HistoryBack, _: &mut Window, cx: &mut Context<Self>) {
        if self.viewer.is_some() {
            return;
        }
        self.navigate_history(true, cx);
    }

    fn on_history_forward(&mut self, _: &HistoryForward, _: &mut Window, cx: &mut Context<Self>) {
        if self.viewer.is_some() {
            return;
        }
        self.navigate_history(false, cx);
    }

    fn on_toggle_sidebar(&mut self, _: &ToggleSidebar, _: &mut Window, cx: &mut Context<Self>) {
        self.sidebar_collapsed = !self.sidebar_collapsed;
        self.persist_sidebar();
        self.sync_gallery_list();
        self.request_thumbs(cx);
        cx.notify();
    }

    fn on_toggle_gallery_mode(
        &mut self,
        _: &ToggleGalleryMode,
        _: &mut Window,
        cx: &mut Context<Self>,
    ) {
        self.gallery_mode = match self.gallery_mode {
            GalleryMode::Grid => GalleryMode::List,
            GalleryMode::List => GalleryMode::Grid,
        };
        self.sync_gallery_list();
        self.request_thumbs(cx);
        cx.notify();
    }

    fn on_cycle_sort(&mut self, _: &CycleSort, _: &mut Window, cx: &mut Context<Self>) {
        self.cycle_sort(cx);
    }

    fn on_toggle_include_subfolders(
        &mut self,
        _: &ToggleIncludeSubfolders,
        _: &mut Window,
        cx: &mut Context<Self>,
    ) {
        self.toggle_include_subfolders(cx);
    }

    fn on_focus_search(&mut self, _: &FocusSearch, window: &mut Window, cx: &mut Context<Self>) {
        if self.viewer.is_some() || self.rename.is_some() || self.drop_rename.is_some() {
            return;
        }
        self.search.update(cx, |state, cx| {
            state.focus(window, cx);
        });
    }

    fn on_close_overlay(&mut self, _: &CloseOverlay, window: &mut Window, cx: &mut Context<Self>) {
        match next_escape_target(
            is_dragging(&self.drag) || !matches!(self.drag, DragPhase::Idle),
            self.drop_rename.is_some(),
            self.rename.is_some(),
            self.viewer.is_some(),
            !self.selected.is_empty(),
            !self.search_text.is_empty(),
        ) {
            EscapeTarget::Drag => {
                let _ = drag_cancel(std::mem::replace(&mut self.drag, DragPhase::Idle));
                cx.notify();
            }
            EscapeTarget::DropRename => {
                self.drop_rename = None;
                self.focus_handle.focus(window, cx);
                cx.notify();
            }
            EscapeTarget::Rename => {
                self.rename = None;
                self.restore_overlay_focus(window, cx);
                cx.notify();
            }
            EscapeTarget::Viewer => {
                self.close_viewer(window, cx);
            }
            EscapeTarget::Selection => {
                self.clear_selection();
                cx.notify();
            }
            EscapeTarget::Search => {
            self.search_text.clear();
            self.search.update(cx, |state, cx| {
                state.set_value("", window, cx);
            });
            self.recompute_visible();
            self.sync_gallery_list();
            self.request_thumbs(cx);
            self.focus_handle.focus(window, cx);
            cx.notify();
            }
            EscapeTarget::None => {}
        }
    }

    fn on_clear_selection(&mut self, _: &ClearSelection, _: &mut Window, cx: &mut Context<Self>) {
        if self.viewer.is_some() || self.rename.is_some() || self.drop_rename.is_some() {
            return;
        }
        self.clear_selection();
        cx.notify();
    }

    fn on_select_all(&mut self, _: &SelectAll, _: &mut Window, cx: &mut Context<Self>) {
        if self.viewer.is_some() {
            return;
        }
        self.clear_selection();
        for item in &self.visible {
            if let ListItem::Image(img) = item {
                self.selected.insert(img.path.clone());
                self.selection_order.push(img.path.clone());
            }
        }
        self.status = format!("已選取 {} 張圖片", self.selected.len());
        cx.notify();
    }

    fn on_open_viewer(&mut self, _: &OpenViewer, window: &mut Window, cx: &mut Context<Self>) {
        if self.viewer.is_some() || self.rename.is_some() || self.drop_rename.is_some() {
            return;
        }
        // Enter on a selected folder navigates into it.
        if let Some(path) = self.selection_order.last().cloned() {
            if self
                .visible
                .iter()
                .any(|item| item.is_folder() && path_equals(item.path(), &path))
            {
                self.open_folder(path, false, true, cx);
                self.focus_handle.focus(window, cx);
                return;
            }
        }
        if let Some(img) = self.selected_images().first() {
            let path = img.path.clone();
            self.open_viewer(&path, window, cx);
        } else if let Some(item) = self.visible.iter().find_map(|i| i.as_image()) {
            let path = item.path.clone();
            self.select_path(&path, false);
            self.open_viewer(&path, window, cx);
        } else {
            self.status = "請先選取圖片。".into();
            cx.notify();
        }
    }

    fn viewer_zoom_is_fit(&self) -> bool {
        self.viewer
            .as_ref()
            .map(|v| is_fit_view(v.zoom.zoom, v.zoom.offset))
            .unwrap_or(true)
    }

    fn on_viewer_prev(&mut self, _: &ViewerPrev, _: &mut Window, cx: &mut Context<Self>) {
        match page_key_outcome(
            self.viewer.is_some(),
            self.visible.len(),
            self.current_visible_index(),
            self.gallery_columns(),
            self.page_rows(),
            false,
        ) {
            PageKeyOutcome::ViewerStep(delta) => {
                if self.viewer_zoom_is_fit() {
                    self.viewer_step(delta, cx);
                }
            }
            PageKeyOutcome::Gallery(index) => self.select_visible_index(index, cx),
        }
    }

    fn on_viewer_next(&mut self, _: &ViewerNext, _: &mut Window, cx: &mut Context<Self>) {
        match page_key_outcome(
            self.viewer.is_some(),
            self.visible.len(),
            self.current_visible_index(),
            self.gallery_columns(),
            self.page_rows(),
            true,
        ) {
            PageKeyOutcome::ViewerStep(delta) => {
                if self.viewer_zoom_is_fit() {
                    self.viewer_step(delta, cx);
                }
            }
            PageKeyOutcome::Gallery(index) => self.select_visible_index(index, cx),
        }
    }

    fn current_visible_index(&self) -> Option<usize> {
        self.selection_order.last().and_then(|path| {
            self.visible
                .iter()
                .position(|item| path_equals(item.path(), path))
        })
    }

    fn page_rows(&self) -> usize {
        self.viewport_rows.len().max(1)
    }

    fn select_visible_index(&mut self, index: Option<usize>, cx: &mut Context<Self>) {
        let Some(index) = index else {
            return;
        };
        let Some(item) = self.visible.get(index) else {
            return;
        };
        let path = item.path().to_string();
        self.select_path(&path, false);
        self.gallery_list.scroll_to_reveal_item(index / self.gallery_columns().max(1));
        cx.notify();
    }

    fn on_gallery_home(&mut self, _: &GalleryHome, _: &mut Window, cx: &mut Context<Self>) {
        if self.viewer.is_some() {
            return;
        }
        let index = gallery_jump_index(
            self.visible.len(),
            self.current_visible_index(),
            self.gallery_columns(),
            self.page_rows(),
            GalleryJump::Home,
        );
        self.select_visible_index(index, cx);
    }

    fn on_gallery_end(&mut self, _: &GalleryEnd, _: &mut Window, cx: &mut Context<Self>) {
        if self.viewer.is_some() {
            return;
        }
        let index = gallery_jump_index(
            self.visible.len(),
            self.current_visible_index(),
            self.gallery_columns(),
            self.page_rows(),
            GalleryJump::End,
        );
        self.select_visible_index(index, cx);
    }

    fn on_zoom_in(&mut self, _: &ZoomIn, _: &mut Window, cx: &mut Context<Self>) {
        if let Some(v) = self.viewer.as_mut() {
            v.zoom.zoom = clamp_zoom(v.zoom.zoom * 1.2);
            cx.notify();
        } else {
            self.adjust_thumb_size(20, cx);
        }
    }

    fn on_zoom_out(&mut self, _: &ZoomOut, _: &mut Window, cx: &mut Context<Self>) {
        if let Some(v) = self.viewer.as_mut() {
            v.zoom.zoom = clamp_zoom(v.zoom.zoom / 1.2);
            cx.notify();
        } else {
            self.adjust_thumb_size(-20, cx);
        }
    }

    fn on_zoom_reset(&mut self, _: &ZoomReset, _: &mut Window, cx: &mut Context<Self>) {
        if let Some(v) = self.viewer.as_mut() {
            v.zoom = reset_zoom_state();
            cx.notify();
        }
    }

    fn on_trash(&mut self, _: &TrashSelection, window: &mut Window, cx: &mut Context<Self>) {
        if self.viewer.is_some() {
            // Trash current viewer image
            if let Some(viewer) = self.viewer.as_ref() {
                let idx = viewer.sequence.current_index as usize;
                if let Some(img) = viewer.sequence.images.get(idx) {
                    let paths = vec![img.path.clone()];
                    let batch = trash_paths(&paths);
                    self.apply_batch("移至回收筒", &batch, window, cx);
                    self.close_viewer(window, cx);
                    self.refresh(cx);
                    return;
                }
            }
        }
        let paths: Vec<String> = self.selected_images().into_iter().map(|i| i.path).collect();
        if paths.is_empty() {
            self.status = "請先選取圖片。".into();
            cx.notify();
            return;
        }
        let batch = trash_paths(&paths);
        self.apply_batch("移至回收筒", &batch, window, cx);
        self.refresh(cx);
    }

    fn on_rename(&mut self, _: &RenameSelection, window: &mut Window, cx: &mut Context<Self>) {
        if self.viewer.is_some() {
            return;
        }
        self.start_rename(window, cx);
    }

    fn on_drop_rename(&mut self, _: &DropRenamePlan, _: &mut Window, cx: &mut Context<Self>) {
        if self.viewer.is_some() {
            return;
        }
        self.plan_drop_rename_from_selection(cx);
    }

    fn on_convert_jpg(&mut self, _: &ConvertJpg, window: &mut Window, cx: &mut Context<Self>) {
        let paths = self.visible_image_paths();
        let batch = convert_to_jpg(&paths);
        self.apply_batch("轉 JPG", &batch, window, cx);
        self.refresh(cx);
    }

    fn on_convert_webp(&mut self, _: &ConvertWebp, window: &mut Window, cx: &mut Context<Self>) {
        let paths = self.visible_image_paths();
        let batch = convert_to_lossless_webp(&paths);
        self.apply_batch("轉 WebP", &batch, window, cx);
        self.refresh(cx);
    }

    fn on_cleanup(&mut self, _: &CleanupSameBasename, window: &mut Window, cx: &mut Context<Self>) {
        let paths = self.visible_image_paths();
        let batch = cleanup_same_basename(&paths);
        self.apply_batch("清除同名格式", &batch, window, cx);
        self.refresh(cx);
    }

    fn on_reveal(&mut self, _: &RevealInFileManager, _: &mut Window, cx: &mut Context<Self>) {
        self.reveal_focus(cx);
    }

    fn move_selection(&mut self, delta: i32, cx: &mut Context<Self>) {
        if self.viewer.is_some() || self.visible.is_empty() {
            return;
        }
        let current = self
            .selection_order
            .last()
            .and_then(|p| {
                self.visible
                    .iter()
                    .position(|item| path_equals(item.path(), p))
            })
            .unwrap_or(usize::MAX);
        let len = self.visible.len() as i32;
        let next = if current == usize::MAX {
            if delta >= 0 {
                0
            } else {
                len - 1
            }
        } else {
            (current as i32 + delta).clamp(0, len - 1)
        } as usize;
        let path = self.visible[next].path().to_string();
        self.clear_selection();
        if self.visible[next].is_folder() {
            // Highlight folder by selecting path in order only for navigation feedback
            self.selected.insert(path.clone());
            self.selection_order.push(path);
        } else {
            self.select_path(&path, false);
        }
        cx.notify();
    }

    fn on_move_up(&mut self, _: &MoveSelectionUp, _: &mut Window, cx: &mut Context<Self>) {
        if self.viewer.is_some() {
            return;
        }
        let step = if self.gallery_mode == GalleryMode::Grid {
            self.grid_columns_estimate().max(1) as i32
        } else {
            1
        };
        self.move_selection(-step, cx);
    }

    fn on_move_down(&mut self, _: &MoveSelectionDown, _: &mut Window, cx: &mut Context<Self>) {
        if self.viewer.is_some() {
            return;
        }
        let step = if self.gallery_mode == GalleryMode::Grid {
            self.grid_columns_estimate().max(1) as i32
        } else {
            1
        };
        self.move_selection(step, cx);
    }

    fn on_move_left(&mut self, _: &MoveSelectionLeft, _: &mut Window, cx: &mut Context<Self>) {
        if self.viewer.is_some() {
            if self.viewer_zoom_is_fit() {
                self.viewer_step(-1, cx);
            }
            return;
        }
        self.move_selection(-1, cx);
    }

    fn on_move_right(&mut self, _: &MoveSelectionRight, _: &mut Window, cx: &mut Context<Self>) {
        if self.viewer.is_some() {
            if self.viewer_zoom_is_fit() {
                self.viewer_step(1, cx);
            }
            return;
        }
        self.move_selection(1, cx);
    }

    fn begin_image_drag(&mut self, path: &str, origin: (f64, f64)) {
        let sources = if self.selected.contains(path) {
            self.selected_images()
                .into_iter()
                .map(|image| image.path)
                .collect()
        } else {
            vec![path.to_string()]
        };
        self.drag = drag_begin(origin, sources);
    }

    fn on_shell_mouse_move(
        &mut self,
        event: &MouseMoveEvent,
        _window: &mut Window,
        cx: &mut Context<Self>,
    ) {
        if matches!(self.drag, DragPhase::Idle) {
            return;
        }
        let pointer = (
            f64::from(event.position.x),
            f64::from(event.position.y),
        );
        self.drag = drag_move(self.drag.clone(), pointer, self.hover_path.clone());
        cx.notify();
    }

    fn on_shell_mouse_up(
        &mut self,
        _: &MouseUpEvent,
        _window: &mut Window,
        cx: &mut Context<Self>,
    ) {
        let finish = drag_finish(std::mem::replace(&mut self.drag, DragPhase::Idle));
        match finish {
            DragFinish::Confirm { sources, target } => {
                let plan = plan_drop_rename(&sources, &target);
                if plan.items.is_empty() {
                    self.status = "沒有可重新命名的項目。".into();
                } else {
                    self.drop_rename = Some(plan);
                }
                cx.notify();
            }
            DragFinish::Cancel => cx.notify(),
            DragFinish::Ignore => {}
        }
    }

    fn apply_viewer_wheel(
        &mut self,
        event: &ScrollWheelEvent,
        cx: &mut Context<Self>,
    ) {
        let Some(viewer) = self.viewer.as_mut() else {
            return;
        };
        let delta = match event.delta {
            ScrollDelta::Lines(lines) => {
                if lines.y == 0.0 {
                    return;
                }
                if lines.y > 0.0 {
                    1
                } else {
                    -1
                }
            }
            ScrollDelta::Pixels(pixels) => {
                let y = f32::from(pixels.y);
                if y == 0.0 {
                    return;
                }
                if y > 0.0 {
                    1
                } else {
                    -1
                }
            }
        };
        let pointer = Point {
            x: f64::from(event.position.x),
            y: f64::from(event.position.y),
        };
        let viewport_center = self
            .viewer_canvas_bounds
            .map(|bounds| Point {
                x: f64::from(bounds.origin.x + bounds.size.width / 2.),
                y: f64::from(bounds.origin.y + bounds.size.height / 2.),
            })
            .unwrap_or(pointer);
        viewer.zoom = zoom_at_point(
            viewer.zoom.zoom,
            viewer.zoom.offset,
            viewport_center,
            pointer,
            delta,
        );
        cx.notify();
    }

    fn begin_viewer_pan(&mut self, event: &MouseDownEvent) {
        if self.viewer.as_ref().map(|v| v.zoom.zoom > 1.01).unwrap_or(false) {
            self.viewer_panning = Some(Point {
                x: f64::from(event.position.x),
                y: f64::from(event.position.y),
            });
        }
    }

    fn move_viewer_pan(&mut self, event: &MouseMoveEvent, cx: &mut Context<Self>) {
        let Some(last) = self.viewer_panning else {
            return;
        };
        let Some(viewer) = self.viewer.as_mut() else {
            return;
        };
        let now = Point {
            x: f64::from(event.position.x),
            y: f64::from(event.position.y),
        };
        viewer.zoom.offset = pan_offset(
            viewer.zoom.offset,
            Point {
                x: now.x - last.x,
                y: now.y - last.y,
            },
        );
        self.viewer_panning = Some(now);
        cx.notify();
    }

    fn end_viewer_pan(&mut self) {
        self.viewer_panning = None;
    }

    fn grid_columns_estimate(&self) -> usize {
        let tile = self.thumb_size() as usize + 16;
        // Assume ~960px gallery width when sidebar open
        let width = if self.sidebar_collapsed { 1200 } else { 960 };
        (width / tile).max(1)
    }
}
