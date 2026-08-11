use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[repr(i32)]
#[serde(from = "i32", into = "i32")]
pub enum SortKey {
    Name = 0,
    ModifiedAt = 1,
}

impl From<i32> for SortKey {
    fn from(value: i32) -> Self {
        if value == 1 {
            Self::ModifiedAt
        } else {
            Self::Name
        }
    }
}

impl From<SortKey> for i32 {
    fn from(value: SortKey) -> Self {
        value as i32
    }
}

impl Default for SortKey {
    fn default() -> Self {
        Self::Name
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[repr(i32)]
#[serde(from = "i32", into = "i32")]
pub enum SortDirection {
    Asc = 0,
    Desc = 1,
}

impl From<i32> for SortDirection {
    fn from(value: i32) -> Self {
        if value == 1 {
            Self::Desc
        } else {
            Self::Asc
        }
    }
}

impl From<SortDirection> for i32 {
    fn from(value: SortDirection) -> Self {
        value as i32
    }
}

impl Default for SortDirection {
    fn default() -> Self {
        Self::Asc
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, Default)]
pub struct SortState {
    #[serde(default)]
    pub key: SortKey,
    #[serde(default)]
    pub direction: SortDirection,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct FolderListItem {
    pub path: String,
    pub name: String,
    pub modified_at_ms: Option<i64>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ImageListItem {
    pub path: String,
    pub name: String,
    pub extension: String,
    pub modified_at_ms: Option<i64>,
    pub size_bytes: u64,
    pub is_animated: bool,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum ListItem {
    Folder(FolderListItem),
    Image(ImageListItem),
}

impl ListItem {
    pub fn name(&self) -> &str {
        match self {
            Self::Folder(item) => &item.name,
            Self::Image(item) => &item.name,
        }
    }

    pub fn path(&self) -> &str {
        match self {
            Self::Folder(item) => &item.path,
            Self::Image(item) => &item.path,
        }
    }

    pub fn modified_at_ms(&self) -> Option<i64> {
        match self {
            Self::Folder(item) => item.modified_at_ms,
            Self::Image(item) => item.modified_at_ms,
        }
    }

    pub fn is_folder(&self) -> bool {
        matches!(self, Self::Folder(_))
    }

    pub fn as_image(&self) -> Option<&ImageListItem> {
        match self {
            Self::Image(item) => Some(item),
            Self::Folder(_) => None,
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ListQuery {
    pub folder_path: String,
    pub include_subfolders: bool,
    pub sort: SortState,
}

#[derive(Debug, Clone, PartialEq)]
pub struct ImageSequenceSnapshot {
    pub source_folder_path: String,
    pub include_subfolders: bool,
    pub sort: SortState,
    pub images: Vec<ImageListItem>,
    pub current_index: i32,
}

#[derive(Debug, Clone, Copy, PartialEq)]
pub struct Point {
    pub x: f64,
    pub y: f64,
}

impl Default for Point {
    fn default() -> Self {
        Self { x: 0.0, y: 0.0 }
    }
}

#[derive(Debug, Clone, Copy, PartialEq)]
pub struct ZoomState {
    pub zoom: f64,
    pub offset: Point,
}

impl Default for ZoomState {
    fn default() -> Self {
        Self {
            zoom: 1.0,
            offset: Point::default(),
        }
    }
}
