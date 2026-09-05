use std::fs;
use std::io::{Read, Write};
use std::panic::{catch_unwind, AssertUnwindSafe};
use std::path::{Path, PathBuf};
use std::process::{Child, Command, Stdio};
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::{Condvar, Mutex, OnceLock};
use std::time::{Duration, Instant};

use image::ImageFormat;
use sha2::{Digest, Sha256};

use crate::paths::{ensure_parent_dir, thumbnail_cache_root};
use crate::CancellationToken;

const MAX_CACHE_ENTRIES: usize = 2000;
const MAX_DECODE_PROCESSES: usize = 8;
pub const MAX_ORIGINAL_RGBA_BYTES: usize = 256 * 1024 * 1024;

/// Worker output is temporary raw RGBA, never a downsampled or persistent cache entry.
pub fn write_original_rgba(source: &str, output: &Path) -> Result<(), String> {
    let mut reader = image::ImageReader::open(source)
        .map_err(|e| e.to_string())?
        .with_guessed_format()
        .map_err(|e| e.to_string())?;
    let mut limits = image::Limits::default();
    limits.max_alloc = Some(MAX_ORIGINAL_RGBA_BYTES as u64);
    reader.limits(limits);
    let decoded = reader.decode().map_err(|e| e.to_string())?;
    let (width, height) = (decoded.width(), decoded.height());
    original_rgba_len(width, height)?;
    let rgba = decoded.into_rgba8();
    let mut file = fs::File::create(output).map_err(|e| e.to_string())?;
    file.write_all(&width.to_le_bytes())
        .and_then(|_| file.write_all(&height.to_le_bytes()))
        .and_then(|_| file.write_all(rgba.as_raw()))
        .map_err(|e| e.to_string())
}

fn original_rgba_len(width: u32, height: u32) -> Result<usize, String> {
    (width as usize)
        .checked_mul(height as usize)
        .and_then(|pixels| pixels.checked_mul(4))
        .filter(|bytes| *bytes > 0 && *bytes <= MAX_ORIGINAL_RGBA_BYTES)
        .ok_or_else(|| "原圖超過 256 MiB RGBA 像素上限或尺寸無效。".into())
}

pub fn load_original_with_timeout(
    source: &str,
    executable: &Path,
    timeout: Duration,
    cancellation: &CancellationToken,
) -> Result<(u32, u32, Vec<u8>), String> {
    use std::sync::atomic::AtomicU64;
    static NEXT_OUTPUT: AtomicU64 = AtomicU64::new(1);
    let _permit = acquire_decode_permit(cancellation)?;
    let output = thumbnail_cache_root().join(format!(
        "original-{}-{}.rgba",
        std::process::id(),
        NEXT_OUTPUT.fetch_add(1, Ordering::Relaxed)
    ));
    ensure_parent_dir(&output).map_err(|e| e.to_string())?;
    let result = (|| {
        let mut child = Command::new(executable)
            .arg("--original-worker")
            .arg(source)
            .arg(&output)
            .stdin(Stdio::null())
            .stdout(Stdio::null())
            .stderr(Stdio::piped())
            .spawn()
            .map_err(|e| e.to_string())?;
        if let Err(error) = wait_for_child(&mut child, timeout, cancellation) {
            let mut detail = String::new();
            if let Some(mut stderr) = child.stderr.take() {
                let _ = stderr.read_to_string(&mut detail);
            }
            return Err(format!("{error}: {}", detail.trim()));
        }
        if cancellation.is_canceled() {
            return Err("original canceled".into());
        }
        let mut file = fs::File::open(&output).map_err(|e| e.to_string())?;
        let mut header = [0; 8];
        file.read_exact(&mut header).map_err(|e| e.to_string())?;
        let width = u32::from_le_bytes(header[..4].try_into().unwrap());
        let height = u32::from_le_bytes(header[4..].try_into().unwrap());
        let len = original_rgba_len(width, height)?;
        if file.metadata().map_err(|e| e.to_string())?.len() != len as u64 + 8 {
            return Err("原圖解碼輸出長度無效。".into());
        }
        let mut rgba = vec![0; len];
        file.read_exact(&mut rgba).map_err(|e| e.to_string())?;
        if cancellation.is_canceled() {
            return Err("original canceled".into());
        }
        Ok((width, height, rgba))
    })();
    let _ = fs::remove_file(output);
    result
}

