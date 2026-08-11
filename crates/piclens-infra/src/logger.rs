use std::fs::OpenOptions;
use std::io::Write;
use std::sync::Mutex;

use chrono::Local;

use crate::paths::{ensure_parent_dir, log_path};

static LOG_LOCK: Mutex<()> = Mutex::new(());

pub fn init_file_logger() {
    let _ = ensure_parent_dir(&log_path());
}

pub fn log_line(level: &str, message: &str) {
    let _guard = LOG_LOCK.lock().unwrap_or_else(|e| e.into_inner());
    let path = log_path();
    let _ = ensure_parent_dir(&path);
    if let Ok(mut file) = OpenOptions::new().create(true).append(true).open(&path) {
        let now = Local::now().format("%Y-%m-%d %H:%M:%S");
        let _ = writeln!(file, "{now} [{level}] {message}");
    }
    match level {
        "ERROR" => log::error!("{message}"),
        "WARN" => log::warn!("{message}"),
        _ => log::info!("{message}"),
    }
}

pub fn info(message: impl AsRef<str>) {
    log_line("INFO", message.as_ref());
}

pub fn warn(message: impl AsRef<str>) {
    log_line("WARN", message.as_ref());
}

pub fn error(message: impl AsRef<str>) {
    log_line("ERROR", message.as_ref());
}
