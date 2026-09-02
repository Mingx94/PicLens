//! App state, frame lifecycle, event handling, and action reducer.

use std::collections::{HashMap, VecDeque};
use std::path::PathBuf;
use std::time::{Duration, Instant};

use piclens_domain::{
    apply_tree_children, clamp_zoom, normalize_thumbnail_size, pan_offset, path_equals,
    replace_tree_for_picker, reset_zoom_state, sort_items, toggle_expand, validate_image_file_name,
    zoom_at_point, ExpandAction, ListItem, ListQuery, Point, SortState,
};
use piclens_infra::plan_drop_rename;

use crate::backend::{Backend, Command, Event, FileOperation, WorkIdentity};
use crate::diagnostics::RuntimeMetrics;
use crate::images::ThumbnailLoader;
use crate::model::{
    Action, AppModel, ConversionKind, DialogState, DragSession, Loadable, Page, SelectionGesture,
    SelectionState, ViewerState,
};

const CONVERSION_CONFIRMATION_THRESHOLD: usize = 50;
const DRAG_THRESHOLD: f64 = 8.0;
use crate::{theme, ui, LaunchOptions};

struct Reducer {
    model: AppModel,
    actions: VecDeque<Action>,
    commands: VecDeque<Command>,
    generation: u64,
    tree_generation: u64,
    next_request_id: u64,
    pending_probe: Option<WorkIdentity>,
    pending_library: Option<WorkIdentity>,
    pending_tree: HashMap<String, WorkIdentity>,
    pending_file_operation: Option<WorkIdentity>,
    include_subfolders: bool,
    sort: SortState,
    close_requested: bool,
}

impl Reducer {
    fn new(initial_folder: Option<std::path::PathBuf>) -> Self {
        Self {
            model: AppModel::new(initial_folder),
            actions: VecDeque::new(),
            commands: VecDeque::new(),
            generation: 0,
            tree_generation: 0,
            next_request_id: 1,
            pending_probe: None,
            pending_library: None,
            pending_tree: HashMap::new(),
            pending_file_operation: None,
            include_subfolders: false,
            sort: SortState::default(),
            close_requested: false,
        }
    }

    fn push_action(&mut self, action: Action) {
        self.actions.push_back(action);
    }

    fn reduce_actions(&mut self) -> usize {
        let mut applied = 0;
        while let Some(action) = self.actions.pop_front() {
            applied += 1;
            match action {
                Action::ChooseFolder => {}
                Action::PickedFolder(path) => self.open_folder(path, true, true, true),
                Action::RestoreFolder(path) => self.open_folder(path, true, false, true),
                Action::NavigateFolder(path) => self.open_folder(path, false, false, true),
                Action::NavigateHistory { back } => self.navigate_history(back),
                Action::ToggleTreeFolder(path) => self.toggle_tree_folder(path),
                Action::ToggleSidebar => {
                    self.model.sidebar_collapsed = !self.model.sidebar_collapsed;
                    self.commands.push_back(Command::PersistSidebar {
                        collapsed: self.model.sidebar_collapsed,
                    });
                }
                Action::RetryBackendProbe => self.push_action(Action::StartBackendProbe),
                Action::DismissStatus => self.model.notice = None,
                Action::ShowNotice(message) => self.model.notice = Some(message),
                Action::StartBackendProbe => self.start_backend_probe(),
                Action::LoadLibrary(query) => self.start_library_load(query),
                Action::ReloadLibrary => {
                    if let Some(query) = self.model.library_query.clone() {
                        self.push_action(Action::LoadLibrary(query));
                    }
                }
                Action::SetSearch(search) => {
                    self.model.search = search;
                    self.model.selection = SelectionState::default();
                    self.model.drag = None;
                    self.rebuild_visible_items();
                }
                Action::SetSort(sort) => self.set_sort(sort),
                Action::ToggleIncludeSubfolders => self.toggle_include_subfolders(),
                Action::SetThumbnailSize(size) => self.set_thumbnail_size(size),
                Action::OpenViewer(path) => self.open_viewer(path),
                Action::CloseViewer => self.close_viewer(),
                Action::StepViewer(delta) => self.step_viewer(delta),
                Action::AdjustViewerZoom(delta) => self.adjust_viewer_zoom(delta),
                Action::ZoomViewerAt {
                    pointer,
                    viewport_center,
                    delta,
                } => self.zoom_viewer_at(pointer, viewport_center, delta),
                Action::PanViewer(delta) => self.pan_viewer(delta),
                Action::ResetViewerZoom => self.reset_viewer_zoom(),
                Action::RevealViewer => self.reveal_viewer(),
                Action::RevealPath(path) => self.reveal_path(path),
                Action::OpenRename => self.open_rename(),
                Action::SetRenameBasename(basename) => self.set_rename_basename(basename),
                Action::ConfirmRename => self.confirm_rename(),
                Action::RequestTrash => self.request_trash(),
                Action::ConfirmTrash => self.confirm_trash(),
                Action::RequestConversion(kind) => self.request_conversion(kind),
                Action::ConfirmConversion => self.confirm_conversion(),
                Action::RequestCleanup => self.request_cleanup(),
                Action::ConfirmCleanup => self.confirm_cleanup(),
                Action::StartDrag { source, pointer } => self.start_drag(source, pointer),
                Action::UpdateDrag { pointer, target } => self.update_drag(pointer, target),
                Action::FinishDrag => self.finish_drag(),
                Action::CancelDrag => self.model.drag = None,
                Action::ConfirmDropRename => self.confirm_drop_rename(),
                Action::CancelFileOperation => self.cancel_file_operation(),
                Action::CloseDialog => self.model.dialog = None,
                Action::SelectImage { path, gesture } => self.select_image(&path, gesture),
                Action::ClearSelection => {
                    self.model.selection = SelectionState::default();
                    self.model.drag = None;
                }
            }
        }
        applied
    }

    fn start_backend_probe(&mut self) {
        let identity = WorkIdentity {
            generation: self.generation,
            request_id: self.next_request_id,
        };
        self.next_request_id = self.next_request_id.wrapping_add(1).max(1);
        self.pending_probe = Some(identity);
        self.model.backend = Loadable::Loading;
        self.commands.push_back(Command::Probe { identity });
    }

    fn start_library_load(&mut self, query: ListQuery) {
        self.generation = self.generation.wrapping_add(1).max(1);
        let identity = WorkIdentity {
            generation: self.generation,
            request_id: self.next_request_id,
        };
        self.next_request_id = self.next_request_id.wrapping_add(1).max(1);
        self.pending_library = Some(identity);
        self.include_subfolders = query.include_subfolders;
        self.sort = query.sort;
        self.model.current_folder = Some(query.folder_path.clone().into());
        self.model.library_query = Some(query.clone());
        self.model.library = Loadable::Loading;
        self.model.visible_items.clear();
        self.model.selection = SelectionState::default();
        self.model.drag = None;
        self.commands
            .push_back(Command::LoadLibrary { identity, query });
    }

    fn open_folder(
        &mut self,
        path: std::path::PathBuf,
        rebuild_tree: bool,
        remember_picker: bool,
        push_history: bool,
    ) {
        let path = path.to_string_lossy().into_owned();
        if rebuild_tree {
            self.tree_generation = self.tree_generation.wrapping_add(1).max(1);
            self.pending_tree.clear();
            replace_tree_for_picker(
                true,
                &mut self.model.tree_root,
                &mut self.model.tree_roots,
                &mut self.model.tree_children,
                &mut self.model.tree_expanded,
                &path,
                Vec::new(),
            );
            self.request_tree_children(path.clone(), true);
        }
        if push_history {
            self.model.history.push(path.clone());
        }
        if remember_picker {
            self.commands
                .push_back(Command::PersistPickerFolder { path: path.clone() });
        }
        self.start_library_load(ListQuery {
            folder_path: path,
            include_subfolders: self.include_subfolders,
            sort: self.sort,
        });
    }

    fn navigate_history(&mut self, back: bool) {
        if let Some(path) = self.model.history.step(back).map(str::to_owned) {
            self.open_folder(path.into(), false, false, false);
        }
    }

    fn toggle_tree_folder(&mut self, path: String) {
        match toggle_expand(&mut self.model.tree_expanded, &path) {
            ExpandAction::Collapse => {}
            ExpandAction::NeedChildren => self.request_tree_children(path, false),
        }
    }