// Set in the parent process, not in the short-lived decoder workers.
// Start dirty so an oversized cache from a previous run is also pruned.
static CACHE_DIRTY: AtomicBool = AtomicBool::new(true);
static DECODE_LIMITER: OnceLock<(Mutex<usize>, Condvar)> = OnceLock::new();

struct DecodePermit;

impl Drop for DecodePermit {
    fn drop(&mut self) {
        let (active, wake) = DECODE_LIMITER.get_or_init(|| (Mutex::new(0), Condvar::new()));
        let mut active = active.lock().unwrap_or_else(|err| err.into_inner());
        *active = active.saturating_sub(1);
        wake.notify_one();
    }
}

fn acquire_decode_permit(cancellation: &CancellationToken) -> Result<DecodePermit, String> {
    let (active, wake) = DECODE_LIMITER.get_or_init(|| (Mutex::new(0), Condvar::new()));
    let mut active = active.lock().unwrap_or_else(|err| err.into_inner());
    while *active >= MAX_DECODE_PROCESSES {
        if cancellation.is_canceled() {
            return Err("thumbnail canceled".into());
        }
        active = wake
            .wait_timeout(active, Duration::from_millis(25))
            .unwrap_or_else(|err| err.into_inner())
            .0;
    }
    if cancellation.is_canceled() {
        return Err("thumbnail canceled".into());
    }
    *active += 1;
    Ok(DecodePermit)
}

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

/// Decode and write a PNG thumbnail. Guess format from content so misnamed files work.
/// Decode panics and I/O failures become `Err` — never unwind across the UI.
/// Cache maintenance belongs to the parent, not to this decoder worker.
pub fn ensure_thumbnail(source_path: &str, logical_size: u32) -> Result<PathBuf, String> {
    let out = thumbnail_path(source_path, logical_size);
    if out.exists() {
        return Ok(out);
    }
    ensure_parent_dir(&out).map_err(|e| e.to_string())?;

    let source = source_path.to_string();
    let size = logical_size.max(16);
    let decoded = catch_unwind(AssertUnwindSafe(|| decode_and_resize(&source, size)));
    let thumb = match decoded {
        Ok(Ok(img)) => img,
        Ok(Err(err)) => return Err(err),
        Err(_) => return Err(format!("thumbnail decode panicked: {source_path}")),
    };

    let temporary = out.with_extension(format!("{}.tmp", std::process::id()));
    let save = catch_unwind(AssertUnwindSafe(|| {
        thumb
            .save_with_format(&temporary, ImageFormat::Png)
            .map_err(|e| e.to_string())
    }));
    match save {
        Ok(Ok(())) => {
            if let Err(err) = fs::rename(&temporary, &out) {
                let _ = fs::remove_file(&temporary);
                if !out.exists() {
                    return Err(err.to_string());
                }
            }
            Ok(out)
        }
        Ok(Err(err)) => {
            let _ = fs::remove_file(&temporary);
            Err(err)
        }
        Err(_) => {
            let _ = fs::remove_file(&temporary);
            Err(format!("thumbnail encode panicked: {source_path}"))
        }
    }
}

fn wait_for_child(
    child: &mut Child,
    timeout: Duration,
    cancellation: &CancellationToken,
) -> Result<(), String> {
    let deadline = Instant::now() + timeout;
    loop {
        if cancellation.is_canceled() {
            let _ = child.kill();
            let _ = child.wait();
            return Err("thumbnail canceled".into());
        }
        match child.try_wait() {
            Ok(Some(status)) if status.success() => return Ok(()),
            Ok(Some(status)) => return Err(format!("thumbnail worker exited with {status}")),
            Ok(None) if Instant::now() < deadline => std::thread::sleep(Duration::from_millis(10)),
            Ok(None) => {
                let _ = child.kill();
                let _ = child.wait();
                return Err(format!(
                    "thumbnail decode timed out after {} ms",
                    timeout.as_millis()
                ));
            }
            Err(err) => return Err(format!("thumbnail worker wait failed: {err}")),
        }
    }
}

