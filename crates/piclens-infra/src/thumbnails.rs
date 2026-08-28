use std::fs;
use std::panic::{catch_unwind, AssertUnwindSafe};
use std::path::{Path, PathBuf};
use std::process::{Child, Command, Stdio};
use std::sync::{Condvar, Mutex, OnceLock};
use std::time::{Duration, Instant};

use image::ImageFormat;
use sha2::{Digest, Sha256};

use crate::paths::{ensure_parent_dir, thumbnail_cache_root};
use crate::CancellationToken;

const MAX_CACHE_ENTRIES: usize = 2000;
const MAX_DECODE_PROCESSES: usize = 8;

static CACHE_LOCK: Mutex<()> = Mutex::new(());
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
            prune_cache_if_needed();
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
    wait_for_child(&mut child, timeout, cancellation)?;
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

pub fn load_thumbnail_rgba(
    source_path: &str,
    logical_size: u32,
) -> Result<(u32, u32, Vec<u8>), String> {
    let path = ensure_thumbnail(source_path, logical_size)?;
    let img = image::open(path).map_err(|e| e.to_string())?.into_rgba8();
    let (w, h) = img.dimensions();
    Ok((w, h, img.into_raw()))
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::io::Write;

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
