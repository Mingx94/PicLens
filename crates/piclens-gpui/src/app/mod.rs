//! Main window: library, thumbs, sidebar, selection, viewer, file operations.
//!
//! Render is split across child modules so the shell stays readable.
//! State and commands stay here.

mod gallery;
mod overlays;
mod render;
mod shell;
mod viewer;

use std::cell::Cell;
use std::collections::{BTreeSet, HashMap, HashSet};
use std::ops::Range;
use std::path::PathBuf;
use std::rc::Rc;
use std::sync::Arc;
use std::time::{Duration, Instant};

use viewer::ViewerLoader;

use gpui::*;
use gpui_component::button::{Button, ButtonVariant};
use gpui_component::dialog::DialogButtonProps;
use gpui_component::input::{InputEvent, InputState};
use gpui_component::notification::Notification;
use gpui_component::{Selectable, WindowExt};
use piclens_domain::{
    apply_layout_persist, clamp_zoom, is_fit_view, pan_offset, path_equals, reset_zoom_state,
    zoom_at_point, AppSettings, DropTargetBatchRenamePlan, FileOperationBatchResult, ImageListItem,
    ImageSequenceSnapshot, ListItem, ListQuery, Point, SortDirection, SortKey, SortState,
    ZoomState, DEFAULT_THUMBNAIL_SIZE, MIN_WINDOW_WIDTH,
};
use piclens_infra::{
    apply_drop_rename_cancellable, cleanup_same_basename_cancellable, convert_to_jpg_cancellable,
    convert_to_lossless_webp_cancellable, ensure_thumbnail_with_timeout, info, plan_drop_rename,
    prune_thumbnail_cache_if_needed, rename_image_cancellable, reveal_in_file_manager,
    scan_child_folders_cancellable, scan_folder_cancellable, trash_paths_cancellable, warn,
    CancellationToken, JsonSettingsStore, ScanError,
};

use crate::actions::{
    CancelFileOperation, CleanupSameBasename, ClearSelection, CloseOverlay, ConvertJpg,
    ConvertWebp, CycleSort, DropRenamePlan, FocusSearch, GalleryEnd, GalleryHome, HistoryBack,
    HistoryForward, MoveSelectionDown, MoveSelectionLeft, MoveSelectionRight, MoveSelectionUp,
    OpenFolder, OpenViewer, PrepareShutdown, Refresh, RenameSelection, RevealInFileManager,
    SelectAll, SortModifiedAscending, SortModifiedDescending, SortNameAscending,
    SortNameDescending, ToggleGalleryMode, ToggleIncludeSubfolders, ToggleSidebar, TrashSelection,
    ViewerNext, ViewerPrev, ZoomIn, ZoomOut, ZoomReset,
};
use crate::diagnostics::RuntimeMetrics;
use crate::drag_rename::{
    drag_begin, drag_cancel, drag_finish, drag_move, drag_sources_exist, edge_autoscroll_step,
    is_dragging, DragFinish, DragPhase,
};
use crate::folder_tree::{
    apply_tree_children, replace_tree_for_picker, toggle_expand, visible_tree_rows, ExpandAction,
    TreeRow,
};
use crate::history::FolderHistory;
use crate::interaction::{
    apply_selection, batch_notice_kind, batch_result_message, clear_selection, gallery_jump_index,
    next_escape_target, page_key_outcome, reconcile_selection, BatchNoticeKind, EscapeTarget,
    GalleryJump, PageKeyOutcome, SelectionGesture,
};
use crate::scan_apply::{apply_folder_scan, FolderScanPayload};
use crate::thumbs::{grid_column_count, item_range_for_rows, thumb_queue_update};

/// Keep an icon-only visual while giving gpui-component's Button a UIA name.
fn accessible_icon_button(
    id: impl Into<ElementId>,
    label: impl Into<SharedString>,
    icon: impl IntoElement,
) -> Button {
    Button::new(id)
        .label(label)
        // gpui-component renders labels and re-colors them on hover. Its
        // selected visual path skips that hover foreground override; the
        // refinement below then keeps the UIA label visually hidden.
        .selected(true)
        .relative()
        .size(px(32.0))
        .overflow_hidden()
        .bg(transparent_black())
        .text_color(transparent_black())
        .child(
            div()
                .absolute()
                .inset_0()
                .flex()
                .items_center()
                .justify_center()
                .child(icon),
        )
}

const GRID_GAP: f32 = 12.0;
const GRID_PAD: f32 = 8.0;
const DIALOG_MARGIN: f32 = 16.0;

#[derive(Clone, Copy)]
struct AdaptiveLayout {
    compact: bool,
    minimum: bool,
}

fn adaptive_layout(width: f32) -> AdaptiveLayout {
    AdaptiveLayout {
        compact: width < 980.0,
        minimum: width <= MIN_WINDOW_WIDTH as f32,
    }
}

fn fitted_dialog_width(viewport_width: f32, preferred: f32) -> f32 {
    (viewport_width - DIALOG_MARGIN * 2.0).clamp(280.0, preferred)
}

fn trash_confirmation_description(count: usize) -> String {
    format!("將 {count} 張圖片移至作業系統回收筒。取消不會修改檔案。")
}

fn cleanup_confirmation_description(count: usize) -> String {
    format!(
        "將檢查目前結果的 {count} 張圖片。JPG/JPEG 與 WebP 會保留；其他同名格式會移至作業系統回收筒。取消不會修改檔案。"
    )
}

const CONVERSION_CONFIRMATION_THRESHOLD: usize = 50;
const MAX_THUMB_IN_FLIGHT: usize = 8;
const THUMBNAIL_TIMEOUT: Duration = Duration::from_secs(15);

#[derive(Clone, Copy)]
enum ConversionKind {
    Jpg,
    Webp,
}

impl ConversionKind {
    fn label(self) -> &'static str {
        match self {
            Self::Jpg => "轉 JPG",
            Self::Webp => "轉 WebP",
        }
    }

    fn confirmation_description(self, count: usize) -> String {
        match self {
            Self::Jpg => format!(
                "將處理目前結果的 {count} 張圖片並轉為 JPG。原始檔案會保留，且不會覆寫既有目標檔。取消不會修改檔案。"
            ),
            Self::Webp => format!(
                "將處理目前結果的 {count} 張圖片並轉為無損 WebP。原始檔案會保留；JPG/JPEG、WebP 與動畫圖片會略過，且不會覆寫既有目標檔。取消不會修改檔案。"
            ),
        }
    }
}

struct BackgroundFileOperationConfirmation {
    label: &'static str,
    description: String,
    ok_text: &'static str,
    ok_variant: ButtonVariant,
    kind: BackgroundFileOperationKind,
}

#[derive(Clone, Copy)]
enum BackgroundFileOperationKind {
    Convert(ConversionKind),
    Cleanup,
}

fn conversion_requires_confirmation(count: usize) -> bool {
    count >= CONVERSION_CONFIRMATION_THRESHOLD
}

#[derive(Clone, Copy, PartialEq, Eq)]
enum GalleryMode {
    Grid,
    List,
}

#[derive(Clone, Default)]
pub struct LaunchOptions {
    pub include_subfolders: bool,
    pub search: Option<String>,
    pub list_view: bool,
    pub sidebar_closed: bool,
    pub viewer: Option<String>,
    pub performance_scroll: bool,
    pub metrics: Option<Arc<RuntimeMetrics>>,
}