    fn request_tree_children(&mut self, parent: String, force: bool) {
        if self.pending_tree.contains_key(&parent)
            || (!force && self.model.tree_children.contains_key(&parent))
        {
            return;
        }
        let identity = WorkIdentity {
            generation: self.tree_generation,
            request_id: self.next_request_id,
        };
        self.next_request_id = self.next_request_id.wrapping_add(1).max(1);
        self.pending_tree.insert(parent.clone(), identity);
        self.commands
            .push_back(Command::LoadTreeChildren { identity, parent });
    }

    fn rebuild_visible_items(&mut self) {
        let Loadable::Ready(items) = &self.model.library else {
            self.model.visible_items.clear();
            return;
        };
        let search = self.model.search.trim().to_lowercase();
        let filtered = items
            .iter()
            .filter(|item| {
                search.is_empty()
                    || item.name().to_lowercase().contains(&search)
                    || item.path().to_lowercase().contains(&search)
            })
            .cloned()
            .collect::<Vec<_>>();
        let (sort, folders_first) = self
            .model
            .library_query
            .as_ref()
            .map(|query| (query.sort, !query.include_subfolders))
            .unwrap_or_default();
        self.model.visible_items = sort_items(&filtered, sort, folders_first);
    }

    fn persist_library_settings(&mut self) {
        self.commands.push_back(Command::PersistLibrarySettings {
            include_subfolders: self.include_subfolders,
            sort: self.sort,
            thumbnail_size: self.model.thumbnail_size,
        });
    }

    fn set_sort(&mut self, sort: SortState) {
        self.sort = sort;
        if let Some(query) = &mut self.model.library_query {
            query.sort = sort;
        }
        self.model.selection = SelectionState::default();
        self.model.drag = None;
        self.rebuild_visible_items();
        self.persist_library_settings();
    }

    fn toggle_include_subfolders(&mut self) {
        let Some(mut query) = self.model.library_query.clone() else {
            return;
        };
        query.include_subfolders = !query.include_subfolders;
        self.include_subfolders = query.include_subfolders;
        self.start_library_load(query);
        self.persist_library_settings();
    }

    fn set_thumbnail_size(&mut self, size: i32) {
        self.model.thumbnail_size = normalize_thumbnail_size(f64::from(size));
        self.persist_library_settings();
    }

    fn open_viewer(&mut self, path: std::path::PathBuf) {
        let Some(query) = self.model.library_query.as_ref() else {
            return;
        };
        let current_path = path.to_string_lossy();
        let Some(snapshot) = piclens_domain::ImageSequenceSnapshot::from_visible(
            query.folder_path.clone(),
            query.include_subfolders,
            query.sort,
            &self.model.visible_items,
            &current_path,
        ) else {
            return;
        };
        self.model.viewer = Some(ViewerState {
            snapshot,
            preview: Loadable::Idle,
            zoom: reset_zoom_state(),
        });
        self.model.drag = None;
        self.model.page = Page::Viewer;
    }

    fn close_viewer(&mut self) {
        self.model.viewer = None;
        self.model.page = Page::Library;
    }

    fn step_viewer(&mut self, delta: i32) {
        let Some(viewer) = self.model.viewer.as_mut() else {
            return;
        };
        viewer.snapshot.step(delta);
        viewer.preview = Loadable::Idle;
        viewer.zoom = reset_zoom_state();
    }

    fn adjust_viewer_zoom(&mut self, delta: i32) {
        let Some(viewer) = self.model.viewer.as_mut() else {
            return;
        };
        viewer.zoom.zoom = clamp_zoom(if delta > 0 {
            viewer.zoom.zoom * piclens_domain::ZOOM_STEP
        } else {
            viewer.zoom.zoom / piclens_domain::ZOOM_STEP
        });
    }

    fn zoom_viewer_at(&mut self, pointer: Point, viewport_center: Point, delta: i32) {
        let Some(viewer) = self.model.viewer.as_mut() else {
            return;
        };
        viewer.zoom = zoom_at_point(
            viewer.zoom.zoom,
            viewer.zoom.offset,
            viewport_center,
            pointer,
            delta,
        );
    }

    fn pan_viewer(&mut self, delta: Point) {
        let Some(viewer) = self.model.viewer.as_mut() else {
            return;
        };
        if viewer.zoom.zoom > 1.01 {
            viewer.zoom.offset = pan_offset(viewer.zoom.offset, delta);
        }
    }

    fn reset_viewer_zoom(&mut self) {
        if let Some(viewer) = self.model.viewer.as_mut() {
            viewer.zoom = reset_zoom_state();
        }
    }

    fn reveal_viewer(&mut self) {
        let Some(current) = self
            .model
            .viewer
            .as_ref()
            .and_then(|viewer| viewer.snapshot.current())
        else {
            return;
        };
        self.reveal_path(current.path.clone().into());
    }

    fn reveal_path(&mut self, path: PathBuf) {
        self.commands.push_back(Command::Reveal {
            path: path.to_string_lossy().into_owned(),
        });
    }

    fn open_rename(&mut self) {
        let [source] = self.model.selection.ordered_paths.as_slice() else {
            self.model.notice = Some("重新命名僅適用單張選取圖片。".into());
            return;
        };
        let basename = source
            .file_stem()
            .and_then(|name| name.to_str())
            .unwrap_or_default()
            .to_owned();
        self.model.dialog = Some(DialogState::Rename {
            source: source.clone(),
            basename,
        });
    }

    fn set_rename_basename(&mut self, basename: String) {
        if let Some(DialogState::Rename {
            basename: current, ..
        }) = &mut self.model.dialog
        {
            *current = basename;
        }
    }

    fn confirm_rename(&mut self) {
        let Some(DialogState::Rename { source, basename }) = &self.model.dialog else {
            return;
        };
        let Some(extension) = source.extension().and_then(|extension| extension.to_str()) else {
            self.model.notice = Some("來源圖片沒有可保留的副檔名。".into());
            return;
        };
        let new_file_name = format!("{}.{}", basename.trim(), extension);
        if !validate_image_file_name(&new_file_name).is_valid {
            self.model.notice = Some("檔名不可為空白，也不可包含路徑或保留字元。".into());
            return;
        }
        self.start_file_operation(
            "重新命名",
            format!("正在重新命名 {}…", source.display()),
            FileOperation::Rename {
                source: source.to_string_lossy().into_owned(),
                new_file_name,
            },
        );
    }

    fn request_trash(&mut self) {
        if self.model.selection.ordered_paths.is_empty() {
            self.model.notice = Some("請先選取圖片。".into());
            return;
        }
        self.model.dialog = Some(DialogState::TrashConfirmation {
            paths: self.model.selection.ordered_paths.clone(),
        });
    }

    fn confirm_trash(&mut self) {
        let Some(DialogState::TrashConfirmation { paths }) = &self.model.dialog else {
            return;
        };
        self.start_file_operation(
            "移至回收筒",
            format!("正在處理 {} 張圖片…", paths.len()),
            FileOperation::Trash {
                paths: paths
                    .iter()
                    .map(|path| path.to_string_lossy().into_owned())
                    .collect(),
            },
        );
    }

    fn visible_image_paths(&self) -> Vec<PathBuf> {
        self.model
            .visible_items
            .iter()
            .filter_map(ListItem::as_image)
            .map(|image| PathBuf::from(&image.path))
            .collect()
    }

    fn request_conversion(&mut self, kind: ConversionKind) {
        let paths = self.visible_image_paths();
        if paths.is_empty() {
            self.model.notice = Some(format!(
                "{}：目前結果沒有可處理的圖片。",
                conversion_label(kind)
            ));
            return;
        }
        if paths.len() >= CONVERSION_CONFIRMATION_THRESHOLD {
            self.model.dialog = Some(DialogState::ConversionConfirmation { kind, paths });
        } else {
            self.start_conversion(kind, paths);
        }
    }

    fn confirm_conversion(&mut self) {
        let Some(DialogState::ConversionConfirmation { kind, paths }) = &self.model.dialog else {
            return;
        };
        self.start_conversion(*kind, paths.clone());
    }

    fn start_conversion(&mut self, kind: ConversionKind, paths: Vec<PathBuf>) {
        let count = paths.len();
        let paths = paths
            .iter()
            .map(|path| path.to_string_lossy().into_owned())
            .collect();
        let operation = match kind {
            ConversionKind::Jpg => FileOperation::ConvertJpg { paths },
            ConversionKind::Webp => FileOperation::ConvertWebp { paths },
        };
        self.start_file_operation(
            conversion_label(kind),
            format!("正在處理 {count} 張圖片…"),
            operation,
        );
    }

