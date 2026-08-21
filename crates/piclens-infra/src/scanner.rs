use std::collections::HashSet;
use std::path::{Path, PathBuf};
use std::time::{SystemTime, UNIX_EPOCH};

use piclens_domain::{
    path_key, sort_items, supported_image_extension, FolderListItem, ImageListItem, ListItem,
    ListQuery, SortDirection, SortKey, SortState,
};
use thiserror::Error;
use walkdir::WalkDir;

use crate::animation::is_animated;

#[derive(Debug, Error)]
pub enum ScanError {
    #[error("Directory not found: {0}")]
    DirectoryNotFound(String),
    #[error("Folder scan canceled.")]
    Canceled,
    #[error(transparent)]
    Io(#[from] std::io::Error),
}

fn modified_at_ms(meta: &std::fs::Metadata) -> Option<i64> {
    meta.modified().ok().and_then(|t| {
        t.duration_since(UNIX_EPOCH)
            .ok()
            .map(|d| d.as_millis() as i64)
    })
}

fn is_symlink(path: &Path) -> bool {
    path.symlink_metadata()
        .map(|m| m.file_type().is_symlink())
        .unwrap_or(false)
}

fn create_image_item(path: &Path) -> Option<ImageListItem> {
    let path_str = path.to_string_lossy().replace('\\', "/");
    let extension = supported_image_extension(&path_str)?;
    let meta = std::fs::metadata(path).ok()?;
    if !meta.is_file() {
        return None;
    }
    let name = path
        .file_name()
        .and_then(|n| n.to_str())
        .unwrap_or_default()
        .to_string();
    Some(ImageListItem {
        path: path_str,
        name,
        extension,
        modified_at_ms: modified_at_ms(&meta),
        size_bytes: meta.len(),
        is_animated: is_animated(path),
    })
}

fn create_folder_item(path: &Path) -> Option<FolderListItem> {
    if is_symlink(path) {
        return None;
    }
    let meta = std::fs::metadata(path).ok()?;
    if !meta.is_dir() {
        return None;
    }
    let name = path
        .file_name()
        .and_then(|n| n.to_str())
        .unwrap_or_default()
        .to_string();
    Some(FolderListItem {
        path: path.to_string_lossy().replace('\\', "/"),
        name,
        modified_at_ms: modified_at_ms(&meta),
    })
}

fn direct_entries(folder: &Path) -> std::io::Result<Vec<PathBuf>> {
    let mut entries = Vec::new();
    for entry in std::fs::read_dir(folder)? {
        let entry = entry?;
        let path = entry.path();
        if is_symlink(&path) {
            continue;
        }
        entries.push(path);
    }
    Ok(entries)
}

pub fn scan_folder(query: &ListQuery) -> Result<Vec<ListItem>, ScanError> {
    let root = PathBuf::from(&query.folder_path);
    if !root.is_dir() {
        return Err(ScanError::DirectoryNotFound(query.folder_path.clone()));
    }

    let mut items = Vec::new();
    if !query.include_subfolders {
        for path in direct_entries(&root)? {
            if path.is_dir() {
                if let Some(folder) = create_folder_item(&path) {
                    items.push(ListItem::Folder(folder));
                }
            } else if let Some(image) = create_image_item(&path) {
                items.push(ListItem::Image(image));
            }
        }
    } else {
        let mut visited = HashSet::new();
        for entry in WalkDir::new(&root)
            .follow_links(false)
            .into_iter()
            .filter_map(|e| e.ok())
        {
            let path = entry.path();
            if path.is_dir() {
                if let Ok(canon) = path.canonicalize() {
                    let key = path_key(&canon.to_string_lossy());
                    if !visited.insert(key) {
                        continue;
                    }
                }
                continue;
            }
            if let Some(image) = create_image_item(path) {
                items.push(ListItem::Image(image));
            }
        }
    }

    Ok(sort_items(&items, query.sort, !query.include_subfolders))
}

pub fn scan_child_folders(folder_path: &str) -> Result<Vec<FolderListItem>, ScanError> {
    let root = PathBuf::from(folder_path);
    if !root.is_dir() {
        return Err(ScanError::DirectoryNotFound(folder_path.into()));
    }
    let mut folders = Vec::new();
    for path in direct_entries(&root)? {
        if let Some(folder) = create_folder_item(&path) {
            folders.push(ListItem::Folder(folder));
        }
    }
    let sorted = sort_items(
        &folders,
        SortState {
            key: SortKey::Name,
            direction: SortDirection::Asc,
        },
        false,
    );
    Ok(sorted
        .into_iter()
        .filter_map(|item| match item {
            ListItem::Folder(f) => Some(f),
            ListItem::Image(_) => None,
        })
        .collect())
}

pub fn now_ms() -> i64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|d| d.as_millis() as i64)
        .unwrap_or(0)
}
