//! Keyboard actions, keybindings, and the native menu bar.

use gpui::{App, KeyBinding, Menu, MenuItem, actions};

actions!(
    piclens,
    [
        Quit,
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
        KeyBinding::new("ctrl-q", Quit, None),
        KeyBinding::new("alt-f4", Quit, None),
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

    set_app_menus(cx);
}

/// Native menu bar. Labels stay Traditional Chinese to match the product locale.
pub fn set_app_menus(cx: &mut App) {
    cx.set_menus(vec![
        Menu {
            name: "檔案".into(),
            disabled: false,
            items: vec![
                MenuItem::action("開啟資料夾", OpenFolder),
                MenuItem::action("重新整理", Refresh),
                MenuItem::separator(),
                MenuItem::action("在檔案管理器中顯示", RevealInFileManager),
                MenuItem::separator(),
                MenuItem::action("結束", Quit),
            ],
        },
        Menu {
            name: "編輯".into(),
            disabled: false,
            items: vec![
                MenuItem::action("全選可見圖片", SelectAll),
                MenuItem::action("清除選取", ClearSelection),
                MenuItem::separator(),
                MenuItem::action("重新命名", RenameSelection),
                MenuItem::action("依目標重新命名", DropRenamePlan),
                MenuItem::separator(),
                MenuItem::action("轉 JPG", ConvertJpg),
                MenuItem::action("轉 WebP", ConvertWebp),
                MenuItem::action("清除同名格式", CleanupSameBasename),
                MenuItem::separator(),
                MenuItem::action("移至回收筒", TrashSelection),
            ],
        },
        Menu {
            name: "檢視".into(),
            disabled: false,
            items: vec![
                MenuItem::action("側欄", ToggleSidebar),
                MenuItem::action("格狀 / 列表", ToggleGalleryMode),
                MenuItem::action("切換排序", CycleSort),
                MenuItem::action("含子資料夾", ToggleIncludeSubfolders),
                MenuItem::separator(),
                MenuItem::action("搜尋", FocusSearch),
                MenuItem::separator(),
                MenuItem::action("上一頁", HistoryBack),
                MenuItem::action("下一頁", HistoryForward),
            ],
        },
    ]);
}