    fn request_cleanup(&mut self) {
        let paths = self.visible_image_paths();
        if paths.is_empty() {
            self.model.notice = Some("同名格式清除：目前結果沒有可處理的圖片。".into());
            return;
        }
        self.model.dialog = Some(DialogState::CleanupConfirmation { paths });
    }

    fn confirm_cleanup(&mut self) {
        let Some(DialogState::CleanupConfirmation { paths }) = &self.model.dialog else {
            return;
        };
        self.start_file_operation(
            "清除同名格式",
            format!("正在檢查 {} 張圖片…", paths.len()),
            FileOperation::CleanupSameBasename {
                paths: paths
                    .iter()
                    .map(|path| path.to_string_lossy().into_owned())
                    .collect(),
            },
        );
    }

    fn start_drag(&mut self, source: PathBuf, pointer: Point) {
        if self.pending_file_operation.is_some() || self.model.dialog.is_some() {
            return;
        }
        let source_is_selected = self
            .model
            .selection
            .ordered_paths
            .iter()
            .any(|path| paths_equal(path, &source));
        let sources = if source_is_selected {
            self.model.selection.ordered_paths.clone()
        } else {
            vec![source]
        };
        self.model.drag = Some(DragSession {
            sources,
            origin: pointer,
            pointer,
            target: None,
            dragging: false,
            replaces_selection: !source_is_selected,
        });
    }

    fn update_drag(&mut self, pointer: Point, target: Option<PathBuf>) {
        let visible = self.visible_image_paths();
        let replacement = {
            let Some(drag) = &mut self.model.drag else {
                return;
            };
            if !drag
                .sources
                .iter()
                .all(|source| visible.iter().any(|path| paths_equal(path, source)))
            {
                self.model.drag = None;
                return;
            }
            drag.pointer = pointer;
            let dx = pointer.x - drag.origin.x;
            let dy = pointer.y - drag.origin.y;
            let was_dragging = drag.dragging;
            drag.dragging |= dx * dx + dy * dy >= DRAG_THRESHOLD * DRAG_THRESHOLD;
            drag.target = drag.dragging.then_some(target).flatten().filter(|target| {
                !drag
                    .sources
                    .iter()
                    .any(|source| paths_equal(source, target))
            });
            (!was_dragging && drag.dragging && drag.replaces_selection)
                .then(|| drag.sources.clone())
        };
        if let Some(sources) = replacement {
            self.model.selection.ordered_paths = sources.clone();
            self.model.selection.range_anchor = sources.first().cloned();
        }
    }

    fn finish_drag(&mut self) {
        let Some(drag) = self.model.drag.take() else {
            return;
        };
        if !drag.dragging {
            return;
        }
        let Some(target) = drag.target else {
            return;
        };
        let sources = drag
            .sources
            .iter()
            .map(|path| path.to_string_lossy().into_owned())
            .collect::<Vec<_>>();
        let plan = plan_drop_rename(&sources, target.to_string_lossy().as_ref());
        if plan.items.is_empty() {
            self.model.notice = Some("拖放來源中沒有可重新命名的圖片。".into());
            return;
        }
        self.model.dialog = Some(DialogState::DropRenameConfirmation { plan });
    }

    fn confirm_drop_rename(&mut self) {
        let Some(DialogState::DropRenameConfirmation { plan }) = &self.model.dialog else {
            return;
        };
        let plan = plan.clone();
        self.start_file_operation(
            "依目標重新命名",
            format!("正在處理 {} 張圖片…", plan.total),
            FileOperation::DropRename { plan },
        );
    }

    fn start_file_operation(&mut self, title: &str, message: String, operation: FileOperation) {
        if self.pending_file_operation.is_some() {
            self.model.notice = Some("檔案操作仍在進行中。".into());
            return;
        }
        let identity = WorkIdentity {
            generation: self.generation,
            request_id: self.next_request_id,
        };
        self.next_request_id = self.next_request_id.wrapping_add(1).max(1);
        self.pending_file_operation = Some(identity);
        self.model.drag = None;
        self.model.dialog = Some(DialogState::Progress {
            title: title.into(),
            message,
        });
        self.commands.push_back(Command::StartFileOperation {
            identity,
            operation,
        });
    }

    fn cancel_file_operation(&mut self) {
        let Some(identity) = self.pending_file_operation else {
            return;
        };
        self.model.dialog = Some(DialogState::Progress {
            title: "取消檔案操作".into(),
            message: "正在取消；已開始的項目會先完成…".into(),
        });
        self.commands
            .push_back(Command::CancelFileOperation { identity });
    }

    fn select_image(&mut self, path: &std::path::Path, gesture: SelectionGesture) {
        let visible_images = self
            .model
            .visible_items
            .iter()
            .filter_map(|item| item.as_image())
            .map(|image| std::path::PathBuf::from(&image.path))
            .collect::<Vec<_>>();
        let Some(target_index) = visible_images
            .iter()
            .position(|item| paths_equal(item, path))
        else {
            return;
        };
        let target = visible_images[target_index].clone();
        match gesture {
            SelectionGesture::Replace => {
                self.model.selection.ordered_paths.clear();
                self.model.selection.ordered_paths.push(target.clone());
                self.model.selection.range_anchor = Some(target);
            }
            SelectionGesture::Toggle => {
                if let Some(index) = selected_index(&self.model.selection.ordered_paths, &target) {
                    self.model.selection.ordered_paths.remove(index);
                } else {
                    self.model.selection.ordered_paths.push(target.clone());
                }
                self.model.selection.range_anchor = Some(target);
            }
            SelectionGesture::Range { additive } => {
                let anchor_index = self
                    .model
                    .selection
                    .range_anchor
                    .as_ref()
                    .and_then(|anchor| {
                        visible_images
                            .iter()
                            .position(|item| paths_equal(item, anchor))
                    })
                    .unwrap_or(target_index);
                if self
                    .model
                    .selection
                    .range_anchor
                    .as_ref()
                    .is_none_or(|anchor| !paths_equal(anchor, &visible_images[anchor_index]))
                {
                    self.model.selection.range_anchor = Some(target.clone());
                }
                if !additive {
                    self.model.selection.ordered_paths.clear();
                }
                let (start, end) = if anchor_index <= target_index {
                    (anchor_index, target_index)
                } else {
                    (target_index, anchor_index)
                };
                for range_path in &visible_images[start..=end] {
                    if selected_index(&self.model.selection.ordered_paths, range_path).is_none() {
                        self.model.selection.ordered_paths.push(range_path.clone());
                    }
                }
            }
        }
    }

    fn handle_event(&mut self, event: Event) -> bool {
        match event {
            Event::ProbeCompleted { identity, result } if self.pending_probe == Some(identity) => {
                self.pending_probe = None;
                self.model.backend = match result {
                    Ok(()) => Loadable::Ready(()),
                    Err(message) => Loadable::Failed(message),
                };
                true
            }
            Event::ProbeCompleted { .. } => false,
            Event::LibraryLoaded {
                identity, result, ..
            } if self.pending_library == Some(identity) => {
                self.pending_library = None;
                match result {
                    Ok(items) => {
                        self.model.library = Loadable::Ready(items);
                        self.rebuild_visible_items();
                    }
                    Err(message) => {
                        self.model.library = Loadable::Failed(message);
                        self.model.visible_items.clear();
                    }
                }
                true
            }
            Event::LibraryLoaded { .. } => false,
            Event::TreeChildrenLoaded {
                identity,
                parent,
                result,
            } if self.pending_tree.get(&parent) == Some(&identity) => {
                self.pending_tree.remove(&parent);
                match result {
                    Ok(children) => {
                        apply_tree_children(&mut self.model.tree_children, &parent, children)
                    }
                    Err(message) => self.model.notice = Some(message),
                }
                true
            }
            Event::TreeChildrenLoaded { .. } => false,
            Event::SettingsSaved { result } => {
                if let Err(message) = result {
                    self.model.notice = Some(message);
                    true
                } else {
                    false
                }
            }
            Event::FolderPicked { .. } => false,
            Event::ThumbnailLoaded { .. } => false,
            Event::RevealCompleted { result } => {
                self.model.notice = Some(match result {
                    Ok(()) => "已在檔案管理器中顯示。".into(),
                    Err(message) => message,
                });
                true
            }
            Event::FileOperationCompleted { identity, result }
                if self.pending_file_operation == Some(identity) =>
            {
                self.pending_file_operation = None;
                self.model.selection = SelectionState::default();
                self.model.drag = None;
                match result {
                    Ok(result) => self.model.dialog = Some(DialogState::BatchResult(result)),
                    Err(message) => {
                        self.model.dialog = None;
                        self.model.notice = Some(message);
                    }
                }
                self.push_action(Action::ReloadLibrary);
                true
            }
            Event::FileOperationCompleted { .. } => false,
            Event::SmokeDeadlineElapsed => {
                self.close_requested = true;
                true
            }
        }
    }

