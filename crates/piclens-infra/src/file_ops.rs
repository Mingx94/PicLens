use std::fs;
use std::path::{Path, PathBuf};

use image::ImageFormat;
use piclens_domain::{
    has_link_or_junction_component, is_jpg_extension, is_webp_extension, path_equals, path_key,
    plan_drop_target_batch_rename, supported_image_extension, target_name_exists,
    validate_image_file_name, DropTargetBatchRenamePlan, FileOperationBatchResult,
    FileOperationResult, FileOperationStatus,
};

use crate::platform::{move_to_trash, PlatformError};
use crate::CancellationToken;

fn make_result(
    path: impl Into<String>,
    status: FileOperationStatus,
    target_path: Option<String>,
    reason: Option<String>,
    message: Option<String>,
) -> FileOperationResult {
    FileOperationResult {
        path: path.into(),
        status,
        target_path,
        reason,
        message,
    }
}

fn linked_path(path: &str, target: Option<String>) -> FileOperationResult {
    make_result(
        path,
        FileOperationStatus::Failed,
        target,
        Some("linked_path".into()),
        Some("圖片路徑不可包含符號連結或 junction。".into()),
    )
}

fn canceled(path: &str, target: Option<String>) -> FileOperationResult {
    make_result(
        path,
        FileOperationStatus::Canceled,
        target,
        Some("canceled".into()),
        None,
    )
}

fn append_canceled<'a>(
    items: &mut Vec<FileOperationResult>,
    paths: impl IntoIterator<Item = &'a String>,
) {
    items.extend(paths.into_iter().map(|path| canceled(path, None)));
}

fn existing_directory_files(dir: &Path) -> Vec<String> {
    let Ok(read) = fs::read_dir(dir) else {
        return Vec::new();
    };
    read.filter_map(|e| e.ok())
        .map(|e| e.path().to_string_lossy().replace('\\', "/"))
        .collect()
}

pub fn trash_paths(paths: &[String]) -> FileOperationBatchResult {
    trash_paths_cancellable(paths, &CancellationToken::new())
}

pub fn trash_paths_cancellable(
    paths: &[String],
    cancellation: &CancellationToken,
) -> FileOperationBatchResult {
    let mut items = Vec::new();
    for (index, path) in paths.iter().enumerate() {
        if cancellation.is_canceled() {
            append_canceled(&mut items, &paths[index..]);
            break;
        }
        if has_link_or_junction_component(path) {
            items.push(linked_path(path, None));
            continue;
        }
        match move_to_trash(path) {
            Ok(()) => items.push(make_result(
                path,
                FileOperationStatus::Trashed,
                None,
                None,
                None,
            )),
            Err(PlatformError::Message(msg)) => items.push(make_result(
                path,
                FileOperationStatus::Failed,
                None,
                Some("trash_failed".into()),
                Some(msg),
            )),
        }
    }
    FileOperationBatchResult { items }
}

pub fn rename_image(source_path: &str, new_file_name: &str) -> FileOperationResult {
    rename_image_cancellable(source_path, new_file_name, &CancellationToken::new())
}

pub fn rename_image_cancellable(
    source_path: &str,
    new_file_name: &str,
    cancellation: &CancellationToken,
) -> FileOperationResult {
    if cancellation.is_canceled() {
        return canceled(source_path, None);
    }
    if has_link_or_junction_component(source_path) {
        return linked_path(source_path, None);
    }
    let validation = validate_image_file_name(new_file_name);
    if !validation.is_valid {
        let message = if validation.reason.as_deref() == Some("unsupported_extension") {
            "檔名必須使用支援的圖片副檔名。"
        } else {
            "檔名必須是不含路徑分隔符號的單一檔名。"
        };
        return make_result(
            source_path,
            FileOperationStatus::Failed,
            None,
            Some("invalid_request".into()),
            Some(message.into()),
        );
    }

    let source = Path::new(source_path);
    let parent = source.parent().unwrap_or_else(|| Path::new("."));
    let target = parent.join(new_file_name);
    let target_str = target.to_string_lossy().replace('\\', "/");

    if path_equals(source_path, &target_str) {
        return make_result(
            source_path,
            FileOperationStatus::Skipped,
            Some(target_str),
            Some("same_name".into()),
            None,
        );
    }

    let existing = existing_directory_files(parent);
    if target_name_exists(&existing, &target_str, source_path) {
        return make_result(
            source_path,
            FileOperationStatus::Failed,
            Some(target_str),
            Some("target_exists".into()),
            Some("目標檔名已存在。".into()),
        );
    }

    if cancellation.is_canceled() {
        return canceled(source_path, Some(target_str));
    }

    match fs::rename(source, &target) {
        Ok(()) => make_result(
            source_path,
            FileOperationStatus::Renamed,
            Some(target_str),
            None,
            None,
        ),
        Err(err) => make_result(
            source_path,
            FileOperationStatus::Failed,
            Some(target_str),
            Some("rename_failed".into()),
            Some(err.to_string()),
        ),
    }
}

