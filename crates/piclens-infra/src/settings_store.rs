use std::fs;
use std::path::{Path, PathBuf};

use piclens_domain::{
    merge_settings_patch, normalize_settings, AppSettings, AppSettingsPatch,
};
use thiserror::Error;

use crate::paths::{ensure_parent_dir, settings_path};

fn unique_suffix() -> String {
    format!(
        "{:x}",
        std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .map(|d| d.as_nanos())
            .unwrap_or(0)
    )
}

#[derive(Debug, Error)]
pub enum SettingsError {
    #[error(transparent)]
    Io(#[from] std::io::Error),
    #[error("Unable to parse settings JSON.")]
    Parse,
    #[error(
        "Settings update skipped because the existing settings file could not be read or quarantined."
    )]
    QuarantineFailed,
}

#[derive(Debug, Default)]
pub struct LoadResult {
    pub settings: AppSettings,
    pub read_failed: bool,
    pub quarantined: bool,
}

pub struct JsonSettingsStore {
    path: PathBuf,
}

impl Default for JsonSettingsStore {
    fn default() -> Self {
        Self::new()
    }
}

impl JsonSettingsStore {
    pub fn new() -> Self {
        Self {
            path: settings_path(),
        }
    }

    pub fn with_path(path: impl Into<PathBuf>) -> Self {
        Self { path: path.into() }
    }

    pub fn settings_path(&self) -> &Path {
        &self.path
    }

    pub fn load(&self) -> AppSettings {
        self.load_with_recovery().settings
    }

    pub fn load_with_recovery(&self) -> LoadResult {
        if !self.path.exists() {
            return LoadResult::default();
        }
        let bytes = match fs::read(&self.path) {
            Ok(b) => b,
            Err(_) => {
                let quarantined = self.quarantine_settings_file();
                return LoadResult {
                    settings: AppSettings::default(),
                    read_failed: true,
                    quarantined,
                };
            }
        };
        match serde_json::from_slice::<AppSettings>(&bytes) {
            Ok(settings) => LoadResult {
                settings: normalize_settings(&settings),
                ..Default::default()
            },
            Err(_) => {
                let quarantined = self.quarantine_settings_file();
                LoadResult {
                    settings: AppSettings::default(),
                    read_failed: true,
                    quarantined,
                }
            }
        }
    }

    pub fn save(&self, settings: &AppSettings) -> Result<(), SettingsError> {
        ensure_parent_dir(&self.path)?;
        let normalized = normalize_settings(settings);
        let json = serde_json::to_vec_pretty(&normalized).map_err(|_| SettingsError::Parse)?;
        let tmp = self
            .path
            .with_extension(format!("tmp.{}", unique_suffix()));
        fs::write(&tmp, &json)?;
        fs::rename(&tmp, &self.path)?;
        Ok(())
    }

    pub fn update(&self, patch: &AppSettingsPatch) -> Result<AppSettings, SettingsError> {
        let loaded = self.load_with_recovery();
        if loaded.read_failed && !loaded.quarantined {
            return Err(SettingsError::QuarantineFailed);
        }
        let updated = merge_settings_patch(&loaded.settings, patch);
        self.save(&updated)?;
        Ok(updated)
    }

    fn quarantine_settings_file(&self) -> bool {
        if !self.path.exists() {
            return true;
        }
        let parent = self.path.parent().unwrap_or_else(|| Path::new("."));
        let name = self
            .path
            .file_name()
            .and_then(|n| n.to_str())
            .unwrap_or("piclens-settings.json");
        let target = parent.join(format!("{name}.corrupt.{}", unique_suffix()));
        fs::rename(&self.path, target).is_ok()
    }
}
