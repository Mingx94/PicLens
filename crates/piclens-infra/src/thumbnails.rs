use std::fs;
use std::path::PathBuf;
use std::sync::Mutex;

use image::imageops::FilterType;
use image::ImageFormat;
use sha2::{Digest, Sha256};

use crate::paths::{ensure_parent_dir, thumbnail_cache_root};

const MAX_CACHE_ENTRIES: usize = 2000;

static CACHE_LOCK: Mutex<()> = Mutex::new(());

fn cache_key(path: &str, size: u32) -> String {
    let mut hasher = Sha256::new();
    hasher.update(path.as_bytes());
    hasher.update(b"|");
    hasher.update(size.to_le_bytes());
    if let Ok(meta) = fs::metadata(path) {
        if let Ok(modified) = meta.modified() {
            if let Ok(d) = modified.duration_since(std::time::UNIX_EPOCH) {
                hasher.update(d.as_nanos().to_le_bytes());
            }
        }
        hasher.update(meta.len().to_le_bytes());
    }
    hex::encode(hasher.finalize())
}

pub fn thumbnail_path(source_path: &str, logical_size: u32) -> PathBuf {
    let key = cache_key(source_path, logical_size);
    thumbnail_cache_root().join(format!("{key}.png"))
}

/// Decode and write a PNG thumbnail. Returns the cache path when successful.
pub fn ensure_thumbnail(source_path: &str, logical_size: u32) -> Result<PathBuf, String> {
    let out = thumbnail_path(source_path, logical_size);
    if out.exists() {
        return Ok(out);
    }
    ensure_parent_dir(&out).map_err(|e| e.to_string())?;

    let img = image::open(source_path).map_err(|e| e.to_string())?;
    let thumb = img.thumbnail(logical_size, logical_size);
    // Prefer resize_to_fill for square tiles when needed; thumbnail preserves aspect.
    let _ = FilterType::Triangle;
    thumb
        .save_with_format(&out, ImageFormat::Png)
        .map_err(|e| e.to_string())?;

    prune_cache_if_needed();
    Ok(out)
}

fn prune_cache_if_needed() {
    let _guard = CACHE_LOCK.lock().unwrap_or_else(|e| e.into_inner());
    let root = thumbnail_cache_root();
    let Ok(read) = fs::read_dir(&root) else {
        return;
    };
    let mut files: Vec<(PathBuf, std::time::SystemTime)> = read
        .filter_map(|e| e.ok())
        .filter_map(|e| {
            let path = e.path();
            if path.extension().and_then(|x| x.to_str()) != Some("png") {
                return None;
            }
            let modified = e.metadata().ok()?.modified().ok()?;
            Some((path, modified))
        })
        .collect();
    if files.len() <= MAX_CACHE_ENTRIES {
        return;
    }
    files.sort_by_key(|(_, t)| *t);
    let remove_count = files.len() - MAX_CACHE_ENTRIES;
    for (path, _) in files.into_iter().take(remove_count) {
        let _ = fs::remove_file(path);
    }
}

pub fn load_thumbnail_rgba(source_path: &str, logical_size: u32) -> Result<(u32, u32, Vec<u8>), String> {
    let path = ensure_thumbnail(source_path, logical_size)?;
    let img = image::open(path).map_err(|e| e.to_string())?.into_rgba8();
    let (w, h) = img.dimensions();
    Ok((w, h, img.into_raw()))
}