pub fn convert_to_jpg(paths: &[String]) -> FileOperationBatchResult {
    convert_to_jpg_cancellable(paths, &CancellationToken::new())
}

pub fn convert_to_jpg_cancellable(
    paths: &[String],
    cancellation: &CancellationToken,
) -> FileOperationBatchResult {
    convert_paths(paths, ImageFormat::Jpeg, "jpg", 100, cancellation)
}

pub fn convert_to_lossless_webp(paths: &[String]) -> FileOperationBatchResult {
    convert_to_lossless_webp_cancellable(paths, &CancellationToken::new())
}

pub fn convert_to_lossless_webp_cancellable(
    paths: &[String],
    cancellation: &CancellationToken,
) -> FileOperationBatchResult {
    let mut items = Vec::new();
    for (index, path) in paths.iter().enumerate() {
        if cancellation.is_canceled() {
            append_canceled(&mut items, &paths[index..]);
            break;
        }
        if has_link_or_junction_component(path) {
            items.push(linked_path(path, None));
            continue;
        }
        let Some(ext) = supported_image_extension(path) else {
            items.push(make_result(
                path,
                FileOperationStatus::Skipped,
                None,
                Some("unsupported".into()),
                None,
            ));
            continue;
        };
        if is_jpg_extension(&ext) || is_webp_extension(&ext) {
            items.push(make_result(
                path,
                FileOperationStatus::Skipped,
                None,
                Some("skip_format".into()),
                None,
            ));
            continue;
        }
        if crate::animation::is_animated(Path::new(path)) {
            items.push(make_result(
                path,
                FileOperationStatus::Skipped,
                None,
                Some("animated".into()),
                None,
            ));
            continue;
        }
        items.push(convert_one(
            path,
            ImageFormat::WebP,
            "webp",
            100,
            cancellation,
        ));
    }
    FileOperationBatchResult { items }
}

fn convert_paths(
    paths: &[String],
    format: ImageFormat,
    extension: &str,
    quality: u8,
    cancellation: &CancellationToken,
) -> FileOperationBatchResult {
    let mut items = Vec::new();
    for (index, path) in paths.iter().enumerate() {
        if cancellation.is_canceled() {
            append_canceled(&mut items, &paths[index..]);
            break;
        }
        if has_link_or_junction_component(path) {
            items.push(linked_path(path, None));
            continue;
        }
        if supported_image_extension(path).is_none() {
            items.push(make_result(
                path,
                FileOperationStatus::Skipped,
                None,
                Some("unsupported".into()),
                None,
            ));
            continue;
        }
        items.push(convert_one(path, format, extension, quality, cancellation));
    }
    FileOperationBatchResult { items }
}

fn convert_one(
    path: &str,
    format: ImageFormat,
    extension: &str,
    _quality: u8,
    cancellation: &CancellationToken,
) -> FileOperationResult {
    let source = Path::new(path);
    let parent = source.parent().unwrap_or_else(|| Path::new("."));
    let stem = source
        .file_stem()
        .and_then(|s| s.to_str())
        .unwrap_or("image");
    let target = parent.join(format!("{stem}.{extension}"));
    let target_str = target.to_string_lossy().replace('\\', "/");

    if path_equals(path, &target_str) {
        return make_result(
            path,
            FileOperationStatus::Skipped,
            Some(target_str),
            Some("same_path".into()),
            None,
        );
    }
    let existing = existing_directory_files(parent);
    if target_name_exists(&existing, &target_str, path) || target.exists() {
        return make_result(
            path,
            FileOperationStatus::Skipped,
            Some(target_str),
            Some("target_exists".into()),
            None,
        );
    }

    if cancellation.is_canceled() {
        return canceled(path, Some(target_str));
    }

    match image::open(source) {
        Ok(img) => {
            if cancellation.is_canceled() {
                return canceled(path, Some(target_str));
            }
            // image crate: jpeg/webp encode via save_with_format
            match img.save_with_format(&target, format) {
                Ok(()) => make_result(
                    path,
                    FileOperationStatus::Converted,
                    Some(target_str),
                    None,
                    None,
                ),
                Err(err) => make_result(
                    path,
                    FileOperationStatus::Failed,
                    Some(target_str),
                    Some("encode_failed".into()),
                    Some(err.to_string()),
                ),
            }
        }
        Err(err) => make_result(
            path,
            FileOperationStatus::Failed,
            Some(target_str),
            Some("decode_failed".into()),
            Some(err.to_string()),
        ),
    }
}

/// Keep JPG/JPEG and WebP; trash other same-basename formats when either preferred form exists.
pub fn cleanup_same_basename(paths: &[String]) -> FileOperationBatchResult {
    cleanup_same_basename_cancellable(paths, &CancellationToken::new())
}

