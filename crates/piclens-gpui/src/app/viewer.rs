//! One owned viewer request, followed by at most two adjacent decoded previews.

use std::collections::{HashMap, VecDeque};
use std::path::PathBuf;
use std::sync::Arc;

use gpui::{AppContext as _, Context, RenderImage, Task};
use image::ImageDecoder as _;
use piclens_domain::{path_equals, ImageSequenceSnapshot};
use piclens_infra::{ensure_thumbnail_with_timeout, info, thumbnail_path, warn, CancellationToken};

use super::{PicLensApp, THUMBNAIL_TIMEOUT};

const VIEWER_PREVIEW_SIZE: u32 = 1024;

#[derive(Clone)]
pub(super) struct ViewerPreview {
    cache_path: PathBuf,
    image: Arc<RenderImage>,
}

fn load_render_preview(
    path: &str,
    cached: Option<ViewerPreview>,
    cancellation: &CancellationToken,
) -> Result<ViewerPreview, String> {
    if cancellation.is_canceled() {
        return Err("viewer request canceled".into());
    }
    let expected_path = thumbnail_path(path, VIEWER_PREVIEW_SIZE);
    if let Some(cached) = cached.filter(|cached| cached.cache_path == expected_path) {
        return Ok(cached);
    }
    let worker = std::env::current_exe().map_err(|err| format!("無法啟動圖片解碼器：{err}"))?;
    let cache_path = ensure_thumbnail_with_timeout(
        path,
        VIEWER_PREVIEW_SIZE,
        &worker,
        THUMBNAIL_TIMEOUT,
        cancellation,
    )?;
    if cancellation.is_canceled() {
        return Err("viewer request canceled".into());
    }
    decode_render_preview(cache_path)
}

fn decode_render_preview(cache_path: PathBuf) -> Result<ViewerPreview, String> {
    let decoder = image::ImageReader::open(&cache_path)
        .map_err(|err| err.to_string())?
        .into_decoder()
        .map_err(|err| err.to_string())?;
    let (width, height) = decoder.dimensions();
    if width == 0 || height == 0 || width > VIEWER_PREVIEW_SIZE || height > VIEWER_PREVIEW_SIZE {
        return Err("invalid viewer preview dimensions".into());
    }
    let mut pixels = image::DynamicImage::from_decoder(decoder)
        .map_err(|err| err.to_string())?
        .into_rgba8();
    // GPUI consumes BGRA, even though image::Frame uses an RGBA buffer type.
    for pixel in pixels.pixels_mut() {
        pixel.0.swap(0, 2);
    }
    Ok(ViewerPreview {
        cache_path,
        image: Arc::new(RenderImage::new(vec![image::Frame::new(pixels)])),
    })
}

#[derive(Default)]
pub(super) struct ViewerLoader {
    next_id: u64,
    active: Option<ViewerRequest>,
    prefetch: VecDeque<String>,
    ready: HashMap<String, ViewerPreview>,
    navigation_workload: Option<Task<()>>,
}

struct ViewerRequest {
    id: u64,
    path: String,
    cancellation: CancellationToken,
    _task: Task<()>,
}

impl Drop for ViewerRequest {
    fn drop(&mut self) {
        // Dropping a GPUI task alone cannot stop synchronous decoder work.
        self.cancellation.cancel();
    }
}

impl ViewerLoader {
    pub(super) fn cancel(&mut self, cx: &mut gpui::App) {
        self.navigation_workload = None;
        self.cancel_request();
        for (_, preview) in self.ready.drain() {
            release_preview(preview, cx);
        }
    }

    pub(super) fn cancel_request(&mut self) {
        self.active = None;
        self.prefetch.clear();
    }

    fn is_loading(&self, path: &str) -> bool {
        self.active
            .as_ref()
            .is_some_and(|request| path_equals(&request.path, path))
    }

    fn finish(&mut self, id: u64) -> bool {
        if !self.active.as_ref().is_some_and(|request| request.id == id) {
            return false;
        }
        self.active = None;
        true
    }
}