    fn fail_command(&mut self, command: &Command, message: String) -> bool {
        match command {
            Command::Probe { identity } => self.handle_event(Event::ProbeCompleted {
                identity: *identity,
                result: Err(message),
            }),
            Command::LoadLibrary { identity, query } => self.handle_event(Event::LibraryLoaded {
                identity: *identity,
                query: query.clone(),
                result: Err(message),
            }),
            Command::LoadTreeChildren { identity, parent } => {
                self.handle_event(Event::TreeChildrenLoaded {
                    identity: *identity,
                    parent: parent.clone(),
                    result: Err(message),
                })
            }
            Command::PersistLibrarySettings { .. }
            | Command::PersistPickerFolder { .. }
            | Command::PersistSidebar { .. } => {
                self.model.notice = Some(message);
                true
            }
            Command::SyncThumbnails { .. } => false,
            Command::Reveal { .. } => {
                self.model.notice = Some(message);
                true
            }
            Command::StartFileOperation { identity, .. } => {
                self.handle_event(Event::FileOperationCompleted {
                    identity: *identity,
                    result: Err(message),
                })
            }
            Command::CancelFileOperation { .. } => false,
            Command::Shutdown => false,
        }
    }
}

fn paths_equal(left: &std::path::Path, right: &std::path::Path) -> bool {
    path_equals(&left.to_string_lossy(), &right.to_string_lossy())
}

fn conversion_label(kind: ConversionKind) -> &'static str {
    match kind {
        ConversionKind::Jpg => "轉 JPG",
        ConversionKind::Webp => "轉無損 WebP",
    }
}

fn selected_index(paths: &[std::path::PathBuf], target: &std::path::Path) -> Option<usize> {
    paths.iter().position(|path| paths_equal(path, target))
}

fn request_gallery_focus(ctx: &egui::Context) {
    ctx.memory_mut(|memory| memory.request_focus(ui::gallery_focus_id()));
    ctx.request_repaint();
}

const VIEWER_SELECTION_HOLD: Duration = Duration::from_millis(650);

struct ViewerSelectionMetric {
    index: i32,
    path: String,
    started: Instant,
    preview_recorded: bool,
    painted: bool,
}

struct ViewerNavigationWorkload {
    steps: usize,
    checked: usize,
    next_check: Instant,
}

fn viewer_navigation_delta(checked: usize, steps: usize) -> Option<i32> {
    if checked < steps {
        Some(1)
    } else if checked < steps * 2 {
        Some(-1)
    } else {
        None
    }
}

pub struct PicLensApp {
    reducer: Reducer,
    backend: Backend,
    images: ThumbnailLoader,
    folder_picker_open: bool,
    initial_viewer: Option<PathBuf>,
    performance_viewer: bool,
    navigation_workload: Option<ViewerNavigationWorkload>,
    viewer_selection: Option<ViewerSelectionMetric>,
    metrics: Option<RuntimeMetrics>,
}

impl PicLensApp {
    pub fn new(creation: &eframe::CreationContext<'_>, options: LaunchOptions) -> Self {
        piclens_infra::info("egui application state created");
        theme::install(&creation.egui_ctx);
        let LaunchOptions {
            initial_folder,
            include_subfolders,
            sort,
            thumbnail_size,
            sidebar_collapsed,
            smoke_after,
            initial_viewer,
            performance_viewer,
            metrics_output,
        } = options;
        let backend = Backend::spawn(creation.egui_ctx.clone(), smoke_after);
        let mut reducer = Reducer::new(initial_folder);
        reducer.include_subfolders = include_subfolders;
        reducer.sort = sort;
        reducer.model.thumbnail_size = thumbnail_size;
        reducer.model.sidebar_collapsed = sidebar_collapsed;
        let mut app = Self {
            reducer,
            backend,
            images: ThumbnailLoader::default(),
            folder_picker_open: false,
            initial_viewer,
            performance_viewer,
            navigation_workload: None,
            viewer_selection: None,
            metrics: metrics_output.map(RuntimeMetrics::new),
        };
        app.reducer.push_action(Action::StartBackendProbe);
        if let Some(folder) = app.reducer.model.initial_folder.clone() {
            app.reducer.push_action(Action::RestoreFolder(folder));
        }
        app.reduce_and_dispatch(&creation.egui_ctx);
        app
    }

    fn handle_events(&mut self, ctx: &egui::Context) -> bool {
        let mut changed = false;
        for event in self.backend.poll().collect::<Vec<_>>() {
            match event {
                Event::FolderPicked { path } => {
                    self.folder_picker_open = false;
                    if let Some(path) = path {
                        self.reducer.push_action(Action::PickedFolder(path));
                        changed = true;
                    }
                }
                Event::ThumbnailLoaded { request, result } => {
                    changed |= self.images.handle_result(&request, result, ctx);
                }
                event => changed |= self.reducer.handle_event(event),
            }
        }
        changed
    }

    fn reduce_and_dispatch(&mut self, ctx: &egui::Context) {
        let mut changed = self.reducer.reduce_actions() > 0;
        while let Some(command) = self.reducer.commands.pop_front() {
            if let Err(error) = self.backend.send(command.clone()) {
                let message = format!("背景服務無法接收工作：{error}");
                match &command {
                    Command::SyncThumbnails { requests } => {
                        self.images.fail_requests(requests, &message);
                        changed = true;
                    }
                    _ => changed |= self.reducer.fail_command(&command, message),
                }
            }
        }
        if changed {
            ctx.request_repaint();
        }
    }

    fn close_if_requested(&mut self, ctx: &egui::Context) {
        if self.reducer.close_requested {
            self.reducer.close_requested = false;
            piclens_infra::info("egui main viewport close requested");
            ctx.send_viewport_cmd(egui::ViewportCommand::Close);
        }
    }

    fn record_library_ready(&mut self) {
        let Loadable::Ready(items) = &self.reducer.model.library else {
            return;
        };
        if let Some(metrics) = &mut self.metrics {
            metrics.library_ready(
                items.len(),
                items
                    .iter()
                    .filter(|item| item.as_image().is_some())
                    .count(),
            );
        }
    }

    fn begin_viewer_selection(&mut self) {
        let Some(viewer) = &self.reducer.model.viewer else {
            self.viewer_selection = None;
            return;
        };
        let Some(current) = viewer.snapshot.current() else {
            self.viewer_selection = None;
            return;
        };
        self.viewer_selection = Some(ViewerSelectionMetric {
            index: viewer.snapshot.current_index,
            path: current.path.clone(),
            started: Instant::now(),
            preview_recorded: false,
            painted: false,
        });
    }

    fn observe_viewer_selection(&mut self) {
        let current = self.reducer.model.viewer.as_ref().and_then(|viewer| {
            viewer.snapshot.current().map(|image| {
                (
                    viewer.snapshot.current_index,
                    image.path.clone(),
                    viewer.snapshot.images.len(),
                )
            })
        });
        let Some((index, path, image_count)) = current else {
            self.viewer_selection = None;
            return;
        };
        let changed = self.viewer_selection.as_ref().is_none_or(|selection| {
            selection.index != index || !path_equals(&selection.path, &path)
        });
        if changed {
            self.begin_viewer_selection();
            if let Some(metrics) = &mut self.metrics {
                metrics.viewer_opened();
            }
            if self.performance_viewer && self.navigation_workload.is_none() {
                let steps = image_count.min(64);
                if steps > 0 {
                    piclens_infra::info("egui viewer navigation workload started");
                    self.navigation_workload = Some(ViewerNavigationWorkload {
                        steps,
                        checked: 0,
                        next_check: Instant::now() + VIEWER_SELECTION_HOLD,
                    });
                }
            }
        }
    }

    fn open_initial_viewer_if_ready(&mut self, ctx: &egui::Context) {
        let Some(path) = self.initial_viewer.clone() else {
            return;
        };
        let Loadable::Ready(_) = &self.reducer.model.library else {
            return;
        };
        let found = self
            .reducer
            .model
            .visible_items
            .iter()
            .filter_map(|item| item.as_image())
            .any(|image| path_equals(&image.path, &path.to_string_lossy()));
        self.initial_viewer = None;
        if !found {
            piclens_infra::warn(format!(
                "egui initial viewer image is not visible; path={}",
                path.display()
            ));
            return;
        }
        self.reducer.push_action(Action::OpenViewer(path));
        self.reduce_and_dispatch(ctx);
        self.observe_viewer_selection();
    }

