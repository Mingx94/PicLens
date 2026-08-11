use crate::image_format::supported_image_extension;
use crate::path_rules::{path_case_insensitive, path_equals, target_name_exists};

pub const ALREADY_TARGET_SEQUENCE_REASON: &str = "already_target_sequence";

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct FileNameValidationResult {
    pub is_valid: bool,
    pub reason: Option<String>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct DropTargetBatchRenamePlanItem {
    pub source_path: String,
    pub target_path: String,
    pub should_skip: bool,
    pub reason: Option<String>,
}

#[derive(Debug, Clone, PartialEq, Eq, Default)]
pub struct DropTargetBatchRenamePlan {
    pub total: usize,
    pub items: Vec<DropTargetBatchRenamePlanItem>,
}

fn contains_reserved_file_name_character(file_name: &str) -> bool {
    file_name.chars().any(|ch| {
        matches!(ch, '<' | '>' | ':' | '"' | '/' | '\\' | '|' | '?' | '*') || ch.is_control()
    })
}

pub fn validate_image_file_name(file_name: &str) -> FileNameValidationResult {
    if file_name.trim().is_empty() {
        return FileNameValidationResult {
            is_valid: false,
            reason: Some("empty_name".into()),
        };
    }
    if contains_reserved_file_name_character(file_name) {
        return FileNameValidationResult {
            is_valid: false,
            reason: Some("invalid_file_name".into()),
        };
    }
    if supported_image_extension(file_name).is_none() {
        return FileNameValidationResult {
            is_valid: false,
            reason: Some("unsupported_extension".into()),
        };
    }
    FileNameValidationResult {
        is_valid: true,
        reason: None,
    }
}

fn starts_with_prefix(name: &str, prefix: &str) -> bool {
    if path_case_insensitive() {
        name.to_lowercase().starts_with(&prefix.to_lowercase())
    } else {
        name.starts_with(prefix)
    }
}

fn try_extract_sequence_number(target_path: &str, target_base_name: &str) -> Option<i32> {
    let target_name = std::path::Path::new(target_path)
        .file_stem()
        .and_then(|s| s.to_str())
        .unwrap_or_default();
    let prefix = format!("{target_base_name}-");
    if !starts_with_prefix(target_name, &prefix) {
        return None;
    }
    let suffix = &target_name[prefix.len()..];
    if suffix.is_empty() || !suffix.chars().all(|c| c.is_ascii_digit()) {
        return None;
    }
    suffix.parse().ok()
}

fn extract_sequence_number(target_path: &str, target_base_name: &str) -> i32 {
    try_extract_sequence_number(target_path, target_base_name)
        .expect("Target path must include a target sequence number.")
}

fn create_sequence_target_path(
    source_path: &str,
    target_directory: &str,
    target_base_name: &str,
    sequence_number: i32,
) -> String {
    let extension = std::path::Path::new(source_path)
        .extension()
        .and_then(|e| e.to_str())
        .map(|e| format!(".{e}"))
        .unwrap_or_default();
    let file_name = format!("{target_base_name}-{sequence_number:02}{extension}");
    std::path::Path::new(target_directory)
        .join(file_name)
        .to_string_lossy()
        .replace('\\', "/")
}

fn next_available_sequence_target_path(
    source_path: &str,
    target_directory: &str,
    target_base_name: &str,
    sequence_number: i32,
    existing_paths: &[String],
) -> String {
    let mut candidate_sequence = sequence_number;
    loop {
        let candidate = create_sequence_target_path(
            source_path,
            target_directory,
            target_base_name,
            candidate_sequence,
        );
        if !target_name_exists(existing_paths, &candidate, source_path) {
            return candidate;
        }
        candidate_sequence += 1;
    }
}

fn create_plan_item(
    source_path: &str,
    target_directory: &str,
    target_base_name: &str,
    sequence_number: i32,
    existing_paths: &[String],
) -> DropTargetBatchRenamePlanItem {
    if let Some(source_sequence) = try_extract_sequence_number(source_path, target_base_name) {
        if source_sequence < sequence_number
            && !target_name_exists(existing_paths, source_path, source_path)
        {
            return DropTargetBatchRenamePlanItem {
                source_path: source_path.to_string(),
                target_path: source_path.to_string(),
                should_skip: true,
                reason: Some(ALREADY_TARGET_SEQUENCE_REASON.into()),
            };
        }
    }

    let next_target = next_available_sequence_target_path(
        source_path,
        target_directory,
        target_base_name,
        sequence_number,
        existing_paths,
    );
    if path_equals(source_path, &next_target) {
        return DropTargetBatchRenamePlanItem {
            source_path: source_path.to_string(),
            target_path: next_target,
            should_skip: true,
            reason: Some(ALREADY_TARGET_SEQUENCE_REASON.into()),
        };
    }

    DropTargetBatchRenamePlanItem {
        source_path: source_path.to_string(),
        target_path: next_target,
        should_skip: false,
        reason: None,
    }
}

pub fn plan_drop_target_batch_rename(
    source_paths: &[String],
    target_path: &str,
    existing_paths: &[String],
) -> DropTargetBatchRenamePlan {
    let target = std::path::Path::new(target_path);
    let target_directory = target
        .parent()
        .map(|p| p.to_string_lossy().replace('\\', "/"))
        .unwrap_or_else(|| ".".into());
    let target_base_name = target
        .file_stem()
        .and_then(|s| s.to_str())
        .unwrap_or_default()
        .to_string();

    let mut items = Vec::new();
    let mut sequence_number = 1i32;

    for source_path in source_paths {
        if path_equals(source_path, target_path) {
            continue;
        }
        let item = create_plan_item(
            source_path,
            &target_directory,
            &target_base_name,
            sequence_number,
            existing_paths,
        );
        sequence_number = sequence_number.max(
            extract_sequence_number(&item.target_path, &target_base_name) + 1,
        );
        items.push(item);
    }

    DropTargetBatchRenamePlan {
        total: items.len(),
        items,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn rejects_empty_name() {
        assert!(!validate_image_file_name("  ").is_valid);
    }

    #[test]
    fn plans_sequential_names() {
        let plan = plan_drop_target_batch_rename(
            &["/tmp/a/one.jpg".into(), "/tmp/a/two.jpg".into()],
            "/tmp/a/base.png",
            &["/tmp/a/base.png".into()],
        );
        assert_eq!(plan.total, 2);
        assert!(plan.items[0].target_path.contains("base-01"));
        assert!(plan.items[1].target_path.contains("base-02"));
    }
}