fn release_preview(preview: ViewerPreview, cx: &mut gpui::App) {
    // Run after the current window update, when all windows are in App again.
    // Dropping the CPU pixels alone does not evict GPUI's GPU atlas entry.
    cx.defer(move |cx| cx.drop_image(preview.image, None));
}

fn adjacent_previews(sequence: &ImageSequenceSnapshot) -> VecDeque<String> {
    let images = &sequence.images;
    let current = sequence.current_index as usize;
    let mut paths = VecDeque::new();
    if images.len() < 2 || current >= images.len() {
        return paths;
    }
    for index in [
        (current + 1) % images.len(),
        (current + images.len() - 1) % images.len(),
    ] {
        let image = &images[index];
        if !image.is_animated
            && !path_equals(&image.path, &images[current].path)
            && !paths
                .iter()
                .any(|path: &String| path_equals(path, &image.path))
        {
            paths.push_back(image.path.clone());
        }
    }
    paths
}

impl PicLensApp {
    /// Exercise the same navigation path as the viewer controls in one window.
    /// A held selection that never paints is a failure, not a missing sample.
    pub(super) fn start_viewer_navigation_workload(&mut self, cx: &mut Context<Self>) {
        let Some(viewer) = self.viewer.as_ref() else {
            return;
        };
        let steps = viewer.sequence.images.len().min(64);
        if steps == 0 {
            return;
        }
        info("viewer navigation workload started");
        self.viewer_loader.navigation_workload = Some(cx.spawn(async move |this, cx| {
            for step in 0..=steps * 2 {
                cx.background_executor()
                    .timer(std::time::Duration::from_millis(650))
                    .await;
                let keep_running = this
                    .update(cx, |this, cx| {
                        let Some(viewer) = this.viewer.as_ref().filter(|_| !this.shutting_down)
                        else {
                            return false;
                        };
                        if let Some(metrics) = &this.metrics {
                            metrics.viewer_navigation_checked(viewer.paint_recorded.get());
                        }
                        if !viewer.paint_recorded.get() {
                            warn(format!(
                                "viewer navigation selection did not paint: index={}",
                                viewer.sequence.current_index
                            ));
                        }
                        if step < steps * 2 {
                            this.viewer_step(if step < steps { 1 } else { -1 }, cx);
                        }
                        true
                    })
                    .unwrap_or(false);
                if !keep_running {
                    return;
                }
            }
            info("viewer navigation workload completed");
        }));
    }

    pub(super) fn load_viewer_display(&mut self, path: String, cx: &mut Context<Self>) {
        if self.shutting_down || self.viewer.is_none() {
            return;
        }
        let neighbors = adjacent_previews(&self.viewer.as_ref().unwrap().sequence);
        self.viewer_loader.ready.retain(|source, preview| {
            let keep = path_equals(source, &path)
                || neighbors
                    .iter()
                    .any(|neighbor| path_equals(source, neighbor));
            if !keep {
                release_preview(preview.clone(), cx);
            }
            keep
        });
        self.viewer_loader.prefetch.clear();
        if self.viewer_loader.is_loading(&path) {
            // A navigation step can promote the in-flight prefetch. Its result
            // will be applied to the current image without restarting decode.
            return;
        }
        self.viewer_loader.cancel_request();
        self.start_viewer_request(path, cx);
    }

    fn start_viewer_request(&mut self, path: String, cx: &mut Context<Self>) {
        self.viewer_loader.next_id = self.viewer_loader.next_id.wrapping_add(1);
        let id = self.viewer_loader.next_id;
        let cancellation = CancellationToken::new();
        let worker_cancellation = cancellation.clone();
        let worker_path = path.clone();
        let cached = self.viewer_loader.ready.get(&path).cloned();
        let task = cx.spawn(async move |this, cx| {
            let result = cx
                .background_spawn(async move {
                    load_render_preview(&worker_path, cached, &worker_cancellation)
                })
                .await;
            let _ = this.update(cx, |this, cx| this.finish_viewer_request(id, result, cx));
        });
        // Keep viewer work outside async_tasks, whose size-based trimming can
        // otherwise discard a request that is still decoding.
        self.viewer_loader.active = Some(ViewerRequest {
            id,
            path,
            cancellation,
            _task: task,
        });
    }