pub struct PicLensApp {
    settings_store: Arc<JsonSettingsStore>,
    settings: AppSettings,
    settings_authority: AppSettings,
    folder_path: Option<String>,
    items: Vec<ListItem>,
    visible: Vec<ListItem>,
    tree_roots: Vec<String>,
    tree_root: Option<String>,
    tree_children: HashMap<String, Vec<String>>,
    tree_expanded: HashSet<String>,
    tree_generation: u64,
    tree_motion_path: Option<String>,
    tree_motion_revision: u64,
    selected: BTreeSet<String>,
    selection_order: Vec<String>,
    selection_anchor: Option<String>,
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
    thumb_cancellations: HashMap<String, CancellationToken>,
    thumb_failed: HashSet<String>,
    /// Prevents stacking concurrent thumb pump tasks.
    thumbs_pump_scheduled: bool,
    gallery_list: ListState,
    gallery_width: f32,
    gallery_bounds: Option<Bounds<Pixels>>,
    viewport_rows: Range<usize>,
    drag_autoscroll_step: f32,
    drag_autoscroll_running: bool,
    pending_scroll_restore: Option<(String, ListOffset)>,
    performance_scroll_requested: bool,
    performance_scroll_running: bool,
    metrics: Option<Arc<RuntimeMetrics>>,
    native_menu_snapshot: Option<(u8, bool, usize, bool)>,
    focus_handle: FocusHandle,
    viewer_focus: FocusHandle,
    viewer_title_focus: FocusHandle,
    rename_focus: FocusHandle,
    overlay_restore_focus: Option<FocusHandle>,
    /// Held so tasks cancel on drop / generation bump (do not detach).
    async_tasks: Vec<Task<()>>,
    thumbnail_cache_task: Option<Task<()>>,
    tree_tasks: Vec<Task<()>>,
    scan_cancellation: Option<CancellationToken>,
    tree_cancellations: Vec<CancellationToken>,
    viewer_loader: ViewerLoader,
    file_operation_task: Option<Task<()>>,
    file_operation_label: Option<&'static str>,
    file_operation_cancellation: Option<CancellationToken>,
    file_operation_generation: u64,
    batch_report: Option<BatchReport>,
    batch_report_list: ListState,
    _subscriptions: Vec<Subscription>,
    /// Set on release so late callbacks skip UI updates.
    shutting_down: bool,
}

struct ViewerState {
    sequence: ImageSequenceSnapshot,
    zoom: ZoomState,
    message: Option<String>,
    /// Safe gallery PNG shown while the sharper viewer preview loads.
    display_path: Option<PathBuf>,
    display_image: Option<Arc<RenderImage>>,
    load_started: Instant,
    paint_recorded: Rc<Cell<bool>>,
}

struct RenameState {
    path: String,
    input: Entity<InputState>,
}

struct BatchReport {
    label: String,
    batch: FileOperationBatchResult,
}

impl PicLensApp {
    pub fn new(
        window: &mut Window,
        cx: &mut Context<Self>,
        initial_folder: Option<String>,
        launch: LaunchOptions,
    ) -> Self {
        let mut app = Self::new_with_settings_store(
            window,
            cx,
            initial_folder,
            launch,
            Arc::new(JsonSettingsStore::new()),
        );
        // One parent task owns cleanup. Workers can return PNG paths without
        // waiting for a directory scan; clean intervals do no filesystem work.
        let executor = cx.background_executor().clone();
        app.thumbnail_cache_task = Some(executor.clone().spawn(async move {
            loop {
                prune_thumbnail_cache_if_needed();
                executor.timer(Duration::from_secs(5)).await;
            }
        }));
        app
    }

    fn new_with_settings_store(
        window: &mut Window,
        cx: &mut Context<Self>,
        initial_folder: Option<String>,
        launch: LaunchOptions,
        settings_store: Arc<JsonSettingsStore>,
    ) -> Self {
        let stored_settings = settings_store.load();
        let mut settings = stored_settings.clone();
        if launch.include_subfolders {
            settings.include_subfolders = true;
        }
        if launch.sidebar_closed {
            settings.sidebar_collapsed = true;
        }
        let search = cx.new(|cx| InputState::new(window, cx).placeholder("搜尋名稱或路徑…"));
        if let Some(value) = &launch.search {
            search.update(cx, |state, cx| state.set_value(value, window, cx));
        }

        let mut app = Self {
            settings_store,
            settings: settings.clone(),
            settings_authority: stored_settings.clone(),
            folder_path: None,
            items: Vec::new(),
            visible: Vec::new(),
            tree_roots: Vec::new(),
            tree_root: None,
            tree_children: HashMap::new(),
            tree_expanded: HashSet::new(),
            tree_generation: 0,
            tree_motion_path: None,
            tree_motion_revision: 0,
            selected: BTreeSet::new(),
            selection_order: Vec::new(),
            selection_anchor: None,
            history: FolderHistory::default(),
            status: "請選擇資料夾".into(),
            search: search.clone(),
            search_text: launch.search.clone().unwrap_or_default(),
            sidebar_collapsed: settings.sidebar_collapsed,
            gallery_mode: if launch.list_view {
                GalleryMode::List
            } else {
                GalleryMode::Grid
            },
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
            thumb_cancellations: HashMap::new(),
            thumb_failed: HashSet::new(),
            thumbs_pump_scheduled: false,
            gallery_list: ListState::new(0, ListAlignment::Top, px(256.0)),
            gallery_width: 0.0,
            gallery_bounds: None,
            viewport_rows: 0..0,
            drag_autoscroll_step: 0.0,
            drag_autoscroll_running: false,
            pending_scroll_restore: None,
            performance_scroll_requested: launch.performance_scroll,
            performance_scroll_running: false,
            metrics: launch.metrics.clone(),
            native_menu_snapshot: None,
            focus_handle: cx.focus_handle(),
            viewer_focus: cx.focus_handle(),
            viewer_title_focus: cx.focus_handle(),
            rename_focus: cx.focus_handle(),
            overlay_restore_focus: None,
            async_tasks: Vec::new(),
            thumbnail_cache_task: None,
            tree_tasks: Vec::new(),
            scan_cancellation: None,
            tree_cancellations: Vec::new(),
            viewer_loader: ViewerLoader::default(),
            file_operation_task: None,
            file_operation_label: None,
            file_operation_cancellation: None,
            file_operation_generation: 0,
            batch_report: None,
            batch_report_list: ListState::new(0, ListAlignment::Top, px(104.0)),
            _subscriptions: Vec::new(),
            shutting_down: false,
        };
        if let Some(metrics) = &app.metrics {
            let size = window.bounds().size;
            metrics.window_ready(
                f32::from(size.width).round().max(0.0) as u32,
                f32::from(size.height).round().max(0.0) as u32,
                window.scale_factor(),
            );
            if launch.search.is_some() {
                metrics.search_applied();
            }
        }

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
        let activation_sub = cx.observe_window_activation(window, |this, window, cx| {
            if !window.is_window_active() {
                this.cleanup_drag_session(cx);
            }
        });
        app._subscriptions.push(activation_sub);

        let search_sub = cx.subscribe_in(&search, window, |this, state, event, _window, cx| {
            if this.shutting_down {
                return;
            }
            if matches!(event, InputEvent::Change) {
                this.search_text = state.read(cx).value().to_string();
                if let Some(metrics) = &this.metrics {
                    metrics.search_applied();
                }
                this.recompute_visible();
                this.reconcile_selection();
                this.sync_gallery_list();
                this.request_thumbs(cx);
                cx.notify();
            }
        });
        app._subscriptions.push(search_sub);

        // Cancel async work when this view is released (window close).
        let release = cx.on_release(|this, cx| this.prepare_shutdown(cx));
        app._subscriptions.push(release);

        let has_folder_override = initial_folder.is_some();
        let restore = initial_folder.or(stored_settings.last_folder_path.clone());
        if let Some(path) = restore {
            if PathBuf::from(&path).is_dir() {
                app.open_folder(path, true, !has_folder_override, false, cx);
            }
        }
        if let Some(viewer_path) = launch.viewer {
            let task = cx.spawn_in(window, async move |this, cx| {
                for _ in 0..100 {
                    cx.background_executor()
                        .timer(Duration::from_millis(50))
                        .await;
                    let opened = this
                        .update_in(cx, |this, window, cx| {
                            if this.shutting_down {
                                return true;
                            }
                            if this.items.iter().any(|item| {
                                item.as_image()
                                    .is_some_and(|image| path_equals(&image.path, &viewer_path))
                            }) {
                                this.open_viewer(&viewer_path, window, cx);
                                return true;
                            }
                            false
                        })
                        .unwrap_or(true);
                    if opened {
                        break;
                    }
                }
            });
            app.spawn_task(task);
        }
        app
    }

