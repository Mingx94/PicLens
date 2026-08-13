//! Generation-guarded library scan apply.

use piclens_domain::ListItem;

/// Result of one background folder scan.
pub struct FolderScanPayload {
    pub items: Vec<ListItem>,
}

/// Apply a scan when `result_generation` still matches `current_generation`.
///
/// Returns `true` when the destination was updated. A mismatched generation
/// leaves `items` unchanged.
pub fn apply_folder_scan(
    current_generation: u64,
    result_generation: u64,
    items: &mut Vec<ListItem>,
    payload: FolderScanPayload,
) -> bool {
    if current_generation != result_generation {
        return false;
    }
    *items = payload.items;
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
        let applied = apply_folder_scan(
            2,
            1,
            &mut items,
            FolderScanPayload {
                items: vec![folder("/new")],
            },
        );
        assert!(!applied);
        assert_eq!(items[0].path(), "/old");
    }

    #[test]
    fn apply_scan_accepts_matching_generation() {
        let mut items = vec![folder("/old")];
        let applied = apply_folder_scan(
            3,
            3,
            &mut items,
            FolderScanPayload {
                items: vec![folder("/new")],
            },
        );
        assert!(applied);
        assert_eq!(items[0].path(), "/new");
    }
}
