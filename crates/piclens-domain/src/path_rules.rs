use std::path::{Component, Path, PathBuf};

#[cfg(windows)]
pub fn path_case_insensitive() -> bool {
    true
}

#[cfg(not(windows))]
pub fn path_case_insensitive() -> bool {
    false
}

pub fn path_key(path: &str) -> String {
    let absolute = normalize_absolute(path);
    if path_case_insensitive() {
        absolute.to_lowercase()
    } else {
        absolute
    }
}

pub fn path_equals(left: &str, right: &str) -> bool {
    !left.is_empty() && !right.is_empty() && path_key(left) == path_key(right)
}

pub fn normalize_absolute(path: &str) -> String {
    let path = Path::new(path);
    let absolute = if path.is_absolute() {
        path.to_path_buf()
    } else {
        std::env::current_dir()
            .unwrap_or_else(|_| PathBuf::from("."))
            .join(path)
    };
    clean_path(&absolute)
}

fn clean_path(path: &Path) -> String {
    let mut out = PathBuf::new();
    for component in path.components() {
        match component {
            Component::CurDir => {}
            Component::ParentDir => {
                out.pop();
            }
            other => out.push(other.as_os_str()),
        }
    }
    out.to_string_lossy().replace('\\', "/")
}

pub fn has_same_directory_and_basename_without_extension(left: &str, right: &str) -> bool {
    let left_path = Path::new(left);
    let right_path = Path::new(right);
    let left_parent = left_path
        .parent()
        .map(|p| p.to_string_lossy().to_string())
        .unwrap_or_default();
    let right_parent = right_path
        .parent()
        .map(|p| p.to_string_lossy().to_string())
        .unwrap_or_default();
    if !path_equals(&left_parent, &right_parent) {
        return false;
    }
    let left_stem = left_path
        .file_stem()
        .and_then(|s| s.to_str())
        .unwrap_or_default();
    let right_stem = right_path
        .file_stem()
        .and_then(|s| s.to_str())
        .unwrap_or_default();
    if path_case_insensitive() {
        left_stem.eq_ignore_ascii_case(right_stem)
    } else {
        left_stem == right_stem
    }
}

pub fn target_name_exists(
    existing_paths: &[String],
    candidate_path: &str,
    source_path: &str,
) -> bool {
    existing_paths.iter().any(|path| {
        !path_equals(path, source_path)
            && has_same_directory_and_basename_without_extension(path, candidate_path)
    })
}

/// Best-effort symlink / junction detection for path components.
pub fn has_link_or_junction_component(path: &str) -> bool {
    let path = Path::new(path);
    if path
        .symlink_metadata()
        .map(|m| m.file_type().is_symlink())
        .unwrap_or(false)
    {
        return true;
    }

    let mut current = path.parent().map(Path::to_path_buf);
    while let Some(mut dir) = current {
        if dir
            .symlink_metadata()
            .map(|m| m.file_type().is_symlink())
            .unwrap_or(false)
        {
            return true;
        }
        if !dir.pop() {
            break;
        }
        current = Some(dir);
    }
    false
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn same_basename_ignores_extension() {
        assert!(has_same_directory_and_basename_without_extension(
            "/tmp/a/photo.jpg",
            "/tmp/a/photo.png"
        ));
    }

    #[test]
    fn target_name_exists_skips_source() {
        let existing = vec!["/tmp/a/photo.jpg".into(), "/tmp/a/other.png".into()];
        assert!(!target_name_exists(
            &existing,
            "/tmp/a/photo.webp",
            "/tmp/a/photo.jpg"
        ));
        assert!(target_name_exists(
            &existing,
            "/tmp/a/photo.webp",
            "/tmp/a/different.jpg"
        ));
    }

    #[test]
    fn path_key_normalizes_slashes() {
        let a = path_key(r"C:\Pics\a");
        let b = path_key("C:/Pics/a");
        if path_case_insensitive() {
            assert_eq!(a, b);
        }
    }
}