pub fn cleanup_same_basename_cancellable(
    paths: &[String],
    cancellation: &CancellationToken,
) -> FileOperationBatchResult {
    use std::collections::HashMap;
    let mut groups: HashMap<String, Vec<String>> = HashMap::new();
    for path in paths {
        if cancellation.is_canceled() {
            return FileOperationBatchResult {
                items: paths.iter().map(|path| canceled(path, None)).collect(),
            };
        }
        let p = Path::new(path);
        let parent = p
            .parent()
            .map(|x| x.to_string_lossy().replace('\\', "/"))
            .unwrap_or_default();
        let stem = p.file_stem().and_then(|s| s.to_str()).unwrap_or_default();
        let key = path_key(&format!("{parent}/{stem}"));
        groups.entry(key).or_default().push(path.clone());
    }

    let mut to_trash = Vec::new();
    for group in groups.values() {
        let has_jpg = group.iter().any(|p| {
            supported_image_extension(p)
                .map(|e| is_jpg_extension(&e))
                .unwrap_or(false)
        });
        let has_webp = group.iter().any(|p| {
            supported_image_extension(p)
                .map(|e| is_webp_extension(&e))
                .unwrap_or(false)
        });
        if !(has_jpg || has_webp) {
            continue;
        }
        for path in group {
            let ext = supported_image_extension(path).unwrap_or_default();
            if is_jpg_extension(&ext) || is_webp_extension(&ext) {
                continue;
            }
            to_trash.push(path.clone());
        }
    }
    trash_paths_cancellable(&to_trash, cancellation)
}

pub fn plan_drop_rename(source_paths: &[String], target_path: &str) -> DropTargetBatchRenamePlan {
    let parent = Path::new(target_path)
        .parent()
        .map(Path::to_path_buf)
        .unwrap_or_else(|| PathBuf::from("."));
    let existing = existing_directory_files(&parent);
    plan_drop_target_batch_rename(source_paths, target_path, &existing)
}

pub fn apply_drop_rename(plan: &DropTargetBatchRenamePlan) -> FileOperationBatchResult {
    apply_drop_rename_cancellable(plan, &CancellationToken::new())
}

pub fn apply_drop_rename_cancellable(
    plan: &DropTargetBatchRenamePlan,
    cancellation: &CancellationToken,
) -> FileOperationBatchResult {
    let mut items = Vec::new();
    for (index, item) in plan.items.iter().enumerate() {
        if cancellation.is_canceled() {
            items.extend(
                plan.items[index..]
                    .iter()
                    .map(|item| canceled(&item.source_path, Some(item.target_path.clone()))),
            );
            break;
        }
        if item.should_skip {
            items.push(make_result(
                &item.source_path,
                FileOperationStatus::Skipped,
                Some(item.target_path.clone()),
                item.reason.clone(),
                None,
            ));
            continue;
        }
        if has_link_or_junction_component(&item.source_path)
            || has_link_or_junction_component(&item.target_path)
        {
            items.push(linked_path(
                &item.source_path,
                Some(item.target_path.clone()),
            ));
            continue;
        }
        match fs::rename(&item.source_path, &item.target_path) {
            Ok(()) => items.push(make_result(
                &item.source_path,
                FileOperationStatus::Renamed,
                Some(item.target_path.clone()),
                None,
                None,
            )),
            Err(err) => items.push(make_result(
                &item.source_path,
                FileOperationStatus::Failed,
                Some(item.target_path.clone()),
                Some("rename_failed".into()),
                Some(err.to_string()),
            )),
        }
    }
    FileOperationBatchResult { items }
}

#[cfg(test)]
mod cancellation_tests {
    use super::*;

    #[test]
    fn canceled_batch_marks_every_unstarted_item() {
        let token = CancellationToken::new();
        token.cancel();
        let paths = vec!["a.jpg".into(), "b.jpg".into()];
        let batch = convert_to_jpg_cancellable(&paths, &token);
        assert_eq!(batch.canceled(), 2);
        assert!(batch
            .items
            .iter()
            .all(|item| item.reason.as_deref() == Some("canceled")));
    }

    #[test]
    fn canceled_drop_plan_keeps_source_and_target_context() {
        let token = CancellationToken::new();
        token.cancel();
        let plan = DropTargetBatchRenamePlan {
            total: 1,
            items: vec![piclens_domain::DropTargetBatchRenamePlanItem {
                source_path: "a.jpg".into(),
                target_path: "base-01.jpg".into(),
                should_skip: false,
                reason: None,
            }],
        };
        let batch = apply_drop_rename_cancellable(&plan, &token);
        assert_eq!(batch.canceled(), 1);
        assert_eq!(batch.items[0].target_path.as_deref(), Some("base-01.jpg"));
    }
}