/// Decode through a killable child process. This keeps the physical decoder
/// count bounded and lets one stalled codec release its slot after timeout.
pub fn ensure_thumbnail_with_timeout(
    source_path: &str,
    logical_size: u32,
    worker_executable: &Path,
    timeout: Duration,
    cancellation: &CancellationToken,
) -> Result<PathBuf, String> {
    let out = thumbnail_path(source_path, logical_size);
    if out.exists() {
        return Ok(out);
    }
    let _permit = acquire_decode_permit(cancellation)?;
    let mut child = Command::new(worker_executable)
        .arg("--thumbnail-worker")
        .arg(source_path)
        .arg(logical_size.to_string())
        .stdin(Stdio::null())
        .stdout(Stdio::null())
        .stderr(Stdio::piped())
        .spawn()
        .map_err(|err| format!("thumbnail worker start failed: {err}"))?;
    let result = wait_for_child(&mut child, timeout, cancellation);
    // Even a canceled worker may have published its PNG before it was killed.
    CACHE_DIRTY.store(true, Ordering::Relaxed);
    result?;
    out.exists()
        .then_some(out)
        .ok_or_else(|| "thumbnail worker did not produce a cache file".into())
}

fn decode_and_resize(source_path: &str, logical_size: u32) -> Result<image::DynamicImage, String> {
    let reader = image::ImageReader::open(source_path)
        .map_err(|e| e.to_string())?
        .with_guessed_format()
        .map_err(|e| e.to_string())?;
    let img = reader.decode().map_err(|e| e.to_string())?;
    Ok(img.thumbnail(logical_size, logical_size))
}

/// Called periodically by one background task in the parent process.
/// Idle and cache-hit-only intervals do not scan the cache directory.
pub fn prune_thumbnail_cache_if_needed() {
    if let Err(err) = prune_dirty_cache(&thumbnail_cache_root(), &CACHE_DIRTY, MAX_CACHE_ENTRIES) {
        crate::warn(format!("thumbnail cache cleanup failed: {err}"));
    }
}

fn prune_dirty_cache(root: &Path, dirty: &AtomicBool, capacity: usize) -> std::io::Result<usize> {
    if !dirty.swap(false, Ordering::Relaxed) {
        return Ok(0);
    }
    let result = prune_cache(root, capacity);
    if result.is_err() {
        dirty.store(true, Ordering::Relaxed);
    }
    result
}