    fn drive_viewer_navigation(&mut self, ctx: &egui::Context) {
        let now = Instant::now();
        let Some(workload) = self.navigation_workload.as_ref() else {
            return;
        };
        if now < workload.next_check {
            ctx.request_repaint_after(workload.next_check - now);
            return;
        }

        let painted = self
            .viewer_selection
            .as_ref()
            .is_some_and(|selection| selection.painted);
        if let Some(metrics) = &mut self.metrics {
            metrics.viewer_navigation_checked(painted);
        }
        if !painted {
            let index = self
                .reducer
                .model
                .viewer
                .as_ref()
                .map(|viewer| viewer.snapshot.current_index)
                .unwrap_or_default();
            piclens_infra::warn(format!(
                "egui viewer navigation selection did not paint; index={index}"
            ));
        }

        let workload = self.navigation_workload.as_mut().unwrap();
        let delta = viewer_navigation_delta(workload.checked, workload.steps);
        workload.checked += 1;
        workload.next_check = now + VIEWER_SELECTION_HOLD;
        if let Some(delta) = delta {
            self.reducer.push_action(Action::StepViewer(delta));
            self.reduce_and_dispatch(ctx);
            self.begin_viewer_selection();
            ctx.request_repaint_after(VIEWER_SELECTION_HOLD);
        } else {
            self.navigation_workload = None;
            piclens_infra::info("egui viewer navigation workload completed");
        }
    }

    fn current_viewer_texture_ready(&self) -> bool {
        self.reducer
            .model
            .viewer
            .as_ref()
            .and_then(|viewer| viewer.snapshot.current())
            .filter(|image| !image.is_animated)
            .map(|image| crate::images::ThumbnailKey::from_image(image, 1024))
            .is_some_and(|key| self.images.texture(&key).is_some())
    }

    fn record_viewer_preview_ready(&mut self) {
        if !self.current_viewer_texture_ready() {
            return;
        }
        let Some(selection) = self
            .viewer_selection
            .as_mut()
            .filter(|selection| !selection.preview_recorded)
        else {
            return;
        };
        selection.preview_recorded = true;
        let elapsed = selection.started.elapsed().as_millis();
        piclens_infra::info(format!(
            "egui viewer decoded preview ready in {elapsed} ms: {}",
            selection.path
        ));
        if let Some(metrics) = &mut self.metrics {
            metrics.viewer_preview_ready(elapsed);
        }
    }

    fn record_viewer_sharp_paint(&mut self) {
        if !self.current_viewer_texture_ready() {
            return;
        }
        let Some(selection) = self
            .viewer_selection
            .as_mut()
            .filter(|selection| !selection.painted)
        else {
            return;
        };
        selection.painted = true;
        let elapsed = selection.started.elapsed().as_millis();
        piclens_infra::info(format!(
            "egui viewer sharp preview painted in {elapsed} ms: {}",
            selection.path
        ));
        if let Some(metrics) = &mut self.metrics {
            metrics.viewer_sharp_painted(elapsed);
        }
    }
}

impl eframe::App for PicLensApp {
    fn logic(&mut self, ctx: &egui::Context, _frame: &mut eframe::Frame) {
        if self.handle_events(ctx) {
            ctx.request_repaint();
        }
        self.reduce_and_dispatch(ctx);
        self.record_library_ready();
        self.open_initial_viewer_if_ready(ctx);
        self.observe_viewer_selection();
        self.drive_viewer_navigation(ctx);
        self.close_if_requested(ctx);
    }

    fn ui(&mut self, ui: &mut egui::Ui, frame: &mut eframe::Frame) {
        if let Some(metrics) = &mut self.metrics {
            let size = ui.max_rect().size();
            metrics.window_ready(
                size.x.max(0.0).round() as u32,
                size.y.max(0.0).round() as u32,
                ui.ctx().pixels_per_point(),
            );
        }
        self.record_viewer_preview_ready();
        let mut frame_actions = Vec::new();
        let materialized = ui::show(&self.reducer.model, &self.images, ui, &mut frame_actions);
        self.record_viewer_sharp_paint();
        if !self.folder_picker_open && frame_actions.contains(&Action::ChooseFolder) {
            frame_actions.retain(|action| *action != Action::ChooseFolder);
            let mut dialog = rfd::FileDialog::new()
                .set_title("選擇圖片資料夾")
                .set_parent(frame);
            if let Some(folder) = &self.reducer.model.current_folder {
                dialog = dialog.set_directory(folder);
            }
            match self.backend.choose_folder(dialog) {
                Ok(()) => self.folder_picker_open = true,
                Err(message) => frame_actions.push(Action::ShowNotice(message.into())),
            }
        }
        let restore_gallery_focus = frame_actions.contains(&Action::CloseViewer);
        self.reducer.actions.extend(frame_actions);
        if let Some(requests) = self
            .images
            .sync_materialized(materialized, self.reducer.generation)
        {
            self.reducer
                .commands
                .push_back(Command::SyncThumbnails { requests });
        }
        self.reduce_and_dispatch(ui.ctx());
        self.observe_viewer_selection();
        if restore_gallery_focus {
            request_gallery_focus(ui.ctx());
        }
    }

