//! Release-run viewer paint metrics for the egui frontend.

use std::fs;
use std::path::PathBuf;
use std::time::Instant;

use serde_json::json;

pub struct RuntimeMetrics {
    output: PathBuf,
    started: Instant,
    state: MetricState,
}

#[derive(Default)]
struct MetricState {
    startup_ms: Option<u128>,
    library_ready_ms: Option<u128>,
    viewer_open_ms: Option<u128>,
    viewer_preview_ready_ms: Option<u128>,
    viewer_sharp_paint_ms: Option<u128>,
    viewer_sharp_paint_max_ms: u128,
    viewer_sharp_paint_count: usize,
    viewer_sharp_target_misses: usize,
    viewer_sharp_paint_samples_ms: Vec<u128>,
    viewer_navigation_checked: usize,
    viewer_navigation_unpainted: usize,
    row_count: usize,
    image_count: usize,
    window_size: Option<String>,
    display_scale: Option<f32>,
}

impl RuntimeMetrics {
    pub fn new(output: impl Into<PathBuf>) -> Self {
        Self {
            output: output.into(),
            started: Instant::now(),
            state: MetricState::default(),
        }
    }

    pub fn window_ready(&mut self, width: u32, height: u32, display_scale: f32) {
        self.state
            .startup_ms
            .get_or_insert(self.started.elapsed().as_millis());
        self.state.window_size = Some(format!("{width}x{height}"));
        self.state.display_scale = Some(display_scale);
    }

    pub fn library_ready(&mut self, rows: usize, images: usize) {
        self.state
            .library_ready_ms
            .get_or_insert(self.started.elapsed().as_millis());
        self.state.row_count = rows;
        self.state.image_count = images;
    }

    pub fn viewer_opened(&mut self) {
        self.state
            .viewer_open_ms
            .get_or_insert(self.started.elapsed().as_millis());
    }

    pub fn viewer_preview_ready(&mut self, elapsed_ms: u128) {
        self.state.viewer_preview_ready_ms.get_or_insert(elapsed_ms);
    }

    pub fn viewer_sharp_painted(&mut self, elapsed_ms: u128) {
        self.state.viewer_sharp_paint_ms.get_or_insert(elapsed_ms);
        self.state.viewer_sharp_paint_max_ms = self.state.viewer_sharp_paint_max_ms.max(elapsed_ms);
        self.state.viewer_sharp_paint_count += 1;
        self.state.viewer_sharp_target_misses += usize::from(elapsed_ms > 500);
        if self.state.viewer_sharp_paint_samples_ms.len() < 256 {
            self.state.viewer_sharp_paint_samples_ms.push(elapsed_ms);
        }
    }

    pub fn viewer_navigation_checked(&mut self, painted: bool) {
        self.state.viewer_navigation_checked += 1;
        self.state.viewer_navigation_unpainted += usize::from(!painted);
    }

    pub fn write_snapshot(&self) -> Result<(), String> {
        let value = json!({
            "schemaVersion": 2,
            "frontEnd": "eframe-egui-wgpu",
            "buildProfile": if cfg!(debug_assertions) { "debug" } else { "release" },
            "version": env!("CARGO_PKG_VERSION"),
            "commit": option_env!("PICLENS_BUILD_COMMIT").unwrap_or("working-tree"),
            "os": std::env::consts::OS,
            "gpu": "record externally",
            "storage": "record fixture storage externally",
            "windowSize": self.state.window_size,
            "displayScale": self.state.display_scale,
            "elapsedMilliseconds": self.started.elapsed().as_millis(),
            "startupMilliseconds": self.state.startup_ms,
            "libraryReadyMilliseconds": self.state.library_ready_ms,
            "viewerOpenMilliseconds": self.state.viewer_open_ms,
            "viewerPreviewReadyMilliseconds": self.state.viewer_preview_ready_ms,
            "viewerSharpPaintMilliseconds": self.state.viewer_sharp_paint_ms,
            "viewerSharpPaintMaxMilliseconds": self.state.viewer_sharp_paint_max_ms,
            "viewerSharpPaintCount": self.state.viewer_sharp_paint_count,
            "viewerSharpTargetMilliseconds": 500,
            "viewerSharpTargetMisses": self.state.viewer_sharp_target_misses,
            "viewerSharpPaintSamplesMilliseconds": self.state.viewer_sharp_paint_samples_ms,
            "viewerNavigationCheckedSelections": self.state.viewer_navigation_checked,
            "viewerNavigationUnpaintedSelections": self.state.viewer_navigation_unpainted,
            "rowCount": self.state.row_count,
            "imageCount": self.state.image_count,
            "thresholdGateEnabled": false
        });
        if let Some(parent) = self
            .output
            .parent()
            .filter(|parent| !parent.as_os_str().is_empty())
        {
            fs::create_dir_all(parent).map_err(|error| error.to_string())?;
        }
        fs::write(
            &self.output,
            serde_json::to_vec_pretty(&value).map_err(|error| error.to_string())?,
        )
        .map_err(|error| error.to_string())
    }

    #[cfg(test)]
    fn output(&self) -> &std::path::Path {
        &self.output
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn sharp_paint_counts_misses_and_unpainted_selections() {
        let mut metrics = RuntimeMetrics::new("unused.json");
        metrics.viewer_sharp_painted(120);
        metrics.viewer_sharp_painted(501);
        metrics.viewer_navigation_checked(true);
        metrics.viewer_navigation_checked(false);

        assert_eq!(metrics.output(), std::path::Path::new("unused.json"));
        assert_eq!(metrics.state.viewer_sharp_paint_ms, Some(120));
        assert_eq!(metrics.state.viewer_sharp_paint_max_ms, 501);
        assert_eq!(metrics.state.viewer_sharp_target_misses, 1);
        assert_eq!(metrics.state.viewer_navigation_checked, 2);
        assert_eq!(metrics.state.viewer_navigation_unpainted, 1);
    }
}