    fn finish_viewer_request(
        &mut self,
        id: u64,
        result: Result<ViewerPreview, String>,
        cx: &mut Context<Self>,
    ) {
        if self.shutting_down {
            return;
        }
        let Some(request) = self.viewer_loader.active.as_ref() else {
            return;
        };
        let path = request.path.clone();
        // Check identity before clearing anything. A late result from an old
        // request must not clear or overwrite a newer request, even for A-B-A.
        if !self.viewer_loader.finish(id) {
            return;
        }
        let Some(viewer) = self.viewer.as_mut() else {
            return;
        };
        let is_current = viewer
            .sequence
            .images
            .get(viewer.sequence.current_index as usize)
            .is_some_and(|image| path_equals(&image.path, &path));
        if let Ok(preview) = &result {
            if let Some(old) = self
                .viewer_loader
                .ready
                .insert(path.clone(), preview.clone())
            {
                if !Arc::ptr_eq(&old.image, &preview.image) {
                    release_preview(old, cx);
                }
            }
        }
        if is_current {
            match result {
                Ok(preview) => {
                    viewer.display_path = None;
                    viewer.display_image = Some(preview.image);
                    viewer.message = None;
                    self.viewer_loader.prefetch = adjacent_previews(&viewer.sequence);
                    let elapsed = viewer.load_started.elapsed().as_millis();
                    info(format!(
                        "viewer decoded preview ready in {elapsed} ms: {path}"
                    ));
                    if let Some(metrics) = &self.metrics {
                        metrics.viewer_preview_ready(elapsed);
                    }
                }
                Err(err) => {
                    viewer.display_path = None;
                    viewer.display_image = None;
                    viewer.message = Some(format!("無法載入圖片：{err}"));
                    warn(format!("viewer decode failed for {path}: {err}"));
                }
            }
            cx.notify();
        } else {
            match result {
                Ok(_) => info(format!("viewer prefetch ready: {path}")),
                Err(err) => warn(format!("viewer prefetch failed for {path}: {err}")),
            }
        }
        if let Some(path) = self.viewer_loader.prefetch.pop_front() {
            self.start_viewer_request(path, cx);
        }
    }
}

#[cfg(test)]
mod tests {
    use std::cell::{Cell, RefCell};
    use std::rc::Rc;
    use std::sync::Arc;
    use std::time::{Instant, SystemTime, UNIX_EPOCH};

    use gpui::{AppContext, Entity, TestAppContext, VisualTestContext};
    use gpui_component::Root;
    use piclens_domain::{reset_zoom_state, ImageListItem, ListItem, SortState};
    use piclens_infra::JsonSettingsStore;

    use super::*;
    use crate::app::{LaunchOptions, ViewerState};

    fn sequence(paths: &[(&str, bool)], current_index: i32) -> ImageSequenceSnapshot {
        ImageSequenceSnapshot {
            source_folder_path: "/fixture".into(),
            include_subfolders: false,
            sort: SortState::default(),
            current_index,
            images: paths
                .iter()
                .map(|(path, animated)| ImageListItem {
                    path: (*path).into(),
                    name: (*path).into(),
                    extension: "png".into(),
                    modified_at_ms: None,
                    size_bytes: 0,
                    is_animated: *animated,
                })
                .collect(),
        }
    }

    fn install_request(loader: &mut ViewerLoader, path: &str) -> (u64, CancellationToken) {
        loader.next_id += 1;
        let id = loader.next_id;
        let cancellation = CancellationToken::new();
        loader.active = Some(ViewerRequest {
            id,
            path: path.into(),
            cancellation: cancellation.clone(),
            _task: Task::ready(()),
        });
        (id, cancellation)
    }

