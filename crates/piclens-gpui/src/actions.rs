//! Keyboard actions and keybindings for the PicLens window.

use gpui::{App, KeyBinding, actions};

actions!(
    piclens,
    [
        OpenFolder,
        Refresh,
        HistoryBack,
        HistoryForward,
        ToggleSidebar,
        ToggleGalleryMode,
        CycleSort,
        ToggleIncludeSubfolders,
        ClearSelection,
        SelectAll,
        OpenViewer,
        CloseOverlay,
        ViewerPrev,
        ViewerNext,
        ZoomIn,
        ZoomOut,
        ZoomReset,
        TrashSelection,
        RenameSelection,
        DropRenamePlan,
        ConvertJpg,
        ConvertWebp,
        CleanupSameBasename,
        RevealInFileManager,
        FocusSearch,
        MoveSelectionUp,
        MoveSelectionDown,
        MoveSelectionLeft,
        MoveSelectionRight,
    ]
);

/// Key context for the main PicLens shell (library + overlays handled in one tree).
pub const CONTEXT: &str = "PicLens";

pub fn init(cx: &mut App) {
    cx.bind_keys([
        // File / folder
        KeyBinding::new("ctrl-o", OpenFolder, Some(CONTEXT)),
        KeyBinding::new("f5", Refresh, Some(CONTEXT)),
        KeyBinding::new("ctrl-r", Refresh, Some(CONTEXT)),
        // History
        KeyBinding::new("alt-left", HistoryBack, Some(CONTEXT)),
        KeyBinding::new("alt-right", HistoryForward, Some(CONTEXT)),
        KeyBinding::new("backspace", HistoryBack, Some(CONTEXT)),
        // Shell
        KeyBinding::new("ctrl-b", ToggleSidebar, Some(CONTEXT)),
        KeyBinding::new("ctrl-1", ToggleGalleryMode, Some(CONTEXT)),
        KeyBinding::new("ctrl-2", ToggleGalleryMode, Some(CONTEXT)),
        KeyBinding::new("ctrl-s", CycleSort, Some(CONTEXT)),
        KeyBinding::new("ctrl-shift-s", ToggleIncludeSubfolders, Some(CONTEXT)),
        KeyBinding::new("ctrl-f", FocusSearch, Some(CONTEXT)),
        KeyBinding::new("/", FocusSearch, Some(CONTEXT)),
        // Selection
        KeyBinding::new("escape", CloseOverlay, Some(CONTEXT)),
        KeyBinding::new("ctrl-a", SelectAll, Some(CONTEXT)),
        KeyBinding::new("up", MoveSelectionUp, Some(CONTEXT)),
        KeyBinding::new("down", MoveSelectionDown, Some(CONTEXT)),
        KeyBinding::new("left", MoveSelectionLeft, Some(CONTEXT)),
        KeyBinding::new("right", MoveSelectionRight, Some(CONTEXT)),
        KeyBinding::new("enter", OpenViewer, Some(CONTEXT)),
        KeyBinding::new("space", OpenViewer, Some(CONTEXT)),
        // File ops
        KeyBinding::new("delete", TrashSelection, Some(CONTEXT)),
        KeyBinding::new("f2", RenameSelection, Some(CONTEXT)),
        KeyBinding::new("ctrl-shift-r", DropRenamePlan, Some(CONTEXT)),
        KeyBinding::new("ctrl-j", ConvertJpg, Some(CONTEXT)),
        KeyBinding::new("ctrl-w", ConvertWebp, Some(CONTEXT)),
        KeyBinding::new("ctrl-shift-c", CleanupSameBasename, Some(CONTEXT)),
        KeyBinding::new("ctrl-shift-e", RevealInFileManager, Some(CONTEXT)),
        // Viewer (same context; handlers no-op when viewer closed)
        KeyBinding::new("pageup", ViewerPrev, Some(CONTEXT)),
        KeyBinding::new("pagedown", ViewerNext, Some(CONTEXT)),
        KeyBinding::new("=", ZoomIn, Some(CONTEXT)),
        KeyBinding::new("plus", ZoomIn, Some(CONTEXT)),
        KeyBinding::new("-", ZoomOut, Some(CONTEXT)),
        KeyBinding::new("0", ZoomReset, Some(CONTEXT)),
    ]);
}
