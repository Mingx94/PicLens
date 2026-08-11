const SUPPORTED: &[&str] = &["jpg", "jpeg", "png", "bmp", "webp", "gif"];

/// Returns the lower-case extension when the path is a supported image.
pub fn supported_image_extension(file_path: &str) -> Option<String> {
    let normalized = file_path.replace('\\', "/");
    let extension = std::path::Path::new(&normalized)
        .extension()
        .and_then(|ext| ext.to_str())
        .map(|ext| ext.to_ascii_lowercase())?;
    if SUPPORTED.contains(&extension.as_str()) {
        Some(extension)
    } else {
        None
    }
}

pub fn is_jpg_extension(extension: &str) -> bool {
    let ext = extension.to_ascii_lowercase();
    ext == "jpg" || ext == "jpeg"
}

pub fn is_webp_extension(extension: &str) -> bool {
    extension.eq_ignore_ascii_case("webp")
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn accepts_supported_extensions() {
        assert_eq!(
            supported_image_extension(r"C:\photos\a.JPG").as_deref(),
            Some("jpg")
        );
        assert_eq!(supported_image_extension("x.webp").as_deref(), Some("webp"));
        assert!(supported_image_extension("a.txt").is_none());
    }
}