    fn preview(path: &str) -> ViewerPreview {
        ViewerPreview {
            cache_path: path.into(),
            image: Arc::new(RenderImage::new(vec![image::Frame::new(
                image::RgbaImage::from_pixel(1, 1, image::Rgba([3, 2, 1, 255])),
            )])),
        }
    }

    #[test]
    fn decoded_preview_preserves_color_and_alpha_and_rejects_oversized_cache() {
        let fixture = std::env::temp_dir().join(format!(
            "piclens-viewer-pixels-{}-{}.png",
            std::process::id(),
            SystemTime::now()
                .duration_since(UNIX_EPOCH)
                .unwrap()
                .as_nanos()
        ));
        image::RgbaImage::from_pixel(2, 1, image::Rgba([11, 22, 33, 128]))
            .save(&fixture)
            .unwrap();
        let decoded = decode_render_preview(fixture.clone()).unwrap();
        assert_eq!(
            decoded.image.as_bytes(0).unwrap(),
            &[33, 22, 11, 128, 33, 22, 11, 128]
        );
        image::RgbaImage::new(VIEWER_PREVIEW_SIZE + 1, 1)
            .save(&fixture)
            .unwrap();
        assert!(decode_render_preview(fixture.clone()).is_err());
        std::fs::remove_file(fixture).unwrap();
    }

    #[test]
    fn memory_preview_reuses_pixels_but_invalidates_when_source_changes() {
        init_test_profile();
        let source = std::env::temp_dir().join(format!(
            "piclens-viewer-source-{}-{}.png",
            std::process::id(),
            SystemTime::now()
                .duration_since(UNIX_EPOCH)
                .unwrap()
                .as_nanos()
        ));
        std::fs::write(&source, b"first").unwrap();
        let path = source.to_str().unwrap();
        let mut cached = preview("unused.png");
        cached.cache_path = thumbnail_path(path, VIEWER_PREVIEW_SIZE);
        let result =
            load_render_preview(path, Some(cached.clone()), &CancellationToken::new()).unwrap();
        assert!(Arc::ptr_eq(&cached.image, &result.image));
        std::fs::write(&source, b"changed length").unwrap();
        let changed_cache = thumbnail_path(path, VIEWER_PREVIEW_SIZE);
        assert_ne!(cached.cache_path, changed_cache);
        std::fs::create_dir_all(changed_cache.parent().unwrap()).unwrap();
        image::RgbaImage::from_pixel(1, 1, image::Rgba([44, 55, 66, 255]))
            .save(&changed_cache)
            .unwrap();
        let changed =
            load_render_preview(path, Some(cached.clone()), &CancellationToken::new()).unwrap();
        assert!(!Arc::ptr_eq(&cached.image, &changed.image));
        assert_eq!(changed.image.as_bytes(0).unwrap(), &[66, 55, 44, 255]);
        let cancellation = CancellationToken::new();
        cancellation.cancel();
        assert!(load_render_preview(path, Some(cached), &cancellation).is_err());
        std::fs::remove_file(changed_cache).unwrap();
        std::fs::remove_file(source).unwrap();
    }

    #[test]
    fn prefetch_uses_only_immediate_static_neighbors_and_wraps() {
        let snapshot = sequence(
            &[
                ("a.png", false),
                ("b.gif", true),
                ("c.png", false),
                ("d.png", false),
            ],
            0,
        );
        assert_eq!(adjacent_previews(&snapshot), ["d.png"]);
        let snapshot = sequence(&[("a.png", false), ("b.png", false), ("c.png", false)], 2);
        assert_eq!(adjacent_previews(&snapshot), ["a.png", "b.png"]);
    }

