use std::path::{Path, PathBuf};

pub const DATA_ROOT_ENV: &str = "PICLENS_DATA_ROOT";

fn expand_env_percent(value: &str) -> String {
    let mut result = value.to_string();
    while let Some(start) = result.find('%') {
        let rest = &result[start + 1..];
        if let Some(end) = rest.find('%') {
            let name = &rest[..end];
            if name.is_empty() {
                break;
            }
            if let Ok(replacement) = std::env::var(name) {
                let full = format!("%{name}%");
                result = result.replacen(&full, &replacement, 1);
            } else {
                break;
            }
        } else {
            break;
        }
    }
    result
}

fn local_app_data_root() -> PathBuf {
    directories::BaseDirs::new()
        .map(|dirs| dirs.data_local_dir().to_path_buf())
        .unwrap_or_else(std::env::temp_dir)
}

pub fn app_root() -> PathBuf {
    if let Ok(configured) = std::env::var(DATA_ROOT_ENV) {
        let expanded = expand_env_percent(configured.trim());
        if !expanded.is_empty() {
            return PathBuf::from(expanded);
        }
    }
    local_app_data_root().join("PicLens")
}

pub fn settings_path() -> PathBuf {
    app_root().join("piclens-settings.json")
}

pub fn log_path() -> PathBuf {
    app_root().join("Logs").join("PicLens.log")
}

pub fn thumbnail_cache_root() -> PathBuf {
    app_root().join("Thumbnails")
}

pub fn ensure_parent_dir(path: &Path) -> std::io::Result<()> {
    if let Some(parent) = path.parent() {
        std::fs::create_dir_all(parent)?;
    }
    Ok(())
}
