//! Pure gallery / overlay / selection helpers used by the GPUI shell.

use std::{collections::BTreeSet, path::Path};

use piclens_domain::{FileOperationBatchResult, FileOperationResult, FileOperationStatus};

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

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum SelectionGesture {
    Replace,
    Toggle,
    Range { additive: bool },
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
    anchor: &mut Option<String>,
    visible_images: &[String],
    path: &str,
    gesture: SelectionGesture,
) {
    match gesture {
        SelectionGesture::Replace => {
            clear_selection(selected, order, anchor);
            insert_selection(selected, order, path);
            *anchor = Some(path.to_string());
        }
        SelectionGesture::Toggle => {
            if selected.remove(path) {
                order.retain(|selected_path| selected_path != path);
            } else {
                insert_selection(selected, order, path);
            }
            *anchor = Some(path.to_string());
        }
        SelectionGesture::Range { additive } => {
            let Some(target_index) = visible_images.iter().position(|item| item == path) else {
                return;
            };
            let anchor_index = anchor
                .as_ref()
                .and_then(|anchor| visible_images.iter().position(|item| item == anchor))
                .unwrap_or(target_index);
            if anchor_index == target_index && anchor.as_deref() != Some(path) {
                *anchor = Some(path.to_string());
            }
            if !additive {
                selected.clear();
                order.clear();
            }
            let (start, end) = if anchor_index <= target_index {
                (anchor_index, target_index)
            } else {
                (target_index, anchor_index)
            };
            for range_path in &visible_images[start..=end] {
                insert_selection(selected, order, range_path);
            }
        }
    }
}

fn insert_selection(selected: &mut BTreeSet<String>, order: &mut Vec<String>, path: &str) {
    if selected.insert(path.to_string()) {
        order.push(path.to_string());
    }
}

pub fn clear_selection(
    selected: &mut BTreeSet<String>,
    order: &mut Vec<String>,
    anchor: &mut Option<String>,
) {
    selected.clear();
    order.clear();
    *anchor = None;
}

pub fn reconcile_selection(
    selected: &mut BTreeSet<String>,
    order: &mut Vec<String>,
    anchor: &mut Option<String>,
    visible_images: &[String],
) {
    selected.retain(|path| visible_images.contains(path));
    order.retain(|path| selected.contains(path));
    if anchor
        .as_ref()
        .is_some_and(|path| !visible_images.contains(path))
    {
        *anchor = None;
    }
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

pub fn batch_result_status_label(status: FileOperationStatus) -> &'static str {
    match status {
        FileOperationStatus::Converted => "已轉換",
        FileOperationStatus::Trashed => "已移至回收筒",
        FileOperationStatus::Renamed => "已重新命名",
        FileOperationStatus::Skipped => "已略過",
        FileOperationStatus::Failed => "失敗",
    }
}

pub fn batch_result_file_name(result: &FileOperationResult) -> String {
    Path::new(&result.path)
        .file_name()
        .and_then(|name| name.to_str())
        .unwrap_or(&result.path)
        .to_string()
}

pub fn batch_result_detail(result: &FileOperationResult) -> String {
    let target_name = result
        .target_path
        .as_deref()
        .and_then(|path| Path::new(path).file_name().and_then(|name| name.to_str()));
    match result.reason.as_deref() {
        Some("target_exists") => "目標檔案已存在，未覆寫。".into(),
        Some("same_path" | "same_name") => "來源與目標相同，未變更檔案。".into(),
        Some("skip_format") => "已是 JPG、JPEG 或 WebP，未重複轉換。".into(),
        Some("animated") => "動畫圖片目前不支援轉換。".into(),
        Some("unsupported") => "不是支援的圖片格式。".into(),
        Some("linked_path") => "路徑包含符號連結或 junction，為保護原檔而略過。".into(),
        Some("decode_failed") => format!(
            "無法讀取圖片。{}",
            result.message.as_deref().unwrap_or("請確認檔案是否完整。")
        ),
        Some("encode_failed") => format!(
            "無法寫入轉換檔案。{}",
            result
                .message
                .as_deref()
                .unwrap_or("請確認資料夾權限與可用空間。")
        ),
        Some("trash_failed") => format!(
            "無法移至回收筒。{}",
            result.message.as_deref().unwrap_or("請確認檔案權限。")
        ),
        Some("rename_failed") => format!(
            "無法重新命名。{}",
            result.message.as_deref().unwrap_or("請確認檔案權限。")
        ),
        _ => match result.status {
            FileOperationStatus::Converted => target_name
                .map(|name| format!("已建立 {name}，原始檔案仍保留。"))
                .unwrap_or_else(|| "轉換完成，原始檔案仍保留。".into()),
            FileOperationStatus::Trashed => "可從作業系統回收筒還原。".into(),
            FileOperationStatus::Renamed => target_name
                .map(|name| format!("新檔名：{name}"))
                .unwrap_or_else(|| "重新命名完成。".into()),
            FileOperationStatus::Skipped => result
                .message
                .clone()
                .unwrap_or_else(|| "未變更檔案。".into()),
            FileOperationStatus::Failed => result
                .message
                .clone()
                .unwrap_or_else(|| "操作失敗，請確認檔案與資料夾權限。".into()),
        },
    }
}

