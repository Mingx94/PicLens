//! Framework-light interface state for the egui frontend.

use std::collections::{HashMap, HashSet};
use std::path::PathBuf;

use piclens_domain::{
    DropTargetBatchRenamePlan, FileOperationBatchResult, FolderHistory, ImageSequenceSnapshot,
    ListItem, ListQuery, Point, SortState, ZoomState, DEFAULT_THUMBNAIL_SIZE,
};

#[derive(Debug, Clone, PartialEq)]
pub enum Action {
    Quit,
    ChooseFolder,
    PickedFolder(PathBuf),
    RestoreFolder(PathBuf),
    NavigateFolder(PathBuf),
    NavigateHistory {
        back: bool,
    },
    ToggleTreeFolder(String),
    ToggleSidebar,
    ToggleCompactSidebar,
    RetryBackendProbe,
    DismissStatus,
    ShowNotice(String),
    StartBackendProbe,
    LoadLibrary(ListQuery),
    ReloadLibrary,
    SetSearch(String),
    SetSort(SortState),
    CycleSort,
    ToggleIncludeSubfolders,
    SetThumbnailSize(i32),
    SelectAllVisible,
    MoveGallerySelection(i32),
    SelectGalleryBoundary {
        end: bool,
    },
    ClearGalleryScrollTarget,
    OpenFocusedItem,
    OpenViewer(PathBuf),
    CloseViewer,
    StepViewer(i32),
    AdjustViewerZoom(i32),
    ZoomViewerAt {
        pointer: Point,
        viewport_center: Point,
        delta: i32,
    },
    PanViewer(Point),
    ResetViewerZoom,
    RevealViewer,
    RevealSelection,
    RevealPath(PathBuf),
    OpenRename,
    SetRenameBasename(String),
    ConfirmRename,
    RequestTrash,
    ConfirmTrash,
    RequestConversion(ConversionKind),
    ConfirmConversion,
    RequestCleanup,
    ConfirmCleanup,
    StartDrag {
        source: PathBuf,
        pointer: Point,
    },
    UpdateDrag {
        pointer: Point,
        target: Option<PathBuf>,
    },
    FinishDrag,
    RequestDropRename,
    CancelDrag,
    ConfirmDropRename,
    CancelFileOperation,
    CloseDialog,
    SelectImage {
        path: PathBuf,
        gesture: SelectionGesture,
    },
    ClearSelection,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum SelectionGesture {
    Replace,
    Toggle,
    Range { additive: bool },
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ConversionKind {
    Jpg,
    Webp,
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
    pub focused_path: Option<PathBuf>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct DragSession {
    pub sources: Vec<PathBuf>,
    pub origin: Point,
    pub pointer: Point,
    pub target: Option<PathBuf>,
    pub dragging: bool,
    pub replaces_selection: bool,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum DialogState {
    Rename {
        source: PathBuf,
        basename: String,
    },
    TrashConfirmation {
        paths: Vec<PathBuf>,
    },
    ConversionConfirmation {
        kind: ConversionKind,
        paths: Vec<PathBuf>,
    },
    CleanupConfirmation {
        paths: Vec<PathBuf>,
    },
    DropRenameConfirmation {
        plan: DropTargetBatchRenamePlan,
    },
    Progress {
        title: String,
        message: String,
    },
    BatchResult(FileOperationBatchResult),
}

#[derive(Debug, Clone, PartialEq)]
pub struct ViewerState {
    pub snapshot: ImageSequenceSnapshot,
    pub preview: Loadable<PathBuf>,
    pub zoom: ZoomState,
}

#[derive(Debug, Clone, PartialEq)]
pub struct AppModel {
    pub initial_folder: Option<PathBuf>,
    pub current_folder: Option<PathBuf>,
    pub library_query: Option<ListQuery>,
    pub history: FolderHistory,
    pub tree_root: Option<String>,
    pub tree_roots: Vec<String>,
    pub tree_children: HashMap<String, Vec<String>>,
    pub tree_expanded: HashSet<String>,
    pub sidebar_collapsed: bool,
    pub compact_sidebar_open: bool,
    pub page: Page,
    pub library: Loadable<Vec<ListItem>>,
    pub visible_items: Vec<ListItem>,
    pub search: String,
    pub thumbnail_size: i32,
    pub selection: SelectionState,
    pub gallery_scroll_target: Option<usize>,
    pub gallery_scroll_delta: Option<f32>,
    pub drag: Option<DragSession>,
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
            history: FolderHistory::default(),
            tree_root: None,
            tree_roots: Vec::new(),
            tree_children: HashMap::new(),
            tree_expanded: HashSet::new(),
            sidebar_collapsed: false,
            compact_sidebar_open: false,
            page: Page::Library,
            library: Loadable::Idle,
            visible_items: Vec::new(),
            search: String::new(),
            thumbnail_size: DEFAULT_THUMBNAIL_SIZE,
            selection: SelectionState::default(),
            gallery_scroll_target: None,
            gallery_scroll_delta: None,
            drag: None,
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
            history: FolderHistory::default(),
            tree_root: None,
            tree_roots: Vec::new(),
            tree_children: HashMap::new(),
            tree_expanded: HashSet::new(),
            sidebar_collapsed: false,
            compact_sidebar_open: false,
            page: Page::Library,
            library: Loadable::Idle,
            visible_items: Vec::new(),
            search: String::new(),
            thumbnail_size: DEFAULT_THUMBNAIL_SIZE,
            selection: SelectionState::default(),
            gallery_scroll_target: None,
            gallery_scroll_delta: None,
            drag: None,
            dialog: None,
            viewer: None,
            backend: Loadable::Failed(message.into()),
            notice: None,
        }
    }
}