    #[test]
    fn prefetch_deduplicates_small_sequences_and_never_queues_current_image() {
        assert!(adjacent_previews(&sequence(&[], -1)).is_empty());
        assert!(adjacent_previews(&sequence(&[("a.png", false)], 0)).is_empty());
        assert!(adjacent_previews(&sequence(&[("a.png", false), ("a.png", false)], 0)).is_empty());
        assert_eq!(
            adjacent_previews(&sequence(&[("a.png", false), ("b.png", false)], 0)),
            ["b.png"]
        );
    }

    #[test]
    fn late_results_cannot_clear_new_requests_even_when_path_is_reopened() {
        let mut loader = ViewerLoader::default();
        let (old_id, old_token) = install_request(&mut loader, "a.png");
        loader.prefetch.push_back("b.png".into());
        loader.cancel_request();
        assert!(old_token.is_canceled());
        assert!(loader.prefetch.is_empty());
        let (new_id, new_token) = install_request(&mut loader, "a.png");
        assert!(!loader.finish(old_id));
        assert!(loader.is_loading("a.png"));
        assert!(!new_token.is_canceled());
        assert!(loader.finish(new_id));
        assert!(loader.active.is_none());
    }

    fn init_test_profile() {
        static TEST_PROFILE: std::sync::Once = std::sync::Once::new();
        TEST_PROFILE.call_once(|| {
            std::env::set_var(
                "PICLENS_DATA_ROOT",
                std::env::temp_dir().join(format!("piclens-viewer-profile-{}", std::process::id())),
            );
        });
    }

    fn test_app(cx: &mut TestAppContext) -> (Entity<PicLensApp>, &mut VisualTestContext) {
        init_test_profile();
        cx.update(|cx| {
            gpui_component::init(cx);
            crate::theme::init(cx);
            crate::actions::init(cx);
        });
        let settings_path = std::env::temp_dir().join(format!(
            "piclens-viewer-test-{}-{}.json",
            std::process::id(),
            SystemTime::now()
                .duration_since(UNIX_EPOCH)
                .unwrap()
                .as_nanos()
        ));
        let holder = Rc::new(RefCell::new(None));
        let window_holder = holder.clone();
        let (_, cx) = cx.add_window_view(move |window, cx| {
            let app = cx.new(|cx| {
                PicLensApp::new_with_settings_store(
                    window,
                    cx,
                    None,
                    LaunchOptions::default(),
                    Arc::new(JsonSettingsStore::with_path(settings_path)),
                )
            });
            *window_holder.borrow_mut() = Some(app.clone());
            Root::new(app, window, cx)
        });
        let app = holder.borrow_mut().take().unwrap();
        (app, cx)
    }

    fn set_viewer(app: &mut PicLensApp, snapshot: ImageSequenceSnapshot) {
        app.viewer = Some(ViewerState {
            sequence: snapshot,
            zoom: reset_zoom_state(),
            message: None,
            display_path: None,
            display_image: None,
            load_started: Instant::now(),
            paint_recorded: Rc::new(Cell::new(false)),
        });
    }

    #[gpui::test]
    fn sharp_preview_paints_and_close_evicts_its_gpu_texture(cx: &mut TestAppContext) {
        let (app, cx) = test_app(cx);
        let decoded = preview("a.png");
        app.update(cx, |app, cx| {
            set_viewer(app, sequence(&[("a.png", false)], 0));
            app.viewer.as_mut().unwrap().display_image = Some(decoded.image.clone());
            app.viewer_loader
                .ready
                .insert("a.png".into(), decoded.clone());
            cx.notify();
        });
        cx.run_until_parked();
        cx.update(|window, cx| {
            _ = window.draw(cx);
            assert!(window.has_image_atlas_entry(&decoded.image));
            app.update(cx, |app, cx| {
                assert!(app.viewer.as_ref().unwrap().paint_recorded.get());
                app.cancel_async_work();
                assert!(app.viewer_loader.ready.contains_key("a.png"));
                app.close_viewer(window, cx);
            });
        });
        cx.run_until_parked();
        cx.update(|window, _| {
            assert!(!window.has_image_atlas_entry(&decoded.image));
        });
    }