pub fn batch_result_reveal_path(result: &FileOperationResult) -> Option<&str> {
    match result.status {
        FileOperationStatus::Trashed => None,
        FileOperationStatus::Converted | FileOperationStatus::Renamed => {
            result.target_path.as_deref().or(Some(result.path.as_str()))
        }
        FileOperationStatus::Skipped | FileOperationStatus::Failed => Some(result.path.as_str()),
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
    fn selection_replace_toggle_range_clear_and_reconcile() {
        let mut selected = BTreeSet::new();
        let mut order = Vec::new();
        let mut anchor = None;
        let visible = vec![
            "/a".to_string(),
            "/b".to_string(),
            "/c".to_string(),
            "/d".to_string(),
        ];

        apply_selection(
            &mut selected,
            &mut order,
            &mut anchor,
            &visible,
            "/a",
            SelectionGesture::Replace,
        );
        apply_selection(
            &mut selected,
            &mut order,
            &mut anchor,
            &visible,
            "/b",
            SelectionGesture::Toggle,
        );
        assert_eq!(order, vec!["/a".to_string(), "/b".to_string()]);

        apply_selection(
            &mut selected,
            &mut order,
            &mut anchor,
            &visible,
            "/a",
            SelectionGesture::Toggle,
        );
        assert_eq!(order, vec!["/b".to_string()]);
        assert_eq!(anchor.as_deref(), Some("/a"));

        apply_selection(
            &mut selected,
            &mut order,
            &mut anchor,
            &visible,
            "/d",
            SelectionGesture::Range { additive: false },
        );
        assert_eq!(order, vec!["/a", "/b", "/c", "/d"]);
        assert_eq!(anchor.as_deref(), Some("/a"));

        reconcile_selection(
            &mut selected,
            &mut order,
            &mut anchor,
            &["/c".to_string(), "/d".to_string()],
        );
        assert_eq!(order, vec!["/c", "/d"]);
        assert!(anchor.is_none());

        clear_selection(&mut selected, &mut order, &mut anchor);
        assert!(selected.is_empty());
        assert!(order.is_empty());
        assert!(anchor.is_none());
    }

    #[test]
    fn range_uses_only_visible_images_and_keeps_the_anchor() {
        let mut selected = BTreeSet::new();
        let mut order = Vec::new();
        let mut anchor = None;
        let visible_images = vec!["/a".to_string(), "/b".to_string(), "/c".to_string()];

        apply_selection(
            &mut selected,
            &mut order,
            &mut anchor,
            &visible_images,
            "/b",
            SelectionGesture::Replace,
        );
        apply_selection(
            &mut selected,
            &mut order,
            &mut anchor,
            &visible_images,
            "/c",
            SelectionGesture::Range { additive: false },
        );
        apply_selection(
            &mut selected,
            &mut order,
            &mut anchor,
            &visible_images,
            "/a",
            SelectionGesture::Range { additive: false },
        );

        assert_eq!(order, vec!["/a", "/b"]);
        assert_eq!(anchor.as_deref(), Some("/b"));
    }

    #[test]
    fn additive_range_keeps_existing_selection_order() {
        let mut selected = BTreeSet::new();
        let mut order = Vec::new();
        let mut anchor = None;
        let visible_images = vec!["/a".to_string(), "/b".to_string(), "/c".to_string()];

        apply_selection(
            &mut selected,
            &mut order,
            &mut anchor,
            &visible_images,
            "/c",
            SelectionGesture::Toggle,
        );
        apply_selection(
            &mut selected,
            &mut order,
            &mut anchor,
            &visible_images,
            "/a",
            SelectionGesture::Range { additive: true },
        );

        assert_eq!(order, vec!["/c", "/a", "/b"]);
        assert_eq!(anchor.as_deref(), Some("/c"));
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
    fn batch_result_copy_and_reveal_target_match_the_outcome() {
        let converted = FileOperationResult {
            path: "C:/images/長檔名.png".into(),
            status: FileOperationStatus::Converted,
            target_path: Some("C:/images/長檔名.jpg".into()),
            reason: None,
            message: None,
        };
        assert_eq!(batch_result_file_name(&converted), "長檔名.png");
        assert_eq!(batch_result_status_label(converted.status), "已轉換");
        assert_eq!(
            batch_result_detail(&converted),
            "已建立 長檔名.jpg，原始檔案仍保留。"
        );
        assert_eq!(
            batch_result_reveal_path(&converted),
            Some("C:/images/長檔名.jpg")
        );

        let failed = FileOperationResult {
            path: "C:/images/bad.webp".into(),
            status: FileOperationStatus::Failed,
            target_path: None,
            reason: Some("decode_failed".into()),
            message: Some("檔案已損毀。".into()),
        };
        assert_eq!(batch_result_detail(&failed), "無法讀取圖片。檔案已損毀。");
        assert_eq!(
            batch_result_reveal_path(&failed),
            Some("C:/images/bad.webp")
        );

        let trashed = FileOperationResult {
            status: FileOperationStatus::Trashed,
            ..failed
        };
        assert_eq!(batch_result_reveal_path(&trashed), None);
    }

    #[test]
    fn reduce_motion_disables_overlay_motion() {
        assert!(!allow_motion(true));
        assert!(allow_motion(false));
    }
}
