use serde::{Deserialize, Serialize};

use crate::models::SortState;

pub const DEFAULT_THUMBNAIL_SIZE: i32 = 160;
pub const MIN_THUMBNAIL_SIZE: i32 = 120;
pub const MAX_THUMBNAIL_SIZE: i32 = 240;
pub const THUMBNAIL_SIZE_STEP: i32 = 20;

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AppSettings {
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub last_folder_path: Option<String>,
    #[serde(default)]
    pub sort: SortState,
    #[serde(default)]
    pub include_subfolders: bool,
    #[serde(default = "default_thumbnail_size")]
    pub thumbnail_size: i32,
}

fn default_thumbnail_size() -> i32 {
    DEFAULT_THUMBNAIL_SIZE
}

impl Default for AppSettings {
    fn default() -> Self {
        Self {
            last_folder_path: None,
            sort: SortState::default(),
            include_subfolders: false,
            thumbnail_size: DEFAULT_THUMBNAIL_SIZE,
        }
    }
}

#[derive(Debug, Clone, Default)]
pub struct AppSettingsPatch {
    pub last_folder_path: Option<Option<String>>,
    pub sort: Option<SortState>,
    pub include_subfolders: Option<bool>,
    pub thumbnail_size: Option<i32>,
}

pub fn normalize_thumbnail_size(thumbnail_size: f64) -> i32 {
    if !thumbnail_size.is_finite() {
        return DEFAULT_THUMBNAIL_SIZE;
    }
    let stepped =
        ((thumbnail_size / f64::from(THUMBNAIL_SIZE_STEP)).round() as i32) * THUMBNAIL_SIZE_STEP;
    stepped.clamp(MIN_THUMBNAIL_SIZE, MAX_THUMBNAIL_SIZE)
}

pub fn normalize_settings(settings: &AppSettings) -> AppSettings {
    let mut normalized = settings.clone();
    normalized.thumbnail_size = if settings.thumbnail_size == 0 {
        DEFAULT_THUMBNAIL_SIZE
    } else {
        normalize_thumbnail_size(f64::from(settings.thumbnail_size))
    };
    normalized
}

pub fn merge_settings_patch(current: &AppSettings, patch: &AppSettingsPatch) -> AppSettings {
    let mut merged = normalize_settings(current);
    if let Some(last) = &patch.last_folder_path {
        merged.last_folder_path = last.clone();
    }
    if let Some(sort) = patch.sort {
        merged.sort = sort;
    }
    if let Some(include) = patch.include_subfolders {
        merged.include_subfolders = include;
    }
    if let Some(size) = patch.thumbnail_size {
        merged.thumbnail_size = normalize_thumbnail_size(f64::from(size));
    }
    merged
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::models::{SortDirection, SortKey, SortState};

    #[test]
    fn clamps_thumbnail_size() {
        assert_eq!(normalize_thumbnail_size(10.0), MIN_THUMBNAIL_SIZE);
        assert_eq!(normalize_thumbnail_size(400.0), MAX_THUMBNAIL_SIZE);
        assert_eq!(normalize_thumbnail_size(165.0), 160);
    }

    #[test]
    fn merge_patch_updates_fields() {
        let current = AppSettings::default();
        let patch = AppSettingsPatch {
            last_folder_path: Some(Some("/photos".into())),
            sort: Some(SortState {
                key: SortKey::ModifiedAt,
                direction: SortDirection::Desc,
            }),
            include_subfolders: Some(true),
            thumbnail_size: Some(200),
        };
        let merged = merge_settings_patch(&current, &patch);
        assert_eq!(merged.last_folder_path.as_deref(), Some("/photos"));
        assert_eq!(merged.sort.key, SortKey::ModifiedAt);
        assert!(merged.include_subfolders);
        assert_eq!(merged.thumbnail_size, 200);
    }

    #[test]
    fn zero_thumbnail_size_becomes_default() {
        let mut settings = AppSettings::default();
        settings.thumbnail_size = 0;
        let normalized = normalize_settings(&settings);
        assert_eq!(normalized.thumbnail_size, DEFAULT_THUMBNAIL_SIZE);
    }
}