fn prune_cache(root: &Path, capacity: usize) -> std::io::Result<usize> {
    let read = match fs::read_dir(root) {
        Ok(read) => read,
        Err(err) if err.kind() == std::io::ErrorKind::NotFound => return Ok(0),
        Err(err) => return Err(err),
    };
    let mut files: Vec<(PathBuf, std::time::SystemTime)> = read
        .filter_map(|e| e.ok())
        .filter_map(|e| {
            let path = e.path();
            if path.extension().and_then(|x| x.to_str()) != Some("png")
                || !e.file_type().ok()?.is_file()
            {
                return None;
            }
            let modified = e.metadata().ok()?.modified().ok()?;
            Some((path, modified))
        })
        .collect();
    if files.len() <= capacity {
        return Ok(0);
    }
    files.sort_by_key(|(_, t)| *t);
    let remove_count = files.len() - capacity;
    for (path, _) in files.into_iter().take(remove_count) {
        match fs::remove_file(path) {
            Ok(()) => {}
            Err(err) if err.kind() == std::io::ErrorKind::NotFound => {}
            Err(err) => return Err(err),
        }
    }
    Ok(remove_count)
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::io::Write;

    #[test]
    fn cache_cleanup_keeps_newest_pngs_and_preserves_other_files() {
        let dir = std::env::temp_dir().join(format!("piclens-prune-test-{}", std::process::id()));
        let cache = dir.join("cache");
        fs::create_dir_all(&cache).unwrap();
        let source = dir.join("source.png");
        fs::write(&source, b"source must not change").unwrap();
        fs::write(cache.join("pending.tmp"), b"unfinished thumbnail").unwrap();
        fs::create_dir(cache.join("directory.png")).unwrap();
        for index in 0..3 {
            let file = fs::File::create(cache.join(format!("{index}.png"))).unwrap();
            file.set_modified(std::time::UNIX_EPOCH + Duration::from_secs(index + 1))
                .unwrap();
        }

        assert_eq!(prune_cache(&cache, 2).unwrap(), 1);
        assert!(!cache.join("0.png").exists());
        assert!(cache.join("1.png").exists());
        assert!(cache.join("2.png").exists());
        assert!(cache.join("pending.tmp").exists());
        assert!(cache.join("directory.png").is_dir());
        assert_eq!(fs::read(source).unwrap(), b"source must not change");
        assert_eq!(prune_cache(&cache, 2).unwrap(), 0);
        fs::remove_dir_all(dir).unwrap();
    }

    #[test]
    fn cache_cleanup_skips_clean_intervals_and_retries_failed_passes() {
        let root = std::env::temp_dir().join(format!("piclens-prune-retry-{}", std::process::id()));
        // A regular file makes directory reads fail. A clean interval must not
        // attempt that read, but a dirty interval must retain its retry signal.
        fs::write(&root, b"not a directory").unwrap();
        let dirty = AtomicBool::new(false);
        assert_eq!(prune_dirty_cache(&root, &dirty, 2).unwrap(), 0);
        dirty.store(true, Ordering::Relaxed);
        assert!(prune_dirty_cache(&root, &dirty, 2).is_err());
        assert!(dirty.load(Ordering::Relaxed));

        fs::remove_file(&root).unwrap();
        fs::create_dir(&root).unwrap();
        for index in 0..4 {
            fs::write(root.join(format!("{index}.png")), b"cached").unwrap();
        }
        assert_eq!(prune_dirty_cache(&root, &dirty, 2).unwrap(), 2);
        assert!(!dirty.load(Ordering::Relaxed));
        fs::remove_dir_all(root).unwrap();
    }

    #[test]
    fn cache_cleanup_accepts_a_missing_cache_directory() {
        let root =
            std::env::temp_dir().join(format!("piclens-prune-missing-{}", std::process::id()));
        let dirty = AtomicBool::new(true);
        assert_eq!(prune_dirty_cache(&root, &dirty, 2).unwrap(), 0);
        assert!(!root.exists());
        assert!(!dirty.load(Ordering::Relaxed));
    }

    #[test]
    fn corrupt_bytes_do_not_panic_ensure_thumbnail() {
        let dir = std::env::temp_dir().join(format!("piclens-thumb-test-{}", std::process::id()));
        let _ = fs::create_dir_all(&dir);
        let bad = dir.join("bad.jpg");
        let mut f = fs::File::create(&bad).unwrap();
        // RIFF header-ish payload that is not a valid JPEG
        f.write_all(b"RIFF....WEBPNOTVALID").unwrap();
        drop(f);

        // Point cache under temp via PICLENS_DATA_ROOT
        let data = dir.join("data");
        std::env::set_var("PICLENS_DATA_ROOT", &data);
        let result = ensure_thumbnail(bad.to_str().unwrap(), 64);
        assert!(result.is_err(), "expected Err, got {result:?}");
        let _ = fs::remove_dir_all(&dir);
    }

    #[test]
    fn canceled_worker_does_not_start() {
        let token = CancellationToken::new();
        token.cancel();
        let result = ensure_thumbnail_with_timeout(
            "missing.jpg",
            64,
            Path::new("missing-worker"),
            Duration::from_millis(10),
            &token,
        );
        assert_eq!(result.unwrap_err(), "thumbnail canceled");
    }

    #[cfg(windows)]
    #[test]
    fn stalled_child_is_killed_at_timeout() {
        let mut child = Command::new("powershell")
            .args(["-NoProfile", "-Command", "Start-Sleep -Seconds 5"])
            .spawn()
            .unwrap();
        let result = wait_for_child(
            &mut child,
            Duration::from_millis(50),
            &CancellationToken::new(),
        );
        assert!(result.unwrap_err().contains("timed out"));
    }

    #[cfg(unix)]
    #[test]
    fn stalled_child_is_killed_at_timeout() {
        let mut child = Command::new("sh").args(["-c", "sleep 5"]).spawn().unwrap();
        let result = wait_for_child(
            &mut child,
            Duration::from_millis(50),
            &CancellationToken::new(),
        );
        assert!(result.unwrap_err().contains("timed out"));
    }
}
