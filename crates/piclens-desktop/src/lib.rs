//! The egui/eframe frontend for PicLens.

pub mod app;
pub mod backend;
pub mod cli;
pub mod demo;
pub mod images;
pub mod model;
pub mod theme;
pub mod ui;

use std::path::PathBuf;
use std::time::Duration;

use eframe::egui;
use piclens_domain::{
    normalize_window_size, SortState, DEFAULT_THUMBNAIL_SIZE, MIN_WINDOW_HEIGHT, MIN_WINDOW_WIDTH,
};
use piclens_infra::JsonSettingsStore;

use crate::app::PicLensApp;

const APP_ICON_PNG: &[u8] = include_bytes!("../../../assets/Square150x150Logo.scale-200.png");

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct LaunchOptions {
    pub initial_folder: Option<PathBuf>,
    pub include_subfolders: bool,
    pub sort: SortState,
    pub thumbnail_size: i32,
    pub sidebar_collapsed: bool,
    pub smoke_after: Option<Duration>,
}

pub fn run(options: LaunchOptions) -> eframe::Result<()> {
    let stored = JsonSettingsStore::new().load();
    let (width, height) = stored
        .window_width
        .zip(stored.window_height)
        .map(|(width, height)| normalize_window_size(width, height))
        .unwrap_or((1280, 800));

    let initial_folder = options
        .initial_folder
        .or_else(|| restorable_folder(stored.last_folder_path));
    let options = LaunchOptions {
        initial_folder,
        include_subfolders: stored.include_subfolders,
        sort: stored.sort,
        thumbnail_size: stored.thumbnail_size,
        sidebar_collapsed: stored.sidebar_collapsed,
        smoke_after: options.smoke_after,
    };

    let viewport = egui::ViewportBuilder::default()
        .with_title("PicLens")
        .with_app_id("piclens")
        .with_inner_size([width as f32, height as f32])
        .with_min_inner_size([MIN_WINDOW_WIDTH as f32, MIN_WINDOW_HEIGHT as f32])
        .with_icon(app_icon());
    let native_options = eframe::NativeOptions {
        viewport,
        renderer: eframe::Renderer::Wgpu,
        // A hidden Wayland window may stop receiving compositor frame callbacks.
        // Event-driven backend wakes must still reach App::logic and smoke close.
        wgpu_options: eframe::WgpuConfiguration::default().with_surface_config(
            eframe::SurfaceConfig {
                present_mode: eframe::wgpu::PresentMode::AutoNoVsync,
                desired_maximum_frame_latency: Some(1),
            },
        ),
        centered: true,
        persist_window: false,
        ..Default::default()
    };

    eframe::run_native(
        "PicLens",
        native_options,
        Box::new(move |creation| Ok(Box::new(PicLensApp::new(creation, options)))),
    )
}

fn restorable_folder(last_folder_path: Option<String>) -> Option<PathBuf> {
    last_folder_path
        .map(PathBuf::from)
        .filter(|path| path.is_dir())
}

impl Default for LaunchOptions {
    fn default() -> Self {
        Self {
            initial_folder: None,
            include_subfolders: false,
            sort: SortState::default(),
            thumbnail_size: DEFAULT_THUMBNAIL_SIZE,
            sidebar_collapsed: false,
            smoke_after: None,
        }
    }
}

fn app_icon() -> egui::IconData {
    let image = image::load_from_memory(APP_ICON_PNG)
        .expect("bundled PicLens icon is a valid image")
        .into_rgba8();
    let (width, height) = image.dimensions();
    egui::IconData {
        rgba: image.into_raw(),
        width,
        height,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn startup_restore_uses_only_an_existing_directory() {
        let fixture =
            std::env::temp_dir().join(format!("piclens-egui-restore-{}", std::process::id()));
        let _ = std::fs::remove_dir_all(&fixture);
        std::fs::create_dir_all(&fixture).unwrap();

        assert_eq!(
            restorable_folder(Some(fixture.to_string_lossy().into_owned())),
            Some(fixture.clone())
        );
        std::fs::remove_dir_all(&fixture).unwrap();
        assert_eq!(
            restorable_folder(Some(fixture.to_string_lossy().into_owned())),
            None
        );
    }
}
