//! Generation-guarded library scan apply.

use piclens_domain::ListItem;

/// Result of one background folder + child-folder scan.
pub struct FolderScanPayload {
    pub items: Vec<ListItem>,
    pub child_folders: Vec<String>,
}

/// Apply a scan when `result_generation` still matches `current_generation`.
///
/// Returns `true` when the destination was updated. A mismatched generation
/// leaves `items` and `child_folders` unchanged.
pub fn apply_folder_scan(
    current_generation: u64,
    result_generation: u64,
    items: &mut Vec<ListItem>,
    child_folders: &mut Vec<String>,
    payload: FolderScanPayload,
) -> bool {
    if current_generation != result_generation {
        return false;
    }
    *items = payload.items;
    *child_folders = payload.child_folders;
    true
}

#[cfg(test)]
mod tests {
    use piclens_domain::{FolderListItem, ListItem};

    use super::{apply_folder_scan, FolderScanPayload};

    fn folder(path: &str) -> ListItem {
        ListItem::Folder(FolderListItem {
            path: path.into(),
            name: path.into(),
            modified_at_ms: None,
        })
    }

    #[test]
    fn apply_scan_ignores_mismatched_generation() {
        let mut items = vec![folder("/old")];
        let mut children = vec!["/old/child".into()];
        let applied = apply_folder_scan(
            2,
            1,
            &mut items,
            &mut children,
            FolderScanPayload {
                items: vec![folder("/new")],
                child_folders: vec!["/new/child".into()],
            },
        );
        assert!(!applied);
        assert_eq!(items[0].path(), "/old");
        assert_eq!(children, vec!["/old/child".to_string()]);
    }

    #[test]
    fn apply_scan_accepts_matching_generation() {
        let mut items = vec![folder("/old")];
        let mut children = Vec::new();
        let applied = apply_folder_scan(
            3,
            3,
            &mut items,
            &mut children,
            FolderScanPayload {
                items: vec![folder("/new")],
                child_folders: vec!["/new/child".into()],
            },
        );
        assert!(applied);
        assert_eq!(items[0].path(), "/new");
        assert_eq!(children, vec!["/new/child".to_string()]);
    }
}