    fn prepare_shutdown(&mut self, cx: &mut App) {
        if self.shutting_down {
            return;
        }
        self.shutting_down = true;
        self.generation = self.generation.wrapping_add(1);
        self.tree_generation = self.tree_generation.wrapping_add(1);
        if let Some(token) = self.scan_cancellation.take() {
            token.cancel();
        }
        for token in self.thumb_cancellations.values() {
            token.cancel();
        }
        for token in self.tree_cancellations.drain(..) {
            token.cancel();
        }
        self.viewer_loader.cancel(cx);
        if let Some(token) = self.file_operation_cancellation.take() {
            token.cancel();
        }
        self.async_tasks.clear();
        self.thumbnail_cache_task = None;
        self.tree_tasks.clear();
        self.file_operation_task = None;
    }

    fn cancel_async_work(&mut self) {
        self.generation = self.generation.wrapping_add(1);
        if let Some(token) = self.scan_cancellation.take() {
            token.cancel();
        }
        // The immutable viewer can remain open across a gallery generation.
        // Keep its decoded pixels owned until navigation or close evicts them.
        self.viewer_loader.cancel_request();
        for token in self.thumb_cancellations.values() {
            token.cancel();
        }
        self.thumbs_pump_scheduled = false;
    }

    fn spawn_task(&mut self, task: Task<()>) {
        // Bound growth; completed tasks drop when replaced by generation cancel.
        if self.async_tasks.len() > 64 {
            self.async_tasks.drain(0..32);
        }
        self.async_tasks.push(task);
    }

    fn spawn_tree_task(&mut self, task: Task<()>) {
        if self.tree_tasks.len() > 32 {
            self.tree_tasks.drain(0..16);
        }
        self.tree_tasks.push(task);
    }

    fn cancel_tree_work(&mut self) {
        self.tree_generation = self.tree_generation.wrapping_add(1);
        for token in self.tree_cancellations.drain(..) {
            token.cancel();
        }
        self.tree_tasks.clear();
    }

