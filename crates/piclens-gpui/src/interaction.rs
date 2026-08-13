//! Pure gallery / overlay / selection helpers used by the GPUI shell.

use std::collections::BTreeSet;

use piclens_domain::FileOperationBatchResult;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum GalleryJump {
    Home,
    End,
    PageUp,
    PageDown,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum PageKeyOutcome {
    ViewerStep(i32),
    Gallery(Option<usize>),
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum EscapeTarget {
    Drag,
    DropRename,
    Rename,
    Viewer,
    Selection,
    Search,
    None,
}

pub fn gallery_jump_index(
    visible_len: usize,
    current: Option<usize>,
    columns: usize,
    page_rows: usize,
    jump: GalleryJump,
) -> Option<usize> {
    if visible_len == 0 {
        return None;
    }
    let last = visible_len - 1;
    let page = columns.max(1).saturating_mul(page_rows.max(1));
    Some(match jump {
        GalleryJump::Home => 0,
        GalleryJump::End => last,
        GalleryJump::PageUp => current.unwrap_or(0).saturating_sub(page),
        GalleryJump::PageDown => current.unwrap_or(0).saturating_add(page).min(last),
    })
}

pub fn page_key_outcome(
    viewer_open: bool,
    visible_len: usize,
    current: Option<usize>,
    columns: usize,
    page_rows: usize,
    page_down: bool,
) -> PageKeyOutcome {
    if viewer_open {
        return PageKeyOutcome::ViewerStep(if page_down { 1 } else { -1 });
    }
    let jump = if page_down {
        GalleryJump::PageDown
    } else {
        GalleryJump::PageUp
    };
    PageKeyOutcome::Gallery(gallery_jump_index(
        visible_len,
        current,
        columns,
        page_rows,
        jump,
    ))
}

pub fn next_escape_target(
    drag_active: bool,
    drop_rename: bool,
    rename: bool,
    viewer: bool,
    has_selection: bool,
    has_search: bool,
) -> EscapeTarget {
    if drag_active {
        EscapeTarget::Drag
    } else if drop_rename {
        EscapeTarget::DropRename
    } else if rename {
        EscapeTarget::Rename
    } else if viewer {
        EscapeTarget::Viewer
    } else if has_selection {
        EscapeTarget::Selection
    } else if has_search {
        EscapeTarget::Search
    } else {
        EscapeTarget::None
    }
}

pub fn apply_selection(
    selected: &mut BTreeSet<String>,
    order: &mut Vec<String>,
    path: &str,
    additive: bool,
) {
    if !additive {
        selected.clear();
        order.clear();
    }
    if selected.insert(path.to_string()) {
        order.push(path.to_string());
    }
}

pub fn clear_selection(selected: &mut BTreeSet<String>, order: &mut Vec<String>) {
    selected.clear();
    order.clear();
}

pub fn batch_result_message(label: &str, batch: &FileOperationBatchResult) -> Option<String> {
    if batch.total() == 0 {
        return None;
    }
    Some(format!(
        "{label}：成功 {}，略過 {}，失敗 {}（共 {}）",
        batch.succeeded(),
        batch.skipped(),
        batch.failed(),
        batch.total()
    ))
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum BatchNoticeKind {
    Success,
    Warning,
    Error,
}

pub fn batch_notice_kind(batch: &FileOperationBatchResult) -> Option<BatchNoticeKind> {
    if batch.total() == 0 {
        return None;
    }
    if batch.failed() > 0 {
        Some(BatchNoticeKind::Error)
    } else if batch.succeeded() == 0 {
        Some(BatchNoticeKind::Warning)
    } else {
        Some(BatchNoticeKind::Success)
    }
}

/// Viewer / overlay code must not add motion when this is false.
/// No viewer or overlay animation is shipped, so the UI does not call this yet.
#[cfg_attr(not(test), allow(dead_code))]
pub fn allow_motion(reduce_motion: bool) -> bool {
    !reduce_motion
}

#[cfg(test)]
mod tests {
    use std::collections::BTreeSet;

    use piclens_domain::{FileOperationBatchResult, FileOperationResult, FileOperationStatus};

    use super::*;

    fn item(path: &str, status: FileOperationStatus) -> FileOperationResult {
        FileOperationResult {
            path: path.into(),
            status,
            target_path: None,
            reason: None,
            message: None,
        }
    }

    #[test]
    fn gallery_home_end_page_use_visible_list_and_columns() {
        assert_eq!(
            gallery_jump_index(10, Some(7), 3, 2, GalleryJump::Home),
            Some(0)
        );
        assert_eq!(
            gallery_jump_index(10, Some(7), 3, 2, GalleryJump::End),
            Some(9)
        );
        assert_eq!(
            gallery_jump_index(10, Some(7), 3, 2, GalleryJump::PageUp),
            Some(1)
        );
        assert_eq!(
            gallery_jump_index(10, Some(7), 3, 2, GalleryJump::PageDown),
            Some(9)
        );
    }

    #[test]
    fn page_keys_step_viewer_not_gallery_when_viewer_open() {
        assert_eq!(
            page_key_outcome(true, 12, Some(4), 3, 2, true),
            PageKeyOutcome::ViewerStep(1)
        );
        assert_eq!(
            page_key_outcome(true, 12, Some(4), 3, 2, false),
            PageKeyOutcome::ViewerStep(-1)
        );
        assert_eq!(
            page_key_outcome(false, 12, Some(4), 3, 2, true),
            PageKeyOutcome::Gallery(Some(10))
        );
    }

    #[test]
    fn escape_clears_overlay_then_selection_then_search() {
        assert_eq!(
            next_escape_target(false, true, true, true, true, true),
            EscapeTarget::DropRename
        );
        assert_eq!(
            next_escape_target(false, false, true, true, true, true),
            EscapeTarget::Rename
        );
        assert_eq!(
            next_escape_target(false, false, false, true, true, true),
            EscapeTarget::Viewer
        );
        assert_eq!(
            next_escape_target(false, false, false, false, true, true),
            EscapeTarget::Selection
        );
        assert_eq!(
            next_escape_target(false, false, false, false, false, true),
            EscapeTarget::Search
        );
    }

    #[test]
    fn selection_add_replace_and_clear() {
        let mut selected = BTreeSet::new();
        let mut order = Vec::new();
        apply_selection(&mut selected, &mut order, "/a", false);
        apply_selection(&mut selected, &mut order, "/b", true);
        assert_eq!(order, vec!["/a".to_string(), "/b".to_string()]);
        apply_selection(&mut selected, &mut order, "/c", false);
        assert_eq!(order, vec!["/c".to_string()]);
        assert_eq!(selected.len(), 1);
        clear_selection(&mut selected, &mut order);
        assert!(selected.is_empty());
        assert!(order.is_empty());
    }

    #[test]
    fn batch_message_uses_real_counters_and_skips_empty() {
        let empty = FileOperationBatchResult::default();
        assert!(batch_result_message("轉 JPG", &empty).is_none());
        assert!(batch_notice_kind(&empty).is_none());

        let batch = FileOperationBatchResult {
            items: vec![
                item("a", FileOperationStatus::Converted),
                item("b", FileOperationStatus::Skipped),
                item("c", FileOperationStatus::Failed),
            ],
        };
        let message = batch_result_message("轉 JPG", &batch).expect("non-empty batch");
        assert_eq!(message, "轉 JPG：成功 1，略過 1，失敗 1（共 3）");
        assert_eq!(batch_notice_kind(&batch), Some(BatchNoticeKind::Error));
    }

    #[test]
    fn reduce_motion_disables_overlay_motion() {
        assert!(!allow_motion(true));
        assert!(allow_motion(false));
    }
}
