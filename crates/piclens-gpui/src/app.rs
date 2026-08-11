//! Main window: library, thumbs, sidebar, selection, viewer, file operations.

use std::collections::{BTreeSet, HashMap, HashSet};
use std::path::PathBuf;
use std::sync::Arc;

use gpui::*;
use gpui_component::button::{Button, ButtonVariants as _};
use gpui_component::input::{Input, InputEvent, InputState};
use gpui_component::{h_flex, v_flex, ActiveTheme, Disableable, Icon, IconName, Root};
use piclens_domain::{
    path_equals, AppSettings, DropTargetBatchRenamePlan, ImageListItem, ImageSequenceSnapshot,
    ListItem, ListQuery, SortDirection, SortKey, SortState, ZoomState, clamp_zoom,
    reset_zoom_state, DEFAULT_THUMBNAIL_SIZE,
};
use piclens_infra::{
    apply_drop_rename, cleanup_same_basename, convert_to_jpg, convert_to_lossless_webp,
    ensure_thumbnail, info, plan_drop_rename, rename_image, reveal_in_file_manager,
    scan_child_folders, scan_folder, trash_paths, warn, JsonSettingsStore,
};

use crate::history::FolderHistory;

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
    generation: u64,
    /// Source path -> cached PNG path for tiles.
    thumbs: HashMap<String, PathBuf>,
    thumb_pending: HashSet<String>,
    thumb_failed: HashSet<String>,
    /// Prevents stacking concurrent thumb pump tasks.
    thumbs_pump_scheduled: bool,
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
            selected: BTreeSet::new(),
            selection_order: Vec::new(),
            history: FolderHistory::default(),
            status: "請選擇資料夾".into(),
            search: search.clone(),
            search_text: String::new(),
            sidebar_collapsed: false,
            gallery_mode: GalleryMode::Grid,
            viewer: None,
            rename: None,
            drop_rename: None,
            generation: 0,
            thumbs: HashMap::new(),
            thumb_pending: HashSet::new(),
            thumb_failed: HashSet::new(),
            thumbs_pump_scheduled: false,
        };

        cx.subscribe_in(&search, window, |this, state, event, _window, cx| {
            if matches!(event, InputEvent::Change) {
                this.search_text = state.read(cx).value().to_string();
                this.recompute_visible();
                this.request_thumbs(cx);
                cx.notify();
            }
        })
        .detach();

        let restore = initial_folder.or(settings.last_folder_path.clone());
        if let Some(path) = restore {
            if PathBuf::from(&path).is_dir() {
                app.open_folder(path, true, false, cx);
            }
        }
        app
    }

    fn persist_settings(&mut self) {
        if let Err(err) = self.settings_store.save(&self.settings) {
            warn(format!("settings save failed: {err}"));
        }
    }

    fn clear_selection(&mut self) {
        self.selected.clear();
        self.selection_order.clear();
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
        if self.thumbs_pump_scheduled {
            return;
        }
        self.thumbs_pump_scheduled = true;
        cx.spawn(async move |this, cx| {
            this.update(cx, |this, cx| {
                this.thumbs_pump_scheduled = false;
                this.pump_thumbs(cx);
            })
            .ok();
        })
        .detach();
    }

    /// Queue background thumbnail work for visible static images (bounded).
    fn pump_thumbs(&mut self, cx: &mut Context<Self>) {
        let size = self.thumb_size();
        let gen = self.generation;
        let mut slots = MAX_THUMB_IN_FLIGHT.saturating_sub(self.thumb_pending.len());
        if slots == 0 {
            return;
        }

        let candidates: Vec<String> = self
            .visible
            .iter()
            .filter_map(|item| {
                let img = item.as_image()?;
                if img.is_animated {
                    return None;
                }
                Some(img.path.clone())
            })
            .filter(|path| {
                !self.thumbs.contains_key(path)
                    && !self.thumb_pending.contains(path)
                    && !self.thumb_failed.contains(path)
            })
            .collect();

        for path in candidates {
            if slots == 0 {
                break;
            }
            slots -= 1;
            self.thumb_pending.insert(path.clone());
            let path_for_bg = path.clone();
            cx.spawn(async move |this, cx| {
                let result = cx
                    .background_spawn(async move { ensure_thumbnail(&path_for_bg, size) })
                    .await;
                this.update(cx, |this, cx| {
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
                    // Queue next batch outside this update via a fresh spawn.
                    this.request_thumbs(cx);
                    cx.notify();
                })
                .ok();
            })
            .detach();
        }
    }

    fn open_folder(
        &mut self,
        path: String,
        remember_picker: bool,
        push_history: bool,
        cx: &mut Context<Self>,
    ) {
        let query = ListQuery {
            folder_path: path.clone(),
            include_subfolders: self.settings.include_subfolders,
            sort: self.settings.sort,
        };
        match scan_folder(&query) {
            Ok(items) => {
                self.generation = self.generation.wrapping_add(1);
                self.folder_path = Some(path.clone());
                self.items = items;
                self.clear_selection();
                self.viewer = None;
                self.rename = None;
                self.drop_rename = None;
                self.thumbs.clear();
                self.thumb_pending.clear();
                self.thumb_failed.clear();
                self.recompute_visible();
                self.child_folders = scan_child_folders(&path)
                    .unwrap_or_default()
                    .into_iter()
                    .map(|f| f.path)
                    .collect();
                if push_history {
                    self.history.push(path.clone());
                }
                if remember_picker {
                    self.settings.last_folder_path = Some(path.clone());
                    self.persist_settings();
                }
                self.status = format!("已載入 {} 個項目", self.visible.len());
                info(format!("opened folder: {path}"));
                self.request_thumbs(cx);
            }
            Err(err) => {
                self.status = format!("無法開啟資料夾：{err}");
                warn(self.status.clone());
            }
        }
        cx.notify();
    }

    fn pick_folder(&mut self, cx: &mut Context<Self>) {
        if let Some(path) = rfd::FileDialog::new().pick_folder() {
            let path = path.to_string_lossy().replace('\\', "/");
            self.open_folder(path, true, true, cx);
        }
    }

    fn refresh(&mut self, cx: &mut Context<Self>) {
        if let Some(path) = self.folder_path.clone() {
            self.open_folder(path, false, false, cx);
        }
    }

    fn navigate_history(&mut self, back: bool, cx: &mut Context<Self>) {
        let path = if back {
            self.history.back().map(str::to_string)
        } else {
            self.history.forward().map(str::to_string)
        };
        if let Some(path) = path {
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
        self.generation = self.generation.wrapping_add(1);
        self.thumbs.clear();
        self.thumb_pending.clear();
        self.thumb_failed.clear();
        self.request_thumbs(cx);
        cx.notify();
    }

    fn select_path(&mut self, path: &str, additive: bool) {
        if !additive {
            self.clear_selection();
        }
        if self.selected.insert(path.to_string()) {
            self.selection_order.push(path.to_string());
        }
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

    fn apply_batch(&mut self, label: &str, batch: &piclens_domain::FileOperationBatchResult) {
        self.status = format!(
            "{label}：成功 {}，略過 {}，失敗 {}（共 {}）",
            batch.succeeded(),
            batch.skipped(),
            batch.failed(),
            batch.total()
        );
        info(self.status.clone());
    }

    fn open_viewer(&mut self, path: &str, cx: &mut Context<Self>) {
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
        cx.notify();
    }

    /// Decode a bounded safe PNG for the viewer (never feed raw corrupt files to `img`).
    fn load_viewer_display(&mut self, path: String, cx: &mut Context<Self>) {
        let gen = self.generation;
        let path_for_bg = path.clone();
        let path_for_ui = path;
        cx.spawn(async move |this, cx| {
            let result = cx
                .background_spawn(async move { ensure_thumbnail(&path_for_bg, 1024) })
                .await;
            this.update(cx, |this, cx| {
                if this.generation != gen {
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
            })
            .ok();
        })
        .detach();
    }

    fn close_viewer(&mut self, cx: &mut Context<Self>) {
        self.viewer = None;
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
        self.rename = Some(RenameState { path, input });
        cx.notify();
    }

    fn commit_rename(&mut self, cx: &mut Context<Self>) {
        let Some(draft) = self.rename.take() else {
            return;
        };
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

    fn commit_drop_rename(&mut self, cx: &mut Context<Self>) {
        let Some(plan) = self.drop_rename.take() else {
            return;
        };
        let batch = apply_drop_rename(&plan);
        self.apply_batch("拖放重新命名", &batch);
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

    fn tile_preview(&self, path: &str, is_folder: bool, animated: bool, size: f32) -> AnyElement {
        if is_folder {
            return div()
                .size(px(size))
                .flex()
                .items_center()
                .justify_center()
                .bg(self_theme_muted())
                .child(Icon::new(IconName::Folder))
                .into_any_element();
        }
        if animated {
            return div()
                .size(px(size))
                .flex()
                .items_center()
                .justify_center()
                .bg(self_theme_muted())
                .child(
                    div()
                        .text_xs()
                        .child("動畫"),
                )
                .into_any_element();
        }
        if let Some(cache) = self.thumbs.get(path) {
            return img(cache.clone())
                .object_fit(ObjectFit::Cover)
                .size(px(size))
                .into_any_element();
        }
        if self.thumb_pending.contains(path) {
            return div()
                .size(px(size))
                .flex()
                .items_center()
                .justify_center()
                .bg(self_theme_muted())
                .child(div().text_xs().child("…"))
                .into_any_element();
        }
        div()
            .size(px(size))
            .flex()
            .items_center()
            .justify_center()
            .bg(self_theme_muted())
            .child(Icon::new(IconName::File))
            .into_any_element()
    }
}

/// Placeholder muted color when theme access needs a static fallback in pure helpers.
/// Prefer `cx.theme().secondary` in render paths.
fn self_theme_muted() -> Hsla {
    hsla(0.0, 0.0, 0.5, 0.15)
}

impl Render for PicLensApp {
    fn render(&mut self, window: &mut Window, cx: &mut Context<Self>) -> impl IntoElement {
        // Do not schedule thumbs or update entities here — causes RefCell / stack faults.

        let folder_title = self
            .folder_path
            .as_deref()
            .map(|p| {
                PathBuf::from(p)
                    .file_name()
                    .and_then(|n| n.to_str())
                    .unwrap_or(p)
                    .to_string()
            })
            .unwrap_or_else(|| "未選擇資料夾".into());

        let tile_size = self.thumb_size() as f32;
        let gallery_mode = self.gallery_mode;

        let gallery_body: AnyElement = if self.visible.is_empty() {
            v_flex()
                .size_full()
                .items_center()
                .justify_center()
                .gap_3()
                .child(div().text_lg().child(if self.folder_path.is_some() {
                    "此資料夾沒有符合的項目"
                } else {
                    "請選擇圖片資料夾以開始"
                }))
                .child(
                    Button::new("empty-open")
                        .primary()
                        .label("選擇資料夾")
                        .on_click(cx.listener(|this, _, _, cx| this.pick_folder(cx))),
                )
                .into_any_element()
        } else if gallery_mode == GalleryMode::Grid {
            div()
                .flex()
                .flex_row()
                .flex_wrap()
                .gap_2()
                .p_2()
                .children(self.visible.iter().enumerate().map(|(idx, item)| {
                    let path = item.path().to_string();
                    let name = item.name().to_string();
                    let is_folder = item.is_folder();
                    let animated = item.as_image().map(|i| i.is_animated).unwrap_or(false);
                    let selected = self.selected.contains(&path);
                    let preview = self.tile_preview(&path, is_folder, animated, tile_size);
                    v_flex()
                        .id(("tile", idx))
                        .w(px(tile_size + 8.0))
                        .gap_1()
                        .p_1()
                        .rounded(cx.theme().radius)
                        .border_1()
                        .border_color(if selected {
                            cx.theme().primary
                        } else {
                            cx.theme().border
                        })
                        .bg(if selected {
                            cx.theme().secondary
                        } else {
                            cx.theme().background
                        })
                        .cursor_pointer()
                        .child(preview)
                        .child(
                            div()
                                .text_xs()
                                .overflow_hidden()
                                .whitespace_nowrap()
                                .child(name),
                        )
                        .on_mouse_down(
                            MouseButton::Left,
                            cx.listener(move |this, event: &MouseDownEvent, _, cx| {
                                let additive = event.modifiers.control || event.modifiers.shift;
                                if is_folder {
                                    this.open_folder(path.clone(), false, true, cx);
                                } else if event.click_count >= 2 {
                                    this.select_path(&path, false);
                                    this.open_viewer(&path, cx);
                                } else {
                                    this.select_path(&path, additive);
                                    cx.notify();
                                }
                            }),
                        )
                }))
                .into_any_element()
        } else {
            v_flex()
                .w_full()
                .children(self.visible.iter().enumerate().map(|(idx, item)| {
                    let path = item.path().to_string();
                    let name = item.name().to_string();
                    let is_folder = item.is_folder();
                    let animated = item.as_image().map(|i| i.is_animated).unwrap_or(false);
                    let selected = self.selected.contains(&path);
                    let preview = self.tile_preview(&path, is_folder, animated, 48.0);
                    h_flex()
                        .id(("row", idx))
                        .w_full()
                        .gap_2()
                        .p_2()
                        .items_center()
                        .border_b_1()
                        .border_color(if selected {
                            cx.theme().primary
                        } else {
                            cx.theme().border
                        })
                        .bg(if selected {
                            cx.theme().secondary
                        } else {
                            cx.theme().background
                        })
                        .cursor_pointer()
                        .child(preview)
                        .child(div().flex_1().child(name))
                        .on_mouse_down(
                            MouseButton::Left,
                            cx.listener(move |this, event: &MouseDownEvent, _, cx| {
                                let additive = event.modifiers.control || event.modifiers.shift;
                                if is_folder {
                                    this.open_folder(path.clone(), false, true, cx);
                                } else if event.click_count >= 2 {
                                    this.select_path(&path, false);
                                    this.open_viewer(&path, cx);
                                } else {
                                    this.select_path(&path, additive);
                                    cx.notify();
                                }
                            }),
                        )
                }))
                .into_any_element()
        };

        let sidebar = if self.sidebar_collapsed {
            div().id("sidebar-off").into_any_element()
        } else {
            let root = self.folder_path.clone().unwrap_or_default();
            v_flex()
                .id("sidebar")
                .w_56()
                .h_full()
                .border_r_1()
                .border_color(cx.theme().border)
                .p_2()
                .gap_1()
                .child(div().text_sm().child("資料夾"))
                .children(self.child_folders.iter().enumerate().map(|(idx, path)| {
                    let path = path.clone();
                    let name = PathBuf::from(&path)
                        .file_name()
                        .and_then(|n| n.to_str())
                        .unwrap_or(path.as_str())
                        .to_string();
                    Button::new(("child", idx)).label(name).on_click(cx.listener(
                        move |this, _, _, cx| {
                            this.open_folder(path.clone(), false, true, cx);
                        },
                    ))
                }))
                .child(if root.is_empty() {
                    div().into_any_element()
                } else {
                    div()
                        .text_xs()
                        .text_color(cx.theme().muted_foreground)
                        .child(root)
                        .into_any_element()
                })
                .into_any_element()
        };

        let viewer_layer = self.viewer.as_ref().map(|viewer| {
            let idx = viewer.sequence.current_index as usize;
            let name = viewer
                .sequence
                .images
                .get(idx)
                .map(|i| i.name.clone())
                .unwrap_or_default();
            let zoom = viewer.zoom.zoom;
            let message = viewer.message.clone();
            let display = viewer.display_path.clone();

            div()
                .id("viewer")
                .absolute()
                .inset_0()
                .flex()
                .flex_col()
                .bg(cx.theme().background)
                .child(
                    h_flex()
                        .w_full()
                        .gap_2()
                        .p_2()
                        .items_center()
                        .border_b_1()
                        .border_color(cx.theme().border)
                        .child(
                            Button::new("v-close")
                                .label("關閉")
                                .on_click(cx.listener(|this, _, _, cx| this.close_viewer(cx))),
                        )
                        .child(
                            Button::new("v-prev")
                                .label("上一張")
                                .on_click(cx.listener(|this, _, _, cx| this.viewer_step(-1, cx))),
                        )
                        .child(
                            Button::new("v-next")
                                .label("下一張")
                                .on_click(cx.listener(|this, _, _, cx| this.viewer_step(1, cx))),
                        )
                        .child(
                            Button::new("v-zin")
                                .label("放大")
                                .on_click(cx.listener(|this, _, _, cx| {
                                    if let Some(v) = this.viewer.as_mut() {
                                        v.zoom.zoom = clamp_zoom(v.zoom.zoom * 1.2);
                                        cx.notify();
                                    }
                                })),
                        )
                        .child(
                            Button::new("v-zout")
                                .label("縮小")
                                .on_click(cx.listener(|this, _, _, cx| {
                                    if let Some(v) = this.viewer.as_mut() {
                                        v.zoom.zoom = clamp_zoom(v.zoom.zoom / 1.2);
                                        cx.notify();
                                    }
                                })),
                        )
                        .child(
                            Button::new("v-zreset")
                                .label("重設縮放")
                                .on_click(cx.listener(|this, _, _, cx| {
                                    if let Some(v) = this.viewer.as_mut() {
                                        v.zoom = reset_zoom_state();
                                        cx.notify();
                                    }
                                })),
                        )
                        .child(
                            Button::new("v-reveal")
                                .label("在檔案管理器顯示")
                                .on_click(cx.listener(|this, _, _, cx| this.reveal_focus(cx))),
                        )
                        .child(div().flex_1())
                        .child(div().child(format!("{name} · {:.0}%", zoom * 100.0))),
                )
                .child(
                    div()
                        .flex_1()
                        .flex()
                        .items_center()
                        .justify_center()
                        .overflow_hidden()
                        .p_2()
                        .child(if let Some(msg) = message {
                            div()
                                .text_lg()
                                .text_color(cx.theme().danger)
                                .child(msg)
                                .into_any_element()
                        } else if let Some(display_path) = display {
                            let base = 640.0 * zoom as f32;
                            img(display_path)
                                .object_fit(ObjectFit::Contain)
                                .w(px(base))
                                .h(px(base))
                                .into_any_element()
                        } else {
                            div()
                                .text_sm()
                                .text_color(cx.theme().muted_foreground)
                                .child("載入中…")
                                .into_any_element()
                        }),
                )
        });

        let rename_layer = self.rename.as_ref().map(|draft| {
            div()
                .id("rename")
                .absolute()
                .inset_0()
                .flex()
                .items_center()
                .justify_center()
                .bg(black().opacity(0.4))
                .child(
                    v_flex()
                        .w_96()
                        .gap_3()
                        .p_4()
                        .rounded(cx.theme().radius)
                        .bg(cx.theme().background)
                        .border_1()
                        .border_color(cx.theme().border)
                        .child(div().child("重新命名"))
                        .child(Input::new(&draft.input))
                        .child(
                            h_flex()
                                .gap_2()
                                .justify_end()
                                .child(
                                    Button::new("rn-cancel").label("取消").on_click(cx.listener(
                                        |this, _, _, cx| {
                                            this.rename = None;
                                            cx.notify();
                                        },
                                    )),
                                )
                                .child(
                                    Button::new("rn-ok")
                                        .primary()
                                        .label("確定")
                                        .on_click(cx.listener(|this, _, _, cx| {
                                            this.commit_rename(cx);
                                        })),
                                ),
                        ),
                )
        });

        let drop_layer = self.drop_rename.as_ref().map(|plan| {
            let lines: Vec<AnyElement> = plan
                .items
                .iter()
                .take(12)
                .map(|item| {
                    let src = PathBuf::from(&item.source_path)
                        .file_name()
                        .and_then(|n| n.to_str())
                        .unwrap_or("?")
                        .to_string();
                    let dst = PathBuf::from(&item.target_path)
                        .file_name()
                        .and_then(|n| n.to_str())
                        .unwrap_or("?")
                        .to_string();
                    let line = if item.should_skip {
                        format!("略過 {src}")
                    } else {
                        format!("{src} → {dst}")
                    };
                    div().text_sm().child(line).into_any_element()
                })
                .collect();
            let more = if plan.items.len() > 12 {
                format!("…共 {} 項", plan.total)
            } else {
                format!("共 {} 項", plan.total)
            };

            div()
                .id("drop-rename")
                .absolute()
                .inset_0()
                .flex()
                .items_center()
                .justify_center()
                .bg(black().opacity(0.4))
                .child(
                    v_flex()
                        .w(px(520.))
                        .max_h(px(480.))
                        .gap_2()
                        .p_4()
                        .rounded(cx.theme().radius)
                        .bg(cx.theme().background)
                        .border_1()
                        .border_color(cx.theme().border)
                        .child(div().child("拖放重新命名預覽"))
                        .child(div().text_xs().text_color(cx.theme().muted_foreground).child(
                            "選取順序：來源在前，最後一張為目標。取消不會改檔。",
                        ))
                        .child(
                            div()
                                .id("drop-plan-list")
                                .flex_1()
                                .overflow_y_scroll()
                                .child(v_flex().gap_1().children(lines)),
                        )
                        .child(div().text_xs().child(more))
                        .child(
                            h_flex()
                                .gap_2()
                                .justify_end()
                                .child(
                                    Button::new("dr-cancel").label("取消").on_click(cx.listener(
                                        |this, _, _, cx| {
                                            this.drop_rename = None;
                                            cx.notify();
                                        },
                                    )),
                                )
                                .child(
                                    Button::new("dr-ok")
                                        .primary()
                                        .label("確認重新命名")
                                        .on_click(cx.listener(|this, _, _, cx| {
                                            this.commit_drop_rename(cx);
                                        })),
                                ),
                        ),
                )
        });

        div()
            .id("piclens-root")
            .size_full()
            .flex()
            .flex_col()
            .bg(cx.theme().background)
            .text_color(cx.theme().foreground)
            .on_key_down(cx.listener(|this, event: &KeyDownEvent, _, cx| {
                let key = event.keystroke.key.as_str();
                if key == "escape" {
                    if this.drop_rename.is_some() {
                        this.drop_rename = None;
                        cx.notify();
                    } else if this.rename.is_some() {
                        this.rename = None;
                        cx.notify();
                    } else if this.viewer.is_some() {
                        this.close_viewer(cx);
                    }
                } else if this.viewer.is_some() {
                    match key {
                        "left" | "arrowleft" => this.viewer_step(-1, cx),
                        "right" | "arrowright" => this.viewer_step(1, cx),
                        _ => {}
                    }
                }
            }))
            .child(
                h_flex()
                    .w_full()
                    .gap_2()
                    .p_2()
                    .items_center()
                    .border_b_1()
                    .border_color(cx.theme().border)
                    .child(
                        Button::new("open")
                            .primary()
                            .label("選擇資料夾")
                            .on_click(cx.listener(|this, _, _, cx| this.pick_folder(cx))),
                    )
                    .child(
                        Button::new("back")
                            .label("上一頁")
                            .disabled(!self.history.can_back())
                            .on_click(cx.listener(|this, _, _, cx| this.navigate_history(true, cx))),
                    )
                    .child(
                        Button::new("forward")
                            .label("下一頁")
                            .disabled(!self.history.can_forward())
                            .on_click(
                                cx.listener(|this, _, _, cx| this.navigate_history(false, cx)),
                            ),
                    )
                    .child(
                        Button::new("refresh")
                            .label("重新整理")
                            .on_click(cx.listener(|this, _, _, cx| this.refresh(cx))),
                    )
                    .child(
                        Button::new("sidebar")
                            .label(if self.sidebar_collapsed {
                                "顯示側欄"
                            } else {
                                "隱藏側欄"
                            })
                            .on_click(cx.listener(|this, _, _, cx| {
                                this.sidebar_collapsed = !this.sidebar_collapsed;
                                cx.notify();
                            })),
                    )
                    .child(
                        Button::new("mode")
                            .label(if self.gallery_mode == GalleryMode::Grid {
                                "格狀"
                            } else {
                                "列表"
                            })
                            .on_click(cx.listener(|this, _, _, cx| {
                                this.gallery_mode = match this.gallery_mode {
                                    GalleryMode::Grid => GalleryMode::List,
                                    GalleryMode::List => GalleryMode::Grid,
                                };
                                cx.notify();
                            })),
                    )
                    .child(
                        Button::new("sort")
                            .label(self.sort_label())
                            .on_click(cx.listener(|this, _, _, cx| this.cycle_sort(cx))),
                    )
                    .child(
                        Button::new("recursive")
                            .label(if self.settings.include_subfolders {
                                "含子資料夾"
                            } else {
                                "僅目前資料夾"
                            })
                            .on_click(cx.listener(|this, _, _, cx| {
                                this.toggle_include_subfolders(cx)
                            })),
                    )
                    .child(div().flex_1())
                    .child(
                        div()
                            .text_sm()
                            .text_color(cx.theme().muted_foreground)
                            .child(folder_title),
                    ),
            )
            .child(
                h_flex()
                    .w_full()
                    .gap_2()
                    .p_2()
                    .items_center()
                    .border_b_1()
                    .border_color(cx.theme().border)
                    .child(div().w_64().child(Input::new(&self.search)))
                    .child(
                        Button::new("thumb-")
                            .label("縮圖-")
                            .on_click(cx.listener(|this, _, _, cx| this.adjust_thumb_size(-20, cx))),
                    )
                    .child(
                        Button::new("thumb+")
                            .label("縮圖+")
                            .on_click(cx.listener(|this, _, _, cx| this.adjust_thumb_size(20, cx))),
                    )
                    .child(
                        Button::new("to-jpg")
                            .label("轉 JPG")
                            .on_click(cx.listener(|this, _, _, cx| {
                                let paths = this.visible_image_paths();
                                let batch = convert_to_jpg(&paths);
                                this.apply_batch("轉 JPG", &batch);
                                this.refresh(cx);
                            })),
                    )
                    .child(
                        Button::new("to-webp")
                            .label("轉 WebP")
                            .on_click(cx.listener(|this, _, _, cx| {
                                let paths = this.visible_image_paths();
                                let batch = convert_to_lossless_webp(&paths);
                                this.apply_batch("轉 WebP", &batch);
                                this.refresh(cx);
                            })),
                    )
                    .child(
                        Button::new("cleanup")
                            .label("清除同名格式")
                            .on_click(cx.listener(|this, _, _, cx| {
                                let paths = this.visible_image_paths();
                                let batch = cleanup_same_basename(&paths);
                                this.apply_batch("清除同名格式", &batch);
                                this.refresh(cx);
                            })),
                    )
                    .child(
                        Button::new("rename")
                            .label("重新命名")
                            .on_click(cx.listener(|this, _, window, cx| {
                                this.start_rename(window, cx)
                            })),
                    )
                    .child(
                        Button::new("drop-rename")
                            .label("依目標重新命名")
                            .on_click(cx.listener(|this, _, _, cx| {
                                this.plan_drop_rename_from_selection(cx)
                            })),
                    )
                    .child(
                        Button::new("trash")
                            .danger()
                            .label("移至回收筒")
                            .on_click(cx.listener(|this, _, _, cx| {
                                let paths: Vec<String> =
                                    this.selected_images().into_iter().map(|i| i.path).collect();
                                if paths.is_empty() {
                                    this.status = "請先選取圖片。".into();
                                    cx.notify();
                                    return;
                                }
                                let batch = trash_paths(&paths);
                                this.apply_batch("移至回收筒", &batch);
                                this.refresh(cx);
                            })),
                    )
                    .child(
                        Button::new("reveal")
                            .label("在檔案管理器顯示")
                            .on_click(cx.listener(|this, _, _, cx| this.reveal_focus(cx))),
                    )
                    .child(
                        Button::new("clear-sel")
                            .label("清除選取")
                            .on_click(cx.listener(|this, _, _, cx| {
                                this.clear_selection();
                                cx.notify();
                            })),
                    )
                    .child(
                        Button::new("open-view")
                            .label("開啟檢視")
                            .on_click(cx.listener(|this, _, _, cx| {
                                if let Some(img) = this.selected_images().first() {
                                    let path = img.path.clone();
                                    this.open_viewer(&path, cx);
                                } else {
                                    this.status = "請先選取圖片。".into();
                                    cx.notify();
                                }
                            })),
                    ),
            )
            .child(
                h_flex()
                    .id("body")
                    .flex_1()
                    .w_full()
                    .overflow_hidden()
                    .child(sidebar)
                    .child(
                        div()
                            .id("gallery")
                            .flex_1()
                            .h_full()
                            .overflow_y_scroll()
                            .child(gallery_body),
                    ),
            )
            .child(
                h_flex()
                    .w_full()
                    .p_2()
                    .border_t_1()
                    .border_color(cx.theme().border)
                    .justify_between()
                    .child(
                        div()
                            .text_sm()
                            .text_color(cx.theme().muted_foreground)
                            .child(self.status.clone()),
                    )
                    .child(
                        div()
                            .text_sm()
                            .text_color(cx.theme().muted_foreground)
                            .child(format!(
                                "{} 項 · 選取 {} · 縮圖佇列 {} · 尺寸 {}",
                                self.visible.len(),
                                self.selected.len(),
                                self.thumb_pending.len(),
                                self.settings.thumbnail_size
                            )),
                    ),
            )
            .children(viewer_layer)
            .children(rename_layer)
            .children(drop_layer)
            .children(Root::render_dialog_layer(window, cx))
            .children(Root::render_sheet_layer(window, cx))
            .children(Root::render_notification_layer(window, cx))
    }
}
