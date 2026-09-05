use std::path::Path;
use std::process::Command;

#[cfg(any(target_os = "linux", test))]
use std::process::{Child, ExitStatus};
#[cfg(any(target_os = "linux", test))]
use std::time::{Duration, Instant};

use piclens_domain::has_link_or_junction_component;
use thiserror::Error;

use crate::CancellationToken;

#[cfg(target_os = "linux")]
const HELPER_TIMEOUT: Duration = Duration::from_secs(15);

#[derive(Debug, Error)]
pub enum PlatformError {
    #[error("{0}")]
    Message(String),
}

pub fn move_to_trash_cancellable(
    path: &str,
    cancellation: &CancellationToken,
) -> Result<(), PlatformError> {
    #[cfg(not(target_os = "linux"))]
    let _ = cancellation;
    if has_link_or_junction_component(path) {
        return Err(PlatformError::Message(
            "Trash path cannot contain a symbolic link or junction.".into(),
        ));
    }
    let path = Path::new(path);
    if !path.exists() {
        return Err(PlatformError::Message("Source path does not exist.".into()));
    }

    #[cfg(windows)]
    {
        trash_windows(path)
    }
    #[cfg(target_os = "linux")]
    {
        trash_linux(path, cancellation)
    }
    #[cfg(not(any(windows, target_os = "linux")))]
    {
        let _ = path;
        Err(PlatformError::Message(
            "Trash is only supported on Windows and Linux.".into(),
        ))
    }
}

#[cfg(windows)]
fn trash_windows(path: &Path) -> Result<(), PlatformError> {
    use std::os::windows::ffi::OsStrExt;
    use windows::core::PCWSTR;
    use windows::Win32::UI::Shell::{
        SHFileOperationW, FOF_ALLOWUNDO, FOF_NOCONFIRMATION, FOF_NOERRORUI, FOF_SILENT, FO_DELETE,
        SHFILEOPSTRUCTW,
    };

    let mut source: Vec<u16> = path
        .as_os_str()
        .encode_wide()
        .chain(std::iter::once(0))
        .chain(std::iter::once(0))
        .collect();

    let mut op = SHFILEOPSTRUCTW {
        hwnd: windows::Win32::Foundation::HWND::default(),
        wFunc: FO_DELETE,
        pFrom: PCWSTR(source.as_mut_ptr()),
        pTo: PCWSTR::null(),
        fFlags: (FOF_ALLOWUNDO.0 | FOF_NOCONFIRMATION.0 | FOF_NOERRORUI.0 | FOF_SILENT.0) as u16,
        fAnyOperationsAborted: windows::core::BOOL(0),
        hNameMappings: std::ptr::null_mut(),
        lpszProgressTitle: PCWSTR::null(),
    };

    let result = unsafe { SHFileOperationW(&mut op) };
    if result != 0 || op.fAnyOperationsAborted.as_bool() {
        return Err(PlatformError::Message(format!(
            "Recycle Bin operation failed with code {result}."
        )));
    }
    Ok(())
}

#[cfg(target_os = "linux")]
fn trash_linux(path: &Path, cancellation: &CancellationToken) -> Result<(), PlatformError> {
    let child = Command::new("gio")
        .args(["trash", &path.to_string_lossy()])
        .spawn()
        .map_err(|e| PlatformError::Message(format!("gio trash failed: {e}")))?;
    let status = wait_for_helper(child, HELPER_TIMEOUT, cancellation)?;
    if !status.success() {
        return Err(PlatformError::Message(format!(
            "gio trash failed with status {status}"
        )));
    }
    Ok(())
}

#[cfg(any(target_os = "linux", test))]
fn wait_for_helper(
    mut child: Child,
    timeout: Duration,
    cancellation: &CancellationToken,
) -> Result<ExitStatus, PlatformError> {
    let deadline = Instant::now() + timeout;
    loop {
        if cancellation.is_canceled() {
            let _ = child.kill();
            let _ = child.wait();
            return Err(PlatformError::Message("helper canceled".into()));
        }
        match child.try_wait() {
            Ok(Some(status)) => return Ok(status),
            Ok(None) if Instant::now() < deadline => {
                std::thread::sleep(Duration::from_millis(10));
            }
            Ok(None) => {
                let _ = child.kill();
                let _ = child.wait();
                return Err(PlatformError::Message(format!(
                    "helper timed out after {} ms",
                    timeout.as_millis()
                )));
            }
            Err(error) => {
                return Err(PlatformError::Message(format!(
                    "helper wait failed: {error}"
                )));
            }
        }
    }
}

pub fn reveal_in_file_manager(path: &str) -> Result<(), PlatformError> {
    if has_link_or_junction_component(path) {
        return Err(PlatformError::Message(
            "Reveal path cannot contain a symbolic link or junction.".into(),
        ));
    }
    let path = Path::new(path);
    if !path.is_file() {
        return Err(PlatformError::Message(
            "Reveal path must be an existing file.".into(),
        ));
    }

    #[cfg(windows)]
    {
        let native = path.to_string_lossy().replace('/', "\\");
        Command::new("explorer.exe")
            .arg(format!("/select,{native}"))
            .spawn()
            .map_err(|e| PlatformError::Message(e.to_string()))?;
        Ok(())
    }
    #[cfg(target_os = "linux")]
    {
        use std::path::PathBuf;

        let parent = path
            .parent()
            .map(|p| p.to_path_buf())
            .unwrap_or_else(|| PathBuf::from("."));
        Command::new("xdg-open")
            .arg(parent)
            .spawn()
            .map_err(|e| PlatformError::Message(e.to_string()))?;
        Ok(())
    }
    #[cfg(not(any(windows, target_os = "linux")))]
    {
        Err(PlatformError::Message(
            "Reveal is only supported on Windows and Linux.".into(),
        ))
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[cfg(windows)]
    fn stalled_child() -> Child {
        Command::new("powershell")
            .args(["-NoProfile", "-Command", "Start-Sleep -Seconds 5"])
            .spawn()
            .unwrap()
    }

    #[cfg(unix)]
    fn stalled_child() -> Child {
        Command::new("sh").args(["-c", "sleep 5"]).spawn().unwrap()
    }

    #[test]
    fn stalled_helper_is_killed_at_timeout() {
        let result = wait_for_helper(
            stalled_child(),
            Duration::from_millis(50),
            &CancellationToken::new(),
        );
        assert!(result.unwrap_err().to_string().contains("timed out"));
    }

    #[test]
    fn canceled_helper_is_killed() {
        let cancellation = CancellationToken::new();
        cancellation.cancel();
        let result = wait_for_helper(stalled_child(), Duration::from_secs(1), &cancellation);
        assert!(result.unwrap_err().to_string().contains("canceled"));
    }
}