    fn on_exit(&mut self) {
        if let Some(metrics) = &self.metrics {
            match metrics.write_snapshot() {
                Ok(()) => piclens_infra::info("egui runtime metrics written"),
                Err(error) => piclens_infra::warn(format!(
                    "failed to write egui runtime metrics; error={error}"
                )),
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn viewer_navigation_checks_initial_forward_and_backward_selections() {
        let steps = 3;
        let deltas = (0..=steps * 2)
            .map(|checked| viewer_navigation_delta(checked, steps))
            .collect::<Vec<_>>();

        assert_eq!(
            deltas,
            vec![
                Some(1),
                Some(1),
                Some(1),
                Some(-1),
                Some(-1),
                Some(-1),
                None
            ]
        );
    }

    #[test]
    fn viewer_close_focus_request_targets_the_gallery_search() {
        use egui_kittest::{kittest::Queryable, Harness};

        let model = crate::demo::loaded_library();
        let mut harness = Harness::new_ui(move |ui| {
            let mut actions = Vec::new();
            ui::show(&model, &ThumbnailLoader::default(), ui, &mut actions);
        });
        harness.run();

        request_gallery_focus(&harness.ctx);
        harness.run();

        assert!(harness
            .get_by_role(egui::accesskit::Role::TextInput)
            .is_focused());
    }

    fn next_probe(reducer: &mut Reducer) -> WorkIdentity {
        match reducer.commands.pop_front().unwrap() {
            Command::Probe { identity } => identity,
            Command::LoadLibrary { .. }
            | Command::LoadTreeChildren { .. }
            | Command::PersistLibrarySettings { .. }
            | Command::PersistPickerFolder { .. }
            | Command::PersistSidebar { .. }
            | Command::Reveal { .. }
            | Command::StartFileOperation { .. }
            | Command::CancelFileOperation { .. } => panic!("expected probe command"),
            Command::SyncThumbnails { .. } => panic!("expected probe command"),
            Command::Shutdown => panic!("expected probe command"),
        }
    }

    fn query(folder: &str) -> ListQuery {
        ListQuery {
            folder_path: folder.into(),
            include_subfolders: false,
            sort: Default::default(),
        }
    }

    fn next_library_load(reducer: &mut Reducer) -> (WorkIdentity, ListQuery) {
        match reducer.commands.pop_front().unwrap() {
            Command::LoadLibrary { identity, query } => (identity, query),
            Command::Probe { .. }
            | Command::LoadTreeChildren { .. }
            | Command::PersistLibrarySettings { .. }
            | Command::PersistPickerFolder { .. }
            | Command::PersistSidebar { .. }
            | Command::SyncThumbnails { .. }
            | Command::Reveal { .. }
            | Command::StartFileOperation { .. }
            | Command::CancelFileOperation { .. }
            | Command::Shutdown => {
                panic!("expected library command")
            }
        }
    }

    fn next_persist(reducer: &mut Reducer) -> (bool, SortState, i32) {
        match reducer.commands.pop_front().unwrap() {
            Command::PersistLibrarySettings {
                include_subfolders,
                sort,
                thumbnail_size,
            } => (include_subfolders, sort, thumbnail_size),
            Command::Probe { .. }
            | Command::LoadLibrary { .. }
            | Command::LoadTreeChildren { .. }
            | Command::PersistPickerFolder { .. }
            | Command::PersistSidebar { .. }
            | Command::SyncThumbnails { .. }
            | Command::Reveal { .. }
            | Command::StartFileOperation { .. }
            | Command::CancelFileOperation { .. }
            | Command::Shutdown => {
                panic!("expected settings command")
            }
        }
    }

    #[test]
    fn picker_sets_restore_authority_tree_and_history() {
        let mut reducer = Reducer::new(None);
        reducer.push_action(Action::PickedFolder("C:/photos".into()));

        assert_eq!(reducer.reduce_actions(), 1);
        assert_eq!(reducer.model.tree_root.as_deref(), Some("C:/photos"));
        assert_eq!(reducer.model.tree_roots, vec!["C:/photos"]);
        assert_eq!(reducer.model.history.current(), Some("C:/photos"));
        assert!(matches!(
            reducer.commands.pop_front(),
            Some(Command::LoadTreeChildren { parent, .. }) if parent == "C:/photos"
        ));
        assert_eq!(
            reducer.commands.pop_front(),
            Some(Command::PersistPickerFolder {
                path: "C:/photos".into()
            })
        );
        assert!(matches!(
            reducer.commands.pop_front(),
            Some(Command::LoadLibrary { query, .. }) if query.folder_path == "C:/photos"
        ));
    }

    #[test]
    fn navigation_and_history_keep_picker_tree_unchanged() {
        let mut reducer = Reducer::new(None);
        reducer.push_action(Action::RestoreFolder("C:/photos".into()));
        reducer.reduce_actions();
        reducer.commands.clear();
        let roots = reducer.model.tree_roots.clone();

        reducer.push_action(Action::NavigateFolder("C:/photos/trip".into()));
        reducer.reduce_actions();
        assert_eq!(reducer.model.tree_roots, roots);
        assert_eq!(reducer.model.history.current(), Some("C:/photos/trip"));
        assert!(reducer.model.history.can_back());
        assert!(matches!(
            reducer.commands.pop_front(),
            Some(Command::LoadLibrary { query, .. }) if query.folder_path == "C:/photos/trip"
        ));

        reducer.push_action(Action::NavigateHistory { back: true });
        reducer.reduce_actions();
        assert_eq!(reducer.model.history.current(), Some("C:/photos"));
        assert_eq!(reducer.model.tree_roots, roots);
        assert!(matches!(
            reducer.commands.pop_front(),
            Some(Command::LoadLibrary { query, .. }) if query.folder_path == "C:/photos"
        ));
    }

    #[test]
    fn newer_picker_root_rejects_stale_tree_children() {
        let mut reducer = Reducer::new(None);
        reducer.push_action(Action::RestoreFolder("C:/first".into()));
        reducer.reduce_actions();
        let Some(Command::LoadTreeChildren {
            identity: first,
            parent,
        }) = reducer.commands.pop_front()
        else {
            panic!("expected tree command");
        };

        reducer.push_action(Action::RestoreFolder("C:/second".into()));
        reducer.reduce_actions();
        assert!(!reducer.handle_event(Event::TreeChildrenLoaded {
            identity: first,
            parent,
            result: Ok(vec!["C:/first/child".into()]),
        }));
        assert_eq!(reducer.model.tree_root.as_deref(), Some("C:/second"));
        assert!(!reducer.model.tree_children.contains_key("C:/first"));
    }

    #[test]
    fn latest_request_rejects_stale_success_and_error() {
        let mut reducer = Reducer::new(None);
        reducer.push_action(Action::RetryBackendProbe);
        reducer.reduce_actions();
        let first = next_probe(&mut reducer);
        reducer.push_action(Action::RetryBackendProbe);
        reducer.reduce_actions();
        let second = next_probe(&mut reducer);

        assert!(!reducer.handle_event(Event::ProbeCompleted {
            identity: first,
            result: Ok(()),
        }));
        assert_eq!(reducer.model.backend, Loadable::Loading);
        assert!(reducer.handle_event(Event::ProbeCompleted {
            identity: second,
            result: Err("測試失敗".into()),
        }));
        assert_eq!(reducer.model.backend, Loadable::Failed("測試失敗".into()));
        assert!(!reducer.handle_event(Event::ProbeCompleted {
            identity: first,
            result: Err("過期錯誤".into()),
        }));
        assert_eq!(reducer.model.backend, Loadable::Failed("測試失敗".into()));
    }

    #[test]
    fn matching_success_clears_pending_request() {
        let mut reducer = Reducer::new(None);
        reducer.push_action(Action::StartBackendProbe);
        reducer.reduce_actions();
        let identity = next_probe(&mut reducer);

        assert!(reducer.handle_event(Event::ProbeCompleted {
            identity,
            result: Ok(()),
        }));
        assert_eq!(reducer.model.backend, Loadable::Ready(()));
        assert_eq!(reducer.pending_probe, None);
    }

    #[test]
    fn library_load_resets_collection_and_selection_once() {
        let mut reducer = Reducer::new(None);
        reducer.model.selection.ordered_paths.push("old.png".into());
        reducer.model.selection.range_anchor = Some("old.png".into());
        reducer.model.drag = Some(DragSession {
            sources: vec!["old.png".into()],
            origin: Point::default(),
            pointer: Point::default(),
            target: None,
            dragging: true,
            replaces_selection: false,
        });
        reducer.push_action(Action::LoadLibrary(query("new-folder")));

        assert_eq!(reducer.reduce_actions(), 1);
        let (identity, command_query) = next_library_load(&mut reducer);
        assert_eq!(identity.generation, 1);
        assert_eq!(command_query, query("new-folder"));
        assert_eq!(reducer.model.library, Loadable::Loading);
        assert!(reducer.model.selection.ordered_paths.is_empty());
        assert_eq!(reducer.model.selection.range_anchor, None);
        assert!(reducer.model.drag.is_none());
        assert_eq!(
            reducer.model.current_folder.as_deref(),
            Some(std::path::Path::new("new-folder"))
        );
    }

    #[test]
    fn newer_library_generation_rejects_stale_result() {
        let mut reducer = Reducer::new(None);
        reducer.push_action(Action::LoadLibrary(query("first")));
        reducer.reduce_actions();
        let (first, first_query) = next_library_load(&mut reducer);
        reducer.push_action(Action::LoadLibrary(query("second")));
        reducer.reduce_actions();
        let (second, second_query) = next_library_load(&mut reducer);

        assert!(!reducer.handle_event(Event::LibraryLoaded {
            identity: first,
            query: first_query,
            result: Ok(Vec::new()),
        }));
        assert_eq!(reducer.model.library, Loadable::Loading);
        assert!(reducer.handle_event(Event::LibraryLoaded {
            identity: second,
            query: second_query,
            result: Err("第二次載入失敗".into()),
        }));
        assert_eq!(
            reducer.model.library,
            Loadable::Failed("第二次載入失敗".into())
        );
    }

    #[test]
    fn reload_uses_current_query_through_follow_up_action() {
        let mut reducer = Reducer::new(None);
        reducer.model.library_query = Some(query("current"));
        reducer.push_action(Action::ReloadLibrary);

        assert_eq!(reducer.reduce_actions(), 2);
        let (_, command_query) = next_library_load(&mut reducer);
        assert_eq!(command_query, query("current"));
    }

    #[test]
    fn search_and_sort_project_loaded_items_without_rescan() {
        use piclens_domain::{ImageListItem, ListItem, SortDirection, SortKey};

        let mut reducer = Reducer::new(None);
        reducer.push_action(Action::LoadLibrary(query("current")));
        reducer.reduce_actions();
        let (identity, loaded_query) = next_library_load(&mut reducer);
        let image = |name: &str| {
            ListItem::Image(ImageListItem {
                path: format!("current/{name}"),
                name: name.into(),
                extension: "png".into(),
                modified_at_ms: None,
                size_bytes: 1,
                is_animated: false,
            })
        };
        reducer.handle_event(Event::LibraryLoaded {
            identity,
            query: loaded_query,
            result: Ok(vec![
                image("other.png"),
                image("img10.png"),
                image("img2.png"),
            ]),
        });

        reducer.push_action(Action::SetSearch("IMG".into()));
        reducer.reduce_actions();
        assert_eq!(
            reducer
                .model
                .visible_items
                .iter()
                .map(ListItem::name)
                .collect::<Vec<_>>(),
            vec!["img2.png", "img10.png"]
        );
        assert!(reducer.commands.is_empty());

        reducer.push_action(Action::SetSort(SortState {
            key: SortKey::Name,
            direction: SortDirection::Desc,
        }));
        reducer.reduce_actions();
        assert_eq!(
            reducer
                .model
                .visible_items
                .iter()
                .map(ListItem::name)
                .collect::<Vec<_>>(),
            vec!["img10.png", "img2.png"]
        );
        let (_, saved_sort, _) = next_persist(&mut reducer);
        assert_eq!(saved_sort.direction, SortDirection::Desc);
    }

    #[test]
    fn include_subfolders_reloads_and_persists_current_query() {
        let mut reducer = Reducer::new(None);
        reducer.model.library_query = Some(query("current"));
        reducer.push_action(Action::ToggleIncludeSubfolders);

        reducer.reduce_actions();
        let (_, load_query) = next_library_load(&mut reducer);
        assert!(load_query.include_subfolders);
        let (saved_include, _, _) = next_persist(&mut reducer);
        assert!(saved_include);
    }

    #[test]
    fn thumbnail_size_is_normalized_before_persisting() {
        let mut reducer = Reducer::new(None);
        reducer.push_action(Action::SetThumbnailSize(999));

        reducer.reduce_actions();
        assert_eq!(reducer.model.thumbnail_size, 240);
        let (_, _, saved_size) = next_persist(&mut reducer);
        assert_eq!(saved_size, 240);
    }

    #[test]
    fn selection_replace_toggle_and_range_keep_stable_anchor_and_order() {
        use piclens_domain::{FolderListItem, ImageListItem, ListItem};

        let image = |name: &str| {
            ListItem::Image(ImageListItem {
                path: format!("C:/gallery/{name}.png"),
                name: format!("{name}.png"),
                extension: "png".into(),
                modified_at_ms: None,
                size_bytes: 1,
                is_animated: false,
            })
        };
        let path = |name: &str| std::path::PathBuf::from(format!("C:/gallery/{name}.png"));
        let mut reducer = Reducer::new(None);
        reducer.model.visible_items = vec![
            image("a"),
            ListItem::Folder(FolderListItem {
                path: "C:/gallery/folder".into(),
                name: "folder".into(),
                modified_at_ms: None,
            }),
            image("b"),
            image("c"),
            image("d"),
        ];

        reducer.push_action(Action::SelectImage {
            path: path("a"),
            gesture: SelectionGesture::Replace,
        });
        reducer.push_action(Action::SelectImage {
            path: path("b"),
            gesture: SelectionGesture::Toggle,
        });
        reducer.push_action(Action::SelectImage {
            path: path("a"),
            gesture: SelectionGesture::Toggle,
        });
        reducer.reduce_actions();
        assert_eq!(reducer.model.selection.ordered_paths, vec![path("b")]);
        assert_eq!(reducer.model.selection.range_anchor, Some(path("a")));

        reducer.push_action(Action::SelectImage {
            path: path("c"),
            gesture: SelectionGesture::Range { additive: false },
        });
        reducer.reduce_actions();
        assert_eq!(
            reducer.model.selection.ordered_paths,
            vec![path("a"), path("b"), path("c")]
        );
        assert_eq!(reducer.model.selection.range_anchor, Some(path("a")));

        reducer.push_action(Action::ClearSelection);
        reducer.push_action(Action::SelectImage {
            path: path("b"),
            gesture: SelectionGesture::Replace,
        });
        reducer.push_action(Action::SelectImage {
            path: path("d"),
            gesture: SelectionGesture::Toggle,
        });
        reducer.push_action(Action::SelectImage {
            path: path("b"),
            gesture: SelectionGesture::Range { additive: true },
        });
        reducer.reduce_actions();
        assert_eq!(
            reducer.model.selection.ordered_paths,
            vec![path("b"), path("d"), path("c")]
        );
        assert_eq!(reducer.model.selection.range_anchor, Some(path("d")));
    }

    #[test]
    fn selection_ignores_folders_and_non_visible_paths() {
        use piclens_domain::{FolderListItem, ListItem};

        let mut reducer = Reducer::new(None);
        reducer.model.visible_items = vec![ListItem::Folder(FolderListItem {
            path: "C:/gallery/folder".into(),
            name: "folder".into(),
            modified_at_ms: None,
        })];
        reducer.push_action(Action::SelectImage {
            path: "C:/gallery/folder".into(),
            gesture: SelectionGesture::Replace,
        });
        reducer.push_action(Action::SelectImage {
            path: "C:/gallery/missing.png".into(),
            gesture: SelectionGesture::Replace,
        });

        reducer.reduce_actions();

        assert!(reducer.model.selection.ordered_paths.is_empty());
        assert_eq!(reducer.model.selection.range_anchor, None);
    }

    #[test]
    fn rename_uses_the_single_selection_and_reloads_after_result() {
        use piclens_domain::{FileOperationResult, FileOperationStatus, ImageListItem, ListItem};

        let path = PathBuf::from("C:/gallery/old.png");
        let mut reducer = Reducer::new(None);
        reducer.model.visible_items = vec![ListItem::Image(ImageListItem {
            path: path.to_string_lossy().into_owned(),
            name: "old.png".into(),
            extension: "png".into(),
            modified_at_ms: None,
            size_bytes: 1,
            is_animated: false,
        })];
        reducer.push_action(Action::SelectImage {
            path: path.clone(),
            gesture: SelectionGesture::Replace,
        });
        reducer.push_action(Action::OpenRename);
        reducer.push_action(Action::SetRenameBasename("new".into()));
        reducer.push_action(Action::ConfirmRename);
        reducer.reduce_actions();

        let (identity, operation) = match reducer.commands.pop_front().unwrap() {
            Command::StartFileOperation {
                identity,
                operation,
            } => (identity, operation),
            command => panic!("expected rename command, got {command:?}"),
        };
        assert_eq!(
            operation,
            FileOperation::Rename {
                source: path.to_string_lossy().into_owned(),
                new_file_name: "new.png".into(),
            }
        );
        assert!(matches!(
            reducer.model.dialog,
            Some(DialogState::Progress { .. })
        ));

        assert!(reducer.handle_event(Event::FileOperationCompleted {
            identity,
            result: Ok(piclens_domain::FileOperationBatchResult {
                items: vec![FileOperationResult {
                    path: path.to_string_lossy().into_owned(),
                    status: FileOperationStatus::Renamed,
                    target_path: Some("C:/gallery/new.png".into()),
                    reason: None,
                    message: None,
                }],
            }),
        }));
        assert!(reducer.model.selection.ordered_paths.is_empty());
        assert!(matches!(
            reducer.model.dialog,
            Some(DialogState::BatchResult(_))
        ));
        assert_eq!(reducer.actions.pop_front(), Some(Action::ReloadLibrary));
    }

    #[test]
    fn trash_cancel_closes_confirmation_without_starting_work() {
        use piclens_domain::{ImageListItem, ListItem};

        let mut reducer = Reducer::new(None);
        reducer.model.visible_items = ["a.png", "b.png"]
            .into_iter()
            .map(|name| {
                ListItem::Image(ImageListItem {
                    path: format!("C:/gallery/{name}"),
                    name: name.into(),
                    extension: "png".into(),
                    modified_at_ms: None,
                    size_bytes: 1,
                    is_animated: false,
                })
            })
            .collect();
        reducer.push_action(Action::SelectImage {
            path: "C:/gallery/a.png".into(),
            gesture: SelectionGesture::Replace,
        });
        reducer.push_action(Action::SelectImage {
            path: "C:/gallery/b.png".into(),
            gesture: SelectionGesture::Toggle,
        });
        reducer.push_action(Action::RequestTrash);
        reducer.push_action(Action::CloseDialog);
        reducer.reduce_actions();

        assert!(reducer.model.dialog.is_none());
        assert!(reducer.commands.is_empty());
        assert_eq!(reducer.model.selection.ordered_paths.len(), 2);
    }

    #[test]
    fn conversion_uses_visible_results_and_confirms_at_fifty() {
        let visible_items = crate::demo::large_library(50).visible_items;
        let mut reducer = Reducer::new(None);
        reducer.model.visible_items = visible_items;
        reducer.model.selection.ordered_paths = vec!["C:/fixture/image0.png".into()];
        reducer.push_action(Action::RequestConversion(ConversionKind::Webp));
        reducer.reduce_actions();

        assert!(reducer.commands.is_empty());
        assert!(matches!(
            &reducer.model.dialog,
            Some(DialogState::ConversionConfirmation { kind, paths })
                if *kind == ConversionKind::Webp && paths.len() == 50
        ));

        reducer.push_action(Action::ConfirmConversion);
        reducer.reduce_actions();
        assert!(matches!(
            reducer.commands.pop_front(),
            Some(Command::StartFileOperation {
                operation: FileOperation::ConvertWebp { paths },
                ..
            }) if paths.len() == 50
        ));

        let mut reducer = Reducer::new(None);
        reducer.model.visible_items = crate::demo::large_library(49).visible_items;
        reducer.push_action(Action::RequestConversion(ConversionKind::Jpg));
        reducer.reduce_actions();
        assert!(matches!(
            reducer.commands.pop_front(),
            Some(Command::StartFileOperation {
                operation: FileOperation::ConvertJpg { paths },
                ..
            }) if paths.len() == 49
        ));
    }

    #[test]
    fn cleanup_cancel_closes_confirmation_without_starting_work() {
        let mut reducer = Reducer::new(None);
        reducer.model.visible_items = crate::demo::large_library(2).visible_items;
        reducer.push_action(Action::RequestCleanup);
        reducer.push_action(Action::CloseDialog);
        reducer.reduce_actions();

        assert!(reducer.model.dialog.is_none());
        assert!(reducer.commands.is_empty());
    }

    #[test]
    fn drag_drop_builds_a_preview_and_waits_for_confirmation() {
        use piclens_domain::{ImageListItem, ListItem};

        let root = std::env::temp_dir().join(format!(
            "piclens-egui-drag-plan-{}-{}",
            std::process::id(),
            std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .unwrap()
                .as_nanos()
        ));
        std::fs::create_dir_all(&root).unwrap();
        let paths = ["one.jpg", "two.png", "base.webp"].map(|name| root.join(name));
        for path in &paths {
            std::fs::write(path, b"fixture").unwrap();
        }
        let mut reducer = Reducer::new(None);
        reducer.model.visible_items = paths
            .iter()
            .map(|path| {
                ListItem::Image(ImageListItem {
                    path: path.to_string_lossy().into_owned(),
                    name: path.file_name().unwrap().to_string_lossy().into_owned(),
                    extension: path.extension().unwrap().to_string_lossy().into_owned(),
                    modified_at_ms: None,
                    size_bytes: 7,
                    is_animated: false,
                })
            })
            .collect();
        reducer.model.selection.ordered_paths = paths[..2].to_vec();
        reducer.model.selection.range_anchor = Some(paths[0].clone());
        reducer.push_action(Action::StartDrag {
            source: paths[0].clone(),
            pointer: Point { x: 10.0, y: 10.0 },
        });
        reducer.reduce_actions();
        assert!(reducer
            .model
            .drag
            .as_ref()
            .is_some_and(|drag| !drag.dragging));

        reducer.push_action(Action::UpdateDrag {
            pointer: Point { x: 13.0, y: 10.0 },
            target: Some(paths[2].clone()),
        });
        reducer.reduce_actions();
        assert!(reducer
            .model
            .drag
            .as_ref()
            .is_some_and(|drag| !drag.dragging && drag.target.is_none()));

        reducer.push_action(Action::UpdateDrag {
            pointer: Point { x: 30.0, y: 30.0 },
            target: Some(paths[2].clone()),
        });
        reducer.push_action(Action::FinishDrag);
        reducer.reduce_actions();

        let Some(DialogState::DropRenameConfirmation { plan }) = &reducer.model.dialog else {
            panic!("expected drop rename preview")
        };
        assert_eq!(plan.total, 2);
        assert!(plan.items[0].target_path.ends_with("base-01.jpg"));
        assert!(plan.items[1].target_path.ends_with("base-02.png"));
        assert!(paths.iter().all(|path| path.exists()));
        assert!(reducer.commands.is_empty());

        reducer.push_action(Action::ConfirmDropRename);
        reducer.reduce_actions();
        assert!(matches!(
            reducer.commands.pop_front(),
            Some(Command::StartFileOperation {
                operation: FileOperation::DropRename { plan },
                ..
            }) if plan.total == 2
        ));
        assert!(paths.iter().all(|path| path.exists()));
        std::fs::remove_dir_all(root).unwrap();
    }

    #[test]
    fn drag_threshold_preserves_selection_until_dragging_starts() {
        let mut reducer = Reducer::new(None);
        reducer.model.visible_items = crate::demo::large_library(3).visible_items;
        reducer.model.selection.ordered_paths = vec!["C:/fixture/image0.png".into()];
        reducer.model.selection.range_anchor = Some("C:/fixture/image0.png".into());
        reducer.push_action(Action::StartDrag {
            source: "C:/fixture/image1.png".into(),
            pointer: Point { x: 10.0, y: 10.0 },
        });
        reducer.push_action(Action::UpdateDrag {
            pointer: Point { x: 13.0, y: 14.0 },
            target: Some("C:/fixture/image2.png".into()),
        });
        reducer.reduce_actions();

        assert_eq!(
            reducer.model.selection.ordered_paths,
            vec![PathBuf::from("C:/fixture/image0.png")]
        );
        assert!(reducer
            .model
            .drag
            .as_ref()
            .is_some_and(|drag| !drag.dragging && drag.target.is_none()));

        reducer.push_action(Action::UpdateDrag {
            pointer: Point { x: 20.0, y: 10.0 },
            target: Some("C:/fixture/image2.png".into()),
        });
        reducer.reduce_actions();
        assert_eq!(
            reducer.model.selection.ordered_paths,
            vec![PathBuf::from("C:/fixture/image1.png")]
        );
        assert!(reducer.model.drag.as_ref().is_some_and(|drag| {
            drag.dragging
                && drag.target.as_deref() == Some(std::path::Path::new("C:/fixture/image2.png"))
        }));
    }

    #[test]
    fn viewer_keeps_visible_snapshot_while_library_changes() {
        use piclens_domain::{ImageListItem, ListItem};

        let image = |name: &str| {
            ListItem::Image(ImageListItem {
                path: format!("C:/gallery/{name}"),
                name: name.into(),
                extension: "png".into(),
                modified_at_ms: None,
                size_bytes: 1,
                is_animated: false,
            })
        };
        let mut reducer = Reducer::new(None);
        reducer.model.library_query = Some(query("C:/gallery"));
        reducer.model.visible_items = vec![image("a.png"), image("b.png")];

        reducer.push_action(Action::OpenViewer("C:/gallery/a.png".into()));
        reducer.reduce_actions();
        reducer.model.visible_items.clear();
        assert_eq!(reducer.model.page, Page::Viewer);
        assert_eq!(
            reducer.model.viewer.as_ref().unwrap().snapshot.images.len(),
            2
        );

        reducer.push_action(Action::ZoomViewerAt {
            viewport_center: Point { x: 200.0, y: 150.0 },
            pointer: Point { x: 260.0, y: 180.0 },
            delta: 1,
        });
        reducer.push_action(Action::PanViewer(Point { x: 5.0, y: 3.0 }));
        reducer.reduce_actions();
        assert_eq!(
            reducer.model.viewer.as_ref().unwrap().zoom,
            piclens_domain::ZoomState {
                zoom: piclens_domain::ZOOM_STEP,
                offset: Point { x: -7.0, y: -3.0 },
            }
        );

        reducer.push_action(Action::StepViewer(-1));
        reducer.reduce_actions();
        assert_eq!(
            reducer.model.viewer.as_ref().unwrap().zoom,
            reset_zoom_state()
        );
        assert_eq!(
            reducer
                .model
                .viewer
                .as_ref()
                .unwrap()
                .snapshot
                .current()
                .map(|item| item.name.as_str()),
            Some("b.png")
        );

        reducer.push_action(Action::RevealViewer);
        reducer.reduce_actions();
        assert_eq!(
            reducer.commands.pop_front(),
            Some(Command::Reveal {
                path: "C:/gallery/b.png".into()
            })
        );
        assert!(reducer.handle_event(Event::RevealCompleted {
            result: Err("無法顯示".into())
        }));
        assert_eq!(reducer.model.notice.as_deref(), Some("無法顯示"));

        reducer.push_action(Action::CloseViewer);
        reducer.reduce_actions();
        assert_eq!(reducer.model.page, Page::Library);
        assert!(reducer.model.viewer.is_none());
    }
}
