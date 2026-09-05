use std::time::Duration;

#[test]
fn original_worker_preserves_pixels_and_reports_decode_errors() {
    let root = std::env::temp_dir().join(format!(
        "piclens-original-fixture-{}-{}",
        std::process::id(),
        std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .unwrap()
            .as_nanos()
    ));
    std::fs::create_dir_all(&root).unwrap();
    // Deliberately misnamed: the worker must detect PNG from its contents.
    let source = root.join("source.jpg");
    let fixture = image::RgbaImage::from_fn(3073, 9, |x, y| {
        image::Rgba([(x % 251) as u8, y as u8, 217, (x % 256) as u8])
    });
    fixture
        .save_with_format(&source, image::ImageFormat::Png)
        .unwrap();
    let before = std::fs::read(&source).unwrap();
    let executable = std::path::Path::new(env!("CARGO_BIN_EXE_piclens-desktop"));
    let token = piclens_infra::CancellationToken::new();
    let (width, height, rgba) = piclens_infra::load_original_with_timeout(
        &source.to_string_lossy(),
        executable,
        Duration::from_secs(10),
        &token,
    )
    .unwrap();
    assert_eq!((width, height), fixture.dimensions());
    assert_eq!(rgba, fixture.into_raw());
    assert_eq!(std::fs::read(&source).unwrap(), before);

    std::fs::write(&source, b"invalid image").unwrap();
    assert!(piclens_infra::load_original_with_timeout(
        &source.to_string_lossy(),
        executable,
        Duration::from_secs(10),
        &token,
    )
    .is_err());
    token.cancel();
    assert!(piclens_infra::load_original_with_timeout(
        &source.to_string_lossy(),
        executable,
        Duration::from_secs(10),
        &token,
    )
    .unwrap_err()
    .contains("canceled"));
    assert!(std::fs::read_dir(piclens_infra::thumbnail_cache_root())
        .unwrap()
        .all(|entry| entry
            .unwrap()
            .path()
            .extension()
            .is_none_or(|ext| ext != "rgba")));
    std::fs::remove_dir_all(root).unwrap();
}
