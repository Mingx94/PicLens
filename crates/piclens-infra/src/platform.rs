use std::path::Path;
use std::process::Command;

use piclens_domain::has_link_or_junction_component;
use thiserror::Error;

#[derive(Debug, Error)]
pub enum PlatformError {
    #[error("{0}")]
    Message(String),
}

pub fn move_to_trash(path: &str) -> Result<(), PlatformError> {
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
        trash_linux(path)
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
fn trash_linux(path: &Path) -> Result<(), PlatformError> {
    let output = Command::new("gio")
        .args(["trash", &path.to_string_lossy()])
        .output()
        .map_err(|e| PlatformError::Message(format!("gio trash failed: {e}")))?;
    if !output.status.success() {
        return Err(PlatformError::Message(format!(
            "gio trash failed: {}",
            String::from_utf8_lossy(&output.stdout)
        )));
    }
    Ok(())
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