    fn bind_gallery_scroll(&mut self, cx: &mut Context<Self>) {
        let entity = cx.weak_entity();
        self.gallery_list
            .set_scroll_handler(move |event, _window, cx| {
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

    fn apply_gallery_width(&mut self, width: f32, cx: &mut Context<Self>) {
        if width <= 0.0 || (self.gallery_width - width).abs() < 0.5 {
            return;
        }
        let before = self.gallery_columns();
        self.gallery_width = width;
        if self.gallery_columns() != before {
            self.sync_gallery_list();
            self.request_thumbs(cx);
            cx.notify();
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

    fn persist_settings_authority(&mut self) {
        if let Err(err) = self.settings_store.save(&self.settings_authority) {
            warn(format!("settings save failed: {err}"));
        }
    }

    fn persist_sidebar(&mut self) {
        self.settings = apply_layout_persist(&self.settings, Some(self.sidebar_collapsed), None);
        self.settings_authority =
            apply_layout_persist(&self.settings_authority, Some(self.sidebar_collapsed), None);
        self.persist_settings_authority();
    }

    fn persist_window_size(&mut self, size: Size<Pixels>) {
        let width = f32::from(size.width).round().max(0.0) as u32;
        let height = f32::from(size.height).round().max(0.0) as u32;
        if self.settings.window_width == Some(width) && self.settings.window_height == Some(height)
        {
            return;
        }
        self.settings = apply_layout_persist(&self.settings, None, Some((width, height)));
        self.settings_authority =
            apply_layout_persist(&self.settings_authority, None, Some((width, height)));
        self.persist_settings_authority();
    }

    fn clear_selection(&mut self) {
        clear_selection(
            &mut self.selected,
            &mut self.selection_order,
            &mut self.selection_anchor,
        );
    }

    fn tree_rows(&self) -> Vec<TreeRow> {
        visible_tree_rows(&self.tree_roots, &self.tree_children, &self.tree_expanded)
    }

    fn toggle_tree_path(&mut self, path: String, cx: &mut Context<Self>) {
        self.tree_motion_path = Some(path.clone());
        self.tree_motion_revision = self.tree_motion_revision.wrapping_add(1);
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
        let gen = self.tree_generation;
        let cancellation = CancellationToken::new();
        self.tree_cancellations.push(cancellation.clone());
        let path_for_bg = path.clone();
        let task = cx.spawn(async move |this, cx| {
            let children = cx
                .background_spawn(async move {
                    scan_child_folders_cancellable(&path_for_bg, &cancellation)
                        .unwrap_or_default()
                        .into_iter()
                        .map(|folder| folder.path)
                        .collect::<Vec<_>>()
                })
                .await;
            let _ = this.update(cx, |this, cx| {
                if this.shutting_down || this.tree_generation != gen {
                    return;
                }
                apply_tree_children(&mut this.tree_children, &path, children);
                cx.notify();
            });
        });
        self.spawn_tree_task(task);
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

    fn reconcile_selection(&mut self) {
        let visible_images = self.visible_image_paths();
        reconcile_selection(
            &mut self.selected,
            &mut self.selection_order,
            &mut self.selection_anchor,
            &visible_images,
        );
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
        if self.shutting_down || self.viewer.is_some() {
            return;
        }
        let size = self.thumb_size();
        let gen = self.generation;
        let mut cached_or_failed: HashSet<String> = self.thumbs.keys().cloned().collect();
        cached_or_failed.extend(self.thumb_failed.iter().cloned());
        let update = thumb_queue_update(
            &self.visible,
            self.viewport_item_range(),
            &cached_or_failed,
            &mut self.thumb_pending,
            MAX_THUMB_IN_FLIGHT,
        );

        for path in update.to_cancel {
            if let Some(token) = self.thumb_cancellations.get(&path) {
                token.cancel();
            }
        }

        for path in update.to_start {
            let cancellation = CancellationToken::new();
            self.thumb_cancellations
                .insert(path.clone(), cancellation.clone());
            let path_for_bg = path.clone();
            let worker = std::env::current_exe().unwrap_or_else(|_| PathBuf::from("piclens-gpui"));
            let task = cx.spawn(async move |this, cx| {
                let result = cx
                    .background_spawn(async move {
                        ensure_thumbnail_with_timeout(
                            &path_for_bg,
                            size,
                            &worker,
                            THUMBNAIL_TIMEOUT,
                            &cancellation,
                        )
                    })
                    .await;
                let _ = this.update(cx, |this, cx| {
                    this.thumb_pending.remove(&path);
                    this.thumb_cancellations.remove(&path);
                    if this.shutting_down {
                        return;
                    }
                    if this.generation != gen {
                        this.request_thumbs(cx);
                        return;
                    }
                    match result {
                        Ok(cache_path) => {
                            this.thumbs.insert(path, cache_path);
                            if let Some(metrics) = &this.metrics {
                                metrics.thumbnail_ready();
                            }
                        }
                        Err(err) => {
                            if !err.contains("canceled") {
                                this.thumb_failed.insert(path.clone());
                                warn(format!("thumb failed for {path}: {err}"));
                            }
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
        rebuild_tree: bool,
        remember_picker: bool,
        push_history: bool,
        cx: &mut Context<Self>,
    ) {
        if self
            .folder_path
            .as_deref()
            .is_some_and(|current| !path_equals(current, &path))
        {
            self.pending_scroll_restore = None;
        }
        self.cancel_async_work();
        self.folder_path = Some(path.clone());
        self.items.clear();
        self.visible.clear();
        if rebuild_tree {
            self.cancel_tree_work();
            self.tree_roots.clear();
            self.tree_children.clear();
            self.tree_expanded.clear();
        }
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
            self.settings_authority.last_folder_path = Some(path.clone());
            self.persist_settings_authority();
        }
        self.status = "載入中…".into();
        let gen = self.generation;
        let cancellation = CancellationToken::new();
        self.scan_cancellation = Some(cancellation.clone());
        let query = ListQuery {
            folder_path: path.clone(),
            include_subfolders: self.settings.include_subfolders,
            sort: self.settings.sort,
        };
        let path_for_bg = path.clone();
        let task = cx.spawn(async move |this, cx| {
            let outcome = cx
                .background_spawn(async move {
                    let items = scan_folder_cancellable(&query, &cancellation);
                    let tree_roots = if rebuild_tree {
                        match &items {
                            Ok(_) => scan_child_folders_cancellable(&path_for_bg, &cancellation)
                                .unwrap_or_default()
                                .into_iter()
                                .map(|f| f.path)
                                .collect(),
                            Err(_) => Vec::new(),
                        }
                    } else {
                        Vec::new()
                    };
                    (items, tree_roots)
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
                            FolderScanPayload { items },
                        );
                        if !applied {
                            return;
                        }
                        replace_tree_for_picker(
                            rebuild_tree,
                            &mut this.tree_root,
                            &mut this.tree_roots,
                            &mut this.tree_children,
                            &mut this.tree_expanded,
                            &path,
                            outcome.1,
                        );
                        this.recompute_visible();
                        if !drag_sources_exist(&this.drag, |source| {
                            this.items.iter().any(|item| {
                                item.as_image()
                                    .is_some_and(|image| path_equals(&image.path, source))
                            })
                        }) {
                            this.cleanup_drag_session(cx);
                        }
                        this.sync_gallery_list();
                        if this
                            .pending_scroll_restore
                            .as_ref()
                            .is_some_and(|(folder, _)| path_equals(folder, &path))
                        {
                            if let Some((_, offset)) = this.pending_scroll_restore.take() {
                                this.gallery_list.scroll_to(offset);
                            }
                        }
                        this.status = format!("已載入 {} 個項目", this.visible.len());
                        if let Some(metrics) = &this.metrics {
                            metrics.library_ready(
                                this.visible.len(),
                                this.visible
                                    .iter()
                                    .filter(|item| item.as_image().is_some())
                                    .count(),
                            );
                        }
                        this.start_performance_scroll(cx);
                        info(format!("opened folder: {path}"));
                        this.request_thumbs(cx);
                        cx.notify();
                    }
                    Err(err) => {
                        if this.generation != gen {
                            return;
                        }
                        if matches!(err, ScanError::Canceled) {
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
            if let Ok(Ok(Some(paths))) = receiver.await {
                if let Some(path) = paths.into_iter().next() {
                    let path = path.to_string_lossy().replace('\\', "/");
                    let _ = this.update(cx, |this, cx| {
                        if this.shutting_down {
                            return;
                        }
                        this.open_folder(path, true, true, true, cx);
                    });
                }
            }
        });
        self.spawn_task(task);
    }

    fn refresh(&mut self, cx: &mut Context<Self>) {
        if let Some(path) = self.folder_path.clone() {
            self.open_folder(path, false, false, false, cx);
        }
    }

    fn navigate_history(&mut self, back: bool, cx: &mut Context<Self>) {
        if let Some(path) = self.history.step(back).map(str::to_string) {
            self.open_folder(path, false, false, false, cx);
        }
    }

    fn toggle_include_subfolders(&mut self, cx: &mut Context<Self>) {
        self.settings.include_subfolders = !self.settings.include_subfolders;
        self.settings_authority.include_subfolders = self.settings.include_subfolders;
        self.persist_settings_authority();
        self.refresh(cx);
    }

    fn cycle_sort(&mut self, cx: &mut Context<Self>) {
        let sort = match (self.settings.sort.key, self.settings.sort.direction) {
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
        self.set_sort(sort, cx);
    }

    fn set_sort(&mut self, sort: SortState, cx: &mut Context<Self>) {
        self.settings.sort = sort;
        self.settings_authority.sort = sort;
        self.persist_settings_authority();
        self.sync_native_menus(cx);
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

    fn menu_availability(&self) -> crate::actions::MenuAvailability {
        crate::actions::MenuAvailability {
            has_visible_images: self.visible.iter().any(|item| item.as_image().is_some()),
            selection_count: self.selected_images().len(),
            file_operation_busy: self.file_operation_label.is_some(),
        }
    }

    fn sync_native_menus(&mut self, cx: &mut Context<Self>) {
        let sort = match (self.settings.sort.key, self.settings.sort.direction) {
            (SortKey::Name, SortDirection::Asc) => 0,
            (SortKey::Name, SortDirection::Desc) => 1,
            (SortKey::ModifiedAt, SortDirection::Asc) => 2,
            (SortKey::ModifiedAt, SortDirection::Desc) => 3,
        };
        let availability = self.menu_availability();
        let snapshot = (
            sort,
            availability.has_visible_images,
            availability.selection_count,
            availability.file_operation_busy,
        );
        if self.native_menu_snapshot == Some(snapshot) {
            return;
        }
        self.native_menu_snapshot = Some(snapshot);
        crate::actions::set_app_menus(cx, Some(sort), availability);
    }

    fn adjust_thumb_size(&mut self, delta: i32, cx: &mut Context<Self>) {
        let next = self.settings.thumbnail_size + delta;
        self.settings.thumbnail_size = piclens_domain::normalize_thumbnail_size(f64::from(next));
        self.settings_authority.thumbnail_size = self.settings.thumbnail_size;
        self.persist_settings_authority();
        self.cancel_async_work();
        self.thumbs.clear();
        self.thumb_failed.clear();
        self.sync_gallery_list();
        self.request_thumbs(cx);
        cx.notify();
    }

    fn select_path(&mut self, path: &str, gesture: SelectionGesture) {
        let visible_images = self.visible_image_paths();
        apply_selection(
            &mut self.selected,
            &mut self.selection_order,
            &mut self.selection_anchor,
            &visible_images,
            path,
            gesture,
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

    fn selection_announcement(&self) -> String {
        let Some(index) = self.current_visible_index() else {
            return "未選取項目".into();
        };
        let item = &self.visible[index];
        let kind = if item.is_folder() {
            "資料夾"
        } else {
            "圖片"
        };
        format!(
            "已選取{}「{}」，第 {} 個，共 {} 個項目",
            kind,
            item.name(),
            index + 1,
            self.visible.len()
        )
    }

    fn visible_image_paths(&self) -> Vec<String> {
        self.visible
            .iter()
            .filter_map(|i| i.as_image().map(|img| img.path.clone()))
            .collect()
    }

    fn block_for_active_file_operation(&mut self, cx: &mut Context<Self>) -> bool {
        let Some(active) = self.file_operation_label else {
            return false;
        };
        self.status = format!("{active}仍在進行中");
        cx.notify();
        true
    }

    fn start_file_operation<F>(
        &mut self,
        label: &'static str,
        item_count: usize,
        close_viewer_after: bool,
        operation: F,
        window: &mut Window,
        cx: &mut Context<Self>,
    ) where
        F: FnOnce(&CancellationToken) -> FileOperationBatchResult + Send + 'static,
    {
        if self.block_for_active_file_operation(cx) {
            return;
        }

        if item_count == 0 {
            self.status = format!("{label}：目前結果沒有可處理的圖片");
            cx.notify();
            return;
        }

        self.file_operation_generation = self.file_operation_generation.wrapping_add(1);
        let operation_generation = self.file_operation_generation;
        let cancellation = CancellationToken::new();
        self.file_operation_cancellation = Some(cancellation.clone());
        self.file_operation_label = Some(label);
        self.status = format!("{label}：正在處理 {item_count} 張圖片…");
        cx.notify();

        self.file_operation_task = Some(cx.spawn_in(window, async move |this, cx| {
            let batch = cx
                .background_spawn(async move { operation(&cancellation) })
                .await;
            let _ = this.update_in(cx, |this, window, cx| {
                if this.shutting_down || this.file_operation_generation != operation_generation {
                    return;
                }
                this.file_operation_label = None;
                this.file_operation_cancellation = None;
                this.apply_batch(label, &batch, window, cx);
                if close_viewer_after {
                    this.close_viewer(window, cx);
                }
                if let Some(folder) = this.folder_path.clone() {
                    this.pending_scroll_restore =
                        Some((folder, this.gallery_list.logical_scroll_top()));
                }
                this.refresh(cx);
            });
        }));
    }

    fn cancel_file_operation(&mut self, cx: &mut Context<Self>) {
        let Some(cancellation) = self.file_operation_cancellation.as_ref() else {
            return;
        };
        cancellation.cancel();
        if let Some(label) = self.file_operation_label {
            self.status = format!("{label}：正在取消，已開始的項目會先完成…");
        }
        cx.notify();
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
                self.batch_report_list
                    .reset_with_uniform_height(batch.total(), px(104.0));
                self.batch_report = Some(BatchReport {
                    label: label.into(),
                    batch: batch.clone(),
                });
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

    fn close_batch_report(&mut self, cx: &mut Context<Self>) {
        self.batch_report = None;
        self.batch_report_list
            .reset_with_uniform_height(0, px(104.0));
        cx.notify();
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
        self.viewer_loader.cancel(cx);
        // The gallery is covered. Release its decoder slots for the viewer;
        // close_viewer resumes visible thumbnail requests.
        for token in self.thumb_cancellations.values() {
            token.cancel();
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
            display_image: None,
            load_started: Instant::now(),
            paint_recorded: Rc::new(Cell::new(false)),
        });
        if let Some(metrics) = &self.metrics {
            metrics.viewer_opened();
        }
        if !is_animated {
            self.load_viewer_display(path.to_string(), cx);
        }
        self.capture_overlay_focus(window, cx);
        self.focus_overlay_after_join(self.viewer_title_focus.clone(), window, cx);
        cx.notify();
    }

    fn close_viewer(&mut self, window: &mut Window, cx: &mut Context<Self>) {
        self.viewer_loader.cancel(cx);
        self.viewer = None;
        self.restore_overlay_focus(window, cx);
        self.request_thumbs(cx);
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
        viewer.load_started = Instant::now();
        viewer.paint_recorded = Rc::new(Cell::new(false));
        viewer.display_image = None;
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
        } else {
            self.viewer_loader.cancel(cx);
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
        self.rename = Some(RenameState {
            path,
            input: input.clone(),
        });
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
        let source_path = draft.path;
        self.start_file_operation(
            "重新命名",
            1,
            false,
            move |cancellation| FileOperationBatchResult {
                items: vec![rename_image_cancellable(
                    &source_path,
                    &new_name,
                    cancellation,
                )],
            },
            window,
            cx,
        );
    }

    /// Last selected image is the drop target; earlier selections are sources.
    fn plan_drop_rename_from_selection(&mut self, window: &mut Window, cx: &mut Context<Self>) {
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
        self.capture_overlay_focus(window, cx);
        self.focus_overlay_after_join(self.rename_focus.clone(), window, cx);
        cx.notify();
    }

    fn commit_drop_rename(&mut self, window: &mut Window, cx: &mut Context<Self>) {
        let Some(plan) = self.drop_rename.take() else {
            return;
        };
        self.restore_overlay_focus(window, cx);
        let item_count = plan.items.len();
        self.start_file_operation(
            "拖放重新命名",
            item_count,
            false,
            move |cancellation| apply_drop_rename_cancellable(&plan, cancellation),
            window,
            cx,
        );
    }

    fn reveal_path(&mut self, path: &str, cx: &mut Context<Self>) {
        match reveal_in_file_manager(path) {
            Ok(()) => self.status = "已在檔案管理器中顯示。".into(),
            Err(err) => {
                self.status = format!("無法在檔案管理器顯示：{err}");
                warn(self.status.clone());
            }
        }
        cx.notify();
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
        if let Some(path) = path {
            self.reveal_path(&path, cx);
        } else {
            self.status = "請先選取圖片。".into();
            cx.notify();
        }
    }

    fn confirm_trash(
        &mut self,
        paths: Vec<String>,
        close_viewer_after: bool,
        window: &mut Window,
        cx: &mut Context<Self>,
    ) {
        let item_count = paths.len();
        self.start_file_operation(
            "移至回收筒",
            item_count,
            close_viewer_after,
            move |cancellation| trash_paths_cancellable(&paths, cancellation),
            window,
            cx,
        );
    }

    fn request_trash_confirmation(
        &mut self,
        paths: Vec<String>,
        close_viewer_after: bool,
        window: &mut Window,
        cx: &mut Context<Self>,
    ) {
        let description = trash_confirmation_description(paths.len());
        let entity = cx.entity().downgrade();

        window.open_alert_dialog(cx, move |alert, _, _| {
            let entity = entity.clone();
            let paths = paths.clone();
            alert
                .title("移至回收筒")
                .description(description.clone())
                .button_props(
                    DialogButtonProps::default()
                        .ok_text("移至回收筒")
                        .ok_variant(ButtonVariant::Danger)
                        .cancel_text("取消")
                        .show_cancel(true),
                )
                .on_ok(move |_, window, cx| {
                    let entity = entity.clone();
                    let paths = paths.clone();
                    window.defer(cx, move |window, cx| {
                        let _ = entity.update(cx, |this, cx| {
                            this.confirm_trash(paths, close_viewer_after, window, cx);
                        });
                    });
                    true
                })
        });
    }

    fn request_background_file_operation_confirmation(
        &mut self,
        confirmation: BackgroundFileOperationConfirmation,
        paths: Vec<String>,
        window: &mut Window,
        cx: &mut Context<Self>,
    ) {
        let BackgroundFileOperationConfirmation {
            label,
            description,
            ok_text,
            ok_variant,
            kind,
        } = confirmation;
        let entity = cx.entity().downgrade();

        window.open_alert_dialog(cx, move |alert, _, _| {
            let entity = entity.clone();
            let paths = paths.clone();
            alert
                .title(label)
                .description(description.clone())
                .button_props(
                    DialogButtonProps::default()
                        .ok_text(ok_text)
                        .ok_variant(ok_variant)
                        .cancel_text("取消")
                        .show_cancel(true),
                )
                .on_ok(move |_, window, cx| {
                    let entity = entity.clone();
                    let paths = paths.clone();
                    window.defer(cx, move |window, cx| {
                        let _ = entity.update(cx, |this, cx| {
                            let item_count = paths.len();
                            match kind {
                                BackgroundFileOperationKind::Convert(ConversionKind::Jpg) => {
                                    this.start_file_operation(
                                        label,
                                        item_count,
                                        false,
                                        move |cancellation| {
                                            convert_to_jpg_cancellable(&paths, cancellation)
                                        },
                                        window,
                                        cx,
                                    );
                                }
                                BackgroundFileOperationKind::Convert(ConversionKind::Webp) => {
                                    this.start_file_operation(
                                        label,
                                        item_count,
                                        false,
                                        move |cancellation| {
                                            convert_to_lossless_webp_cancellable(
                                                &paths,
                                                cancellation,
                                            )
                                        },
                                        window,
                                        cx,
                                    );
                                }
                                BackgroundFileOperationKind::Cleanup => {
                                    this.start_file_operation(
                                        label,
                                        item_count,
                                        false,
                                        move |cancellation| {
                                            cleanup_same_basename_cancellable(&paths, cancellation)
                                        },
                                        window,
                                        cx,
                                    );
                                }
                            }
                        });
                    });
                    true
                })
        });
    }

    fn request_conversion_confirmation(
        &mut self,
        kind: ConversionKind,
        paths: Vec<String>,
        window: &mut Window,
        cx: &mut Context<Self>,
    ) {
        let description = kind.confirmation_description(paths.len());
        self.request_background_file_operation_confirmation(
            BackgroundFileOperationConfirmation {
                label: kind.label(),
                description,
                ok_text: "開始轉換",
                ok_variant: ButtonVariant::Primary,
                kind: BackgroundFileOperationKind::Convert(kind),
            },
            paths,
            window,
            cx,
        );
    }

    fn request_conversion(
        &mut self,
        kind: ConversionKind,
        window: &mut Window,
        cx: &mut Context<Self>,
    ) {
        if self.block_for_active_file_operation(cx) {
            return;
        }
        if window.has_active_dialog(cx) {
            return;
        }

        let paths = self.visible_image_paths();
        if paths.is_empty() {
            self.status = format!("{}：目前結果沒有可處理的圖片", kind.label());
            cx.notify();
            return;
        }
        if conversion_requires_confirmation(paths.len()) {
            self.request_conversion_confirmation(kind, paths, window, cx);
        } else {
            let item_count = paths.len();
            match kind {
                ConversionKind::Jpg => self.start_file_operation(
                    kind.label(),
                    item_count,
                    false,
                    move |cancellation| convert_to_jpg_cancellable(&paths, cancellation),
                    window,
                    cx,
                ),
                ConversionKind::Webp => self.start_file_operation(
                    kind.label(),
                    item_count,
                    false,
                    move |cancellation| convert_to_lossless_webp_cancellable(&paths, cancellation),
                    window,
                    cx,
                ),
            }
        }
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

    fn on_sort_name_ascending(
        &mut self,
        _: &SortNameAscending,
        _: &mut Window,
        cx: &mut Context<Self>,
    ) {
        self.set_sort(
            SortState {
                key: SortKey::Name,
                direction: SortDirection::Asc,
            },
            cx,
        );
    }

    fn on_sort_name_descending(
        &mut self,
        _: &SortNameDescending,
        _: &mut Window,
        cx: &mut Context<Self>,
    ) {
        self.set_sort(
            SortState {
                key: SortKey::Name,
                direction: SortDirection::Desc,
            },
            cx,
        );
    }

    fn on_sort_modified_ascending(
        &mut self,
        _: &SortModifiedAscending,
        _: &mut Window,
        cx: &mut Context<Self>,
    ) {
        self.set_sort(
            SortState {
                key: SortKey::ModifiedAt,
                direction: SortDirection::Asc,
            },
            cx,
        );
    }

    fn on_sort_modified_descending(
        &mut self,
        _: &SortModifiedDescending,
        _: &mut Window,
        cx: &mut Context<Self>,
    ) {
        self.set_sort(
            SortState {
                key: SortKey::ModifiedAt,
                direction: SortDirection::Desc,
            },
            cx,
        );
    }

    fn on_cancel_file_operation(
        &mut self,
        _: &CancelFileOperation,
        _: &mut Window,
        cx: &mut Context<Self>,
    ) {
        self.cancel_file_operation(cx);
    }

    fn on_prepare_shutdown(&mut self, _: &PrepareShutdown, _: &mut Window, cx: &mut Context<Self>) {
        self.prepare_shutdown(cx);
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
                self.cleanup_drag_session(cx);
            }
            EscapeTarget::DropRename => {
                self.drop_rename = None;
                self.restore_overlay_focus(window, cx);
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
                self.open_folder(path, false, false, true, cx);
                self.focus_handle.focus(window, cx);
                return;
            }
        }
        if let Some(img) = self.selected_images().first() {
            let path = img.path.clone();
            self.open_viewer(&path, window, cx);
        } else if let Some(item) = self.visible.iter().find_map(|i| i.as_image()) {
            let path = item.path.clone();
            self.select_path(&path, SelectionGesture::Replace);
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
        self.select_path(&path, SelectionGesture::Replace);
        self.gallery_list
            .scroll_to_reveal_item(index / self.gallery_columns().max(1));
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
        if self.block_for_active_file_operation(cx) {
            return;
        }
        if window.has_active_dialog(cx) {
            return;
        }

        let viewer_path = self.viewer.as_ref().and_then(|viewer| {
            viewer
                .sequence
                .images
                .get(viewer.sequence.current_index as usize)
                .map(|image| image.path.clone())
        });
        let close_viewer_after = viewer_path.is_some();
        let paths = viewer_path.map_or_else(
            || {
                self.selected_images()
                    .into_iter()
                    .map(|image| image.path)
                    .collect()
            },
            |path| vec![path],
        );
        if paths.is_empty() {
            self.status = "請先選取圖片。".into();
            cx.notify();
            return;
        }
        self.request_trash_confirmation(paths, close_viewer_after, window, cx);
    }

    fn on_rename(&mut self, _: &RenameSelection, window: &mut Window, cx: &mut Context<Self>) {
        if self.viewer.is_some() {
            return;
        }
        if self.block_for_active_file_operation(cx) {
            return;
        }
        self.start_rename(window, cx);
    }

    fn on_drop_rename(&mut self, _: &DropRenamePlan, window: &mut Window, cx: &mut Context<Self>) {
        if self.viewer.is_some() {
            return;
        }
        if self.block_for_active_file_operation(cx) {
            return;
        }
        self.plan_drop_rename_from_selection(window, cx);
    }

    fn on_convert_jpg(&mut self, _: &ConvertJpg, window: &mut Window, cx: &mut Context<Self>) {
        self.request_conversion(ConversionKind::Jpg, window, cx);
    }

    fn on_convert_webp(&mut self, _: &ConvertWebp, window: &mut Window, cx: &mut Context<Self>) {
        self.request_conversion(ConversionKind::Webp, window, cx);
    }

    fn on_cleanup(&mut self, _: &CleanupSameBasename, window: &mut Window, cx: &mut Context<Self>) {
        if self.block_for_active_file_operation(cx) {
            return;
        }
        if window.has_active_dialog(cx) {
            return;
        }

        let paths = self.visible_image_paths();
        if paths.is_empty() {
            self.status = "清除同名格式：目前結果沒有可處理的圖片".into();
            cx.notify();
            return;
        }
        let description = cleanup_confirmation_description(paths.len());
        self.request_background_file_operation_confirmation(
            BackgroundFileOperationConfirmation {
                label: "清除同名格式",
                description,
                ok_text: "確認清除",
                ok_variant: ButtonVariant::Danger,
                kind: BackgroundFileOperationKind::Cleanup,
            },
            paths,
            window,
            cx,
        );
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
            self.select_path(&path, SelectionGesture::Replace);
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

    fn start_performance_scroll(&mut self, cx: &mut Context<Self>) {
        if !self.performance_scroll_requested
            || self.performance_scroll_running
            || self.visible.is_empty()
        {
            return;
        }
        self.performance_scroll_running = true;
        info("performance scroll workload started");
        let task = cx.spawn(async move |this, cx| {
            let started = std::time::Instant::now();
            for index in 0..60 {
                cx.background_executor()
                    .timer(Duration::from_millis(33))
                    .await;
                let keep_running = this
                    .update(cx, |this, cx| {
                        if this.shutting_down {
                            return false;
                        }
                        let distance = if (index / 15) % 2 == 0 { 360.0 } else { -360.0 };
                        this.gallery_list.scroll_by(px(distance));
                        this.request_thumbs(cx);
                        cx.notify();
                        true
                    })
                    .unwrap_or(false);
                if !keep_running {
                    return;
                }
            }
            let elapsed = started.elapsed().as_millis();
            let _ = this.update(cx, |this, _cx| {
                this.performance_scroll_running = false;
                if let Some(metrics) = &this.metrics {
                    metrics.scroll_completed(elapsed);
                }
                info(format!(
                    "performance scroll workload completed in {elapsed} ms"
                ));
            });
        });
        self.spawn_task(task);
    }

    fn cleanup_drag_session(&mut self, cx: &mut Context<Self>) {
        if matches!(self.drag, DragPhase::Idle) && self.drag_autoscroll_step == 0.0 {
            return;
        }
        let _ = drag_cancel(std::mem::replace(&mut self.drag, DragPhase::Idle));
        self.hover_path = None;
        self.drag_autoscroll_step = 0.0;
        cx.notify();
    }

    fn update_drag_autoscroll(&mut self, pointer_y: f64, cx: &mut Context<Self>) {
        let step = self.gallery_bounds.map_or(0.0, |bounds| {
            edge_autoscroll_step(
                pointer_y,
                f64::from(bounds.origin.y),
                f64::from(bounds.origin.y + bounds.size.height),
            )
        });
        self.drag_autoscroll_step = if is_dragging(&self.drag) { step } else { 0.0 };
        if self.drag_autoscroll_step == 0.0 || self.drag_autoscroll_running {
            return;
        }
        self.drag_autoscroll_running = true;
        let task = cx.spawn(async move |this, cx| loop {
            cx.background_executor()
                .timer(Duration::from_millis(33))
                .await;
            let keep_running = this
                .update(cx, |this, cx| {
                    if this.shutting_down
                        || !is_dragging(&this.drag)
                        || this.drag_autoscroll_step == 0.0
                    {
                        this.drag_autoscroll_running = false;
                        return false;
                    }
                    this.gallery_list.scroll_by(px(this.drag_autoscroll_step));
                    this.request_thumbs(cx);
                    cx.notify();
                    true
                })
                .unwrap_or(false);
            if !keep_running {
                break;
            }
        });
        self.spawn_task(task);
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
        let pointer = (f64::from(event.position.x), f64::from(event.position.y));
        self.drag = drag_move(self.drag.clone(), pointer, self.hover_path.clone());
        self.update_drag_autoscroll(pointer.1, cx);
        cx.notify();
    }

    fn on_shell_mouse_up(
        &mut self,
        _: &MouseUpEvent,
        _window: &mut Window,
        cx: &mut Context<Self>,
    ) {
        let finish = drag_finish(std::mem::replace(&mut self.drag, DragPhase::Idle));
        self.drag_autoscroll_step = 0.0;
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

    fn apply_viewer_wheel(&mut self, event: &ScrollWheelEvent, cx: &mut Context<Self>) {
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
        if self
            .viewer
            .as_ref()
            .map(|v| v.zoom.zoom > 1.01)
            .unwrap_or(false)
        {
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
        let tile = self.thumb_size() as f32;
        let width = if self.gallery_width > 1.0 {
            self.gallery_width - GRID_PAD
        } else if self.sidebar_collapsed {
            1200.0
        } else {
            960.0
        };
        grid_column_count(width, tile, GRID_GAP)
    }
}

#[cfg(test)]
mod adaptive_tests {
    use super::{adaptive_layout, fitted_dialog_width};

    #[test]
    fn minimum_window_uses_minimum_layout() {
        let layout = adaptive_layout(800.0);
        assert!(layout.compact);
        assert!(layout.minimum);
        assert_eq!(fitted_dialog_width(800.0, 520.0), 520.0);
    }

    #[test]
    fn normal_window_keeps_full_layout_and_preferred_dialogs() {
        let layout = adaptive_layout(1280.0);
        assert!(!layout.compact);
        assert!(!layout.minimum);
        assert_eq!(fitted_dialog_width(1280.0, 520.0), 520.0);
    }
}

#[cfg(test)]
mod file_operation_tests {
    use std::cell::RefCell;
    use std::fs;
    use std::path::{Path, PathBuf};
    use std::rc::Rc;
    use std::sync::Arc;
    use std::time::{SystemTime, UNIX_EPOCH};

    use gpui::{AppContext as _, Entity, TestAppContext, VisualTestContext};
    use gpui_component::{Root, WindowExt};
    use piclens_domain::{ImageListItem, ListItem};
    use piclens_infra::JsonSettingsStore;

    use super::{
        cleanup_confirmation_description, conversion_requires_confirmation,
        trash_confirmation_description, ConversionKind, GalleryMode, PicLensApp,
    };
    use crate::actions::{CleanupSameBasename, ConvertJpg, ConvertWebp, TrashSelection};

    fn initialize_test_app(cx: &mut TestAppContext) {
        cx.update(|cx| {
            gpui_component::init(cx);
            crate::theme::init(cx);
            crate::actions::init(cx);
        });
    }

    fn open_file_operation_app(
        cx: &mut TestAppContext,
        image_paths: Vec<String>,
        settings_path: PathBuf,
    ) -> (Entity<PicLensApp>, &mut VisualTestContext) {
        let app_slot = Rc::new(RefCell::new(None::<Entity<PicLensApp>>));
        let app_slot_for_window = app_slot.clone();

        let (_, cx) = cx.add_window_view(move |window, cx| {
            let app = cx.new(|cx| {
                PicLensApp::new_with_settings_store(
                    window,
                    cx,
                    None,
                    super::LaunchOptions::default(),
                    Arc::new(JsonSettingsStore::with_path(settings_path)),
                )
            });
            app.update(cx, |app, cx| {
                let images = image_paths
                    .iter()
                    .map(|path| {
                        let path_ref = Path::new(path);
                        ListItem::Image(ImageListItem {
                            path: path.clone(),
                            name: path_ref.file_name().unwrap().to_string_lossy().into_owned(),
                            extension: path_ref.extension().unwrap().to_string_lossy().into_owned(),
                            modified_at_ms: None,
                            size_bytes: fs::metadata(path_ref).unwrap().len(),
                            is_animated: false,
                        })
                    })
                    .collect::<Vec<_>>();
                app.items = images.clone();
                app.visible = images;
                app.selected.extend(image_paths.iter().cloned());
                app.selection_order = image_paths.clone();
                app.selection_anchor = image_paths.first().cloned();
                app.thumb_failed.extend(image_paths.iter().cloned());
                app.gallery_mode = GalleryMode::List;
                app.sync_gallery_list();
                cx.notify();
            });
            *app_slot_for_window.borrow_mut() = Some(app.clone());
            Root::new(app, window, cx)
        });
        let app = app_slot.borrow_mut().take().unwrap();
        let cx: &mut VisualTestContext = cx;
        cx.update(|window, cx| {
            _ = window.draw(cx);
        });
        (app, cx)
    }

    #[gpui::test]
    fn trash_escape_cancels_without_modifying_files(cx: &mut TestAppContext) {
        initialize_test_app(cx);

        let unique = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap()
            .as_nanos();
        let image_path = std::env::temp_dir().join(format!(
            "piclens-trash-confirmation-{}-{unique}.png",
            std::process::id()
        ));
        fs::write(&image_path, b"test image placeholder").unwrap();
        let image_path_string = image_path.to_string_lossy().into_owned();
        let settings_path = image_path.with_extension("settings.json");
        let (app, cx) = open_file_operation_app(cx, vec![image_path_string.clone()], settings_path);
        let app_focus = app.read_with(cx, |app, _| app.focus_handle.clone());

        cx.dispatch_action(TrashSelection);
        assert!(cx.update(|window, cx| window.has_active_dialog(cx)));
        assert!(image_path.exists());
        assert_eq!(
            trash_confirmation_description(1),
            "將 1 張圖片移至作業系統回收筒。取消不會修改檔案。"
        );

        cx.simulate_keystrokes("escape");
        assert!(!cx.update(|window, cx| window.has_active_dialog(cx)));
        cx.update(|window, cx| {
            assert_eq!(window.focused(cx).as_ref(), Some(&app_focus));
        });
        assert!(image_path.exists());
        app.read_with(cx, |app, _| {
            assert_eq!(app.selection_order, vec![image_path_string]);
        });

        fs::remove_file(image_path).unwrap();
    }

    #[gpui::test]
    fn cleanup_escape_cancels_without_modifying_files(cx: &mut TestAppContext) {
        initialize_test_app(cx);

        let unique = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap()
            .as_nanos();
        let fixture_dir = std::env::temp_dir().join(format!(
            "piclens-cleanup-confirmation-{}-{unique}",
            std::process::id()
        ));
        fs::create_dir(&fixture_dir).unwrap();
        let jpg_path = fixture_dir.join("same-name.jpg");
        let png_path = fixture_dir.join("same-name.png");
        fs::write(&jpg_path, b"test jpg placeholder").unwrap();
        fs::write(&png_path, b"test png placeholder").unwrap();
        let image_paths = vec![
            jpg_path.to_string_lossy().into_owned(),
            png_path.to_string_lossy().into_owned(),
        ];
        let settings_path = fixture_dir.join("settings.json");
        let (app, cx) = open_file_operation_app(cx, image_paths, settings_path.clone());
        let app_focus = app.read_with(cx, |app, _| app.focus_handle.clone());

        cx.dispatch_action(CleanupSameBasename);
        assert!(cx.update(|window, cx| window.has_active_dialog(cx)));
        assert_eq!(
            cleanup_confirmation_description(2),
            "將檢查目前結果的 2 張圖片。JPG/JPEG 與 WebP 會保留；其他同名格式會移至作業系統回收筒。取消不會修改檔案。"
        );
        assert!(jpg_path.exists());
        assert!(png_path.exists());

        cx.simulate_keystrokes("escape");
        assert!(!cx.update(|window, cx| window.has_active_dialog(cx)));
        cx.update(|window, cx| {
            assert_eq!(window.focused(cx).as_ref(), Some(&app_focus));
        });
        app.read_with(cx, |app, _| {
            assert!(app.file_operation_label.is_none());
        });
        assert!(jpg_path.exists());
        assert!(png_path.exists());

        fs::remove_file(jpg_path).unwrap();
        fs::remove_file(png_path).unwrap();
        if settings_path.exists() {
            fs::remove_file(settings_path).unwrap();
        }
        fs::remove_dir(fixture_dir).unwrap();
    }

    #[test]
    fn conversion_confirmation_threshold_starts_at_fifty() {
        assert!(!conversion_requires_confirmation(49));
        assert!(conversion_requires_confirmation(50));
        assert_eq!(
            ConversionKind::Jpg.confirmation_description(50),
            "將處理目前結果的 50 張圖片並轉為 JPG。原始檔案會保留，且不會覆寫既有目標檔。取消不會修改檔案。"
        );
        assert_eq!(
            ConversionKind::Webp.confirmation_description(50),
            "將處理目前結果的 50 張圖片並轉為無損 WebP。原始檔案會保留；JPG/JPEG、WebP 與動畫圖片會略過，且不會覆寫既有目標檔。取消不會修改檔案。"
        );
    }

    #[gpui::test]
    fn large_conversions_escape_without_modifying_files(cx: &mut TestAppContext) {
        initialize_test_app(cx);

        let unique = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap()
            .as_nanos();
        let fixture_dir = std::env::temp_dir().join(format!(
            "piclens-conversion-confirmation-{}-{unique}",
            std::process::id()
        ));
        fs::create_dir(&fixture_dir).unwrap();
        let image_paths = (0..50)
            .map(|index| {
                let path = fixture_dir.join(format!("image-{index:02}.png"));
                fs::write(&path, b"test image placeholder").unwrap();
                path.to_string_lossy().into_owned()
            })
            .collect::<Vec<_>>();
        let settings_path = fixture_dir.join("settings.json");
        let (app, cx) = open_file_operation_app(cx, image_paths.clone(), settings_path.clone());
        let app_focus = app.read_with(cx, |app, _| app.focus_handle.clone());

        cx.dispatch_action(ConvertJpg);
        assert!(cx.update(|window, cx| window.has_active_dialog(cx)));
        cx.simulate_keystrokes("escape");
        assert!(!cx.update(|window, cx| window.has_active_dialog(cx)));
        cx.update(|window, cx| {
            assert_eq!(window.focused(cx).as_ref(), Some(&app_focus));
        });

        cx.dispatch_action(ConvertWebp);
        assert!(cx.update(|window, cx| window.has_active_dialog(cx)));
        cx.simulate_keystrokes("escape");
        assert!(!cx.update(|window, cx| window.has_active_dialog(cx)));
        app.read_with(cx, |app, _| {
            assert!(app.file_operation_label.is_none());
        });
        for path in &image_paths {
            assert!(Path::new(path).exists());
            assert!(!Path::new(path).with_extension("jpg").exists());
            assert!(!Path::new(path).with_extension("webp").exists());
            fs::remove_file(path).unwrap();
        }
        if settings_path.exists() {
            fs::remove_file(settings_path).unwrap();
        }
        fs::remove_dir(fixture_dir).unwrap();
    }
}