    #[gpui::test]
    fn navigation_retains_only_current_and_neighbors_and_close_clears_pixels(
        cx: &mut TestAppContext,
    ) {
        let (app, cx) = test_app(cx);
        app.update(cx, |app, cx| {
            set_viewer(
                app,
                sequence(
                    &[
                        ("a.png", false),
                        ("b.png", false),
                        ("c.png", false),
                        ("d.png", false),
                    ],
                    0,
                ),
            );
            for path in ["a.png", "b.png", "c.png", "d.png"] {
                app.viewer_loader.ready.insert(path.into(), preview(path));
            }
            install_request(&mut app.viewer_loader, "b.png");
            app.viewer_step(1, cx);
            assert_eq!(app.viewer_loader.ready.len(), 3);
            assert!(!app.viewer_loader.ready.contains_key("d.png"));
            app.viewer_loader.cancel(cx);
            assert!(app.viewer_loader.ready.is_empty());
        });
    }

    #[gpui::test]
    fn navigation_promotes_inflight_prefetch_and_rejects_stale_results(cx: &mut TestAppContext) {
        let (app, cx) = test_app(cx);
        app.update(cx, |app, cx| {
            set_viewer(
                app,
                sequence(&[("a.png", false), ("b.png", false), ("c.gif", true)], 0),
            );
            let (old_id, _) = install_request(&mut app.viewer_loader, "b.png");
            app.viewer_loader.cancel(cx);
            let (id, token) = install_request(&mut app.viewer_loader, "b.png");
            app.viewer_loader.prefetch.push_back("c.png".into());
            app.viewer_step(1, cx);
            assert_eq!(app.viewer_loader.active.as_ref().unwrap().id, id);
            assert!(!token.is_canceled());
            assert!(app.viewer_loader.prefetch.is_empty());
            app.finish_viewer_request(old_id, Ok(preview("old.png")), cx);
            assert!(app.viewer.as_ref().unwrap().display_path.is_none());
            assert!(app.viewer.as_ref().unwrap().display_image.is_none());
            assert!(!token.is_canceled());
            let decoded = preview("b-preview.png");
            app.finish_viewer_request(id, Ok(decoded.clone()), cx);
            assert!(Arc::ptr_eq(
                app.viewer.as_ref().unwrap().display_image.as_ref().unwrap(),
                &decoded.image
            ));
            assert!(app.viewer_loader.is_loading("a.png"));
            assert!(app.viewer_loader.prefetch.is_empty());
            app.viewer_loader.cancel(cx); // No real decoder is launched by this test.
        });
    }

    #[gpui::test]
    fn arrow_navigation_paints_cached_pixels_on_every_step(cx: &mut TestAppContext) {
        let (app, cx) = test_app(cx);
        let paths = ["a.png", "b.png", "c.png"];
        let previews = paths.map(|path| {
            let mut decoded = preview(path);
            decoded.cache_path = thumbnail_path(path, VIEWER_PREVIEW_SIZE);
            decoded
        });
        cx.update(|window, cx| {
            app.update(cx, |app, cx| {
                set_viewer(
                    app,
                    sequence(&[("a.png", false), ("b.png", false), ("c.png", false)], 0),
                );
                app.viewer.as_mut().unwrap().display_image = Some(previews[0].image.clone());
                for (path, decoded) in paths.iter().zip(&previews) {
                    app.viewer_loader
                        .ready
                        .insert((*path).into(), decoded.clone());
                }
                app.viewer_focus.focus(window, cx);
                cx.notify();
            });
            _ = window.draw(cx);
        });
        for (key, index) in [
            ("right", 1),
            ("left", 0),
            ("right", 1),
            ("right", 2),
            ("right", 0),
            ("left", 2),
        ] {
            cx.simulate_keystrokes(key);
            cx.run_until_parked();
            cx.update(|window, cx| {
                _ = window.draw(cx);
            });
            app.read_with(cx, |app, _| {
                let viewer = app.viewer.as_ref().unwrap();
                assert_eq!(viewer.sequence.current_index as usize, index);
                assert!(viewer.paint_recorded.get());
                assert!(viewer.display_path.is_none());
                assert!(Arc::ptr_eq(
                    viewer.display_image.as_ref().unwrap(),
                    &previews[index].image
                ));
            });
        }
        app.update(cx, |app, cx| app.viewer_loader.cancel(cx));
    }

