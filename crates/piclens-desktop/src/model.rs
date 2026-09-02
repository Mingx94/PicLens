//! Framework-light interface state for the egui frontend.

use std::path::PathBuf;

use piclens_domain::{
    FileOperationBatchResult, ImageSequenceSnapshot, ListItem, ListQuery, SortState,
    DEFAULT_THUMBNAIL_SIZE,
};

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum Action {
    ChooseFolder,
    RetryBackendProbe,
    DismissStatus,
    ShowNotice(String),
    StartBackendProbe,
    LoadLibrary(ListQuery),
    ReloadLibrary,
    SetSearch(String),
    SetSort(SortState),
    ToggleIncludeSubfolders,
    SetThumbnailSize(i32),
}

#[derive(Debug, Clone, Default, PartialEq, Eq)]
pub enum Loadable<T> {
    #[default]
    Idle,
    Loading,
    Ready(T),
    Failed(String),
}

#[derive(Debug, Clone, Copy, Default, PartialEq, Eq)]
pub enum Page {
    #[default]
    Library,
    Viewer,
}

#[derive(Debug, Clone, Default, PartialEq, Eq)]
pub struct SelectionState {
    pub ordered_paths: Vec<PathBuf>,
    pub range_anchor: Option<PathBuf>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum DialogState {
    Confirmation { title: String, message: String },
    Rename { source: PathBuf, basename: String },
    BatchResult(FileOperationBatchResult),
}

#[derive(Debug, Clone, PartialEq)]
pub struct ViewerState {
    pub snapshot: ImageSequenceSnapshot,
    pub preview: Loadable<PathBuf>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct AppModel {
    pub initial_folder: Option<PathBuf>,
    pub current_folder: Option<PathBuf>,
    pub library_query: Option<ListQuery>,
    pub page: Page,
    pub library: Loadable<Vec<ListItem>>,
    pub visible_items: Vec<ListItem>,
    pub search: String,
    pub thumbnail_size: i32,
    pub selection: SelectionState,
    pub dialog: Option<DialogState>,
    pub viewer: Option<ViewerState>,
    pub backend: Loadable<()>,
    pub notice: Option<String>,
}

impl AppModel {
    pub fn new(initial_folder: Option<PathBuf>) -> Self {
        Self {
            initial_folder,
            current_folder: None,
            library_query: None,
            page: Page::Library,
            library: Loadable::Idle,
            visible_items: Vec::new(),
            search: String::new(),
            thumbnail_size: DEFAULT_THUMBNAIL_SIZE,
            selection: SelectionState::default(),
            dialog: None,
            viewer: None,
            backend: Loadable::Loading,
            notice: None,
        }
    }

    pub fn demo_error(message: impl Into<String>) -> Self {
        Self {
            initial_folder: None,
            current_folder: None,
            library_query: None,
            page: Page::Library,
            library: Loadable::Idle,
            visible_items: Vec::new(),
            search: String::new(),
            thumbnail_size: DEFAULT_THUMBNAIL_SIZE,
            selection: SelectionState::default(),
            dialog: None,
            viewer: None,
            backend: Loadable::Failed(message.into()),
            notice: None,
        }
    }
}