    #[gpui::test]
    fn animation_close_and_shutdown_cancel_viewer_work(cx: &mut TestAppContext) {
        let (app, cx) = test_app(cx);
        let (close_id, close_token) = app.update(cx, |app, cx| {
            set_viewer(app, sequence(&[("a.png", false), ("b.gif", true)], 0));
            let (_, token) = install_request(&mut app.viewer_loader, "a.png");
            app.viewer_step(1, cx);
            assert!(token.is_canceled());
            assert!(app.viewer.as_ref().unwrap().message.is_some());
            assert!(app.viewer_loader.active.is_none());
            install_request(&mut app.viewer_loader, "a.png")
        });
        cx.update(|window, cx| {
            app.update(cx, |app, cx| {
                app.close_viewer(window, cx);
                assert!(close_token.is_canceled());
                assert!(app.viewer.is_none());
                assert!(app.thumbs_pump_scheduled);
                app.finish_viewer_request(close_id, Ok(preview("late.png")), cx);
                assert!(app.viewer.is_none());
                let (_, token) = install_request(&mut app.viewer_loader, "a.png");
                app.prepare_shutdown(cx);
                assert!(token.is_canceled());
                assert!(app.viewer_loader.active.is_none());
            })
        });
    }

    #[gpui::test]
    fn open_viewer_releases_gallery_slots_and_pauses_new_thumbnails(cx: &mut TestAppContext) {
        let (app, cx) = test_app(cx);
        cx.update(|window, cx| {
            app.update(cx, |app, cx| {
                let snapshot = sequence(&[("a.png", false)], 0);
                app.visible = snapshot.images.into_iter().map(ListItem::Image).collect();
                let token = CancellationToken::new();
                app.thumb_cancellations
                    .insert("a.png".into(), token.clone());
                app.open_viewer("a.png", window, cx);
                assert!(token.is_canceled());
                assert!(app.viewer_loader.is_loading("a.png"));
                app.pump_thumbs(cx);
                assert!(app.thumb_pending.is_empty());
                app.viewer_loader.cancel(cx);
                app.visible.clear();
                app.prepare_shutdown(cx);
            })
        });
    }

    #[gpui::test]
    fn prefetch_failure_does_not_replace_current_image_and_current_error_is_reported(
        cx: &mut TestAppContext,
    ) {
        let (app, cx) = test_app(cx);
        app.update(cx, |app, cx| {
            set_viewer(app, sequence(&[("a.png", false), ("b.png", false)], 0));
            app.viewer.as_mut().unwrap().display_path = Some("a-preview.png".into());
            let (prefetch_id, _) = install_request(&mut app.viewer_loader, "b.png");
            app.finish_viewer_request(prefetch_id, Err("invalid image".into()), cx);
            assert_eq!(
                app.viewer.as_ref().unwrap().display_path,
                Some("a-preview.png".into())
            );
            assert!(app.viewer.as_ref().unwrap().message.is_none());
            let (current_id, _) = install_request(&mut app.viewer_loader, "a.png");
            app.finish_viewer_request(current_id, Err("invalid image".into()), cx);
            assert!(app.viewer.as_ref().unwrap().display_path.is_none());
            assert!(app
                .viewer
                .as_ref()
                .unwrap()
                .message
                .as_ref()
                .unwrap()
                .contains("invalid image"));
            assert!(app.viewer_loader.active.is_none());
        });
    }
}
