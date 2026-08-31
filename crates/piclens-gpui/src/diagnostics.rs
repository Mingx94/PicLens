//! Release-run performance metrics, including the viewer's 500ms paint target.

use std::fs;
use std::path::PathBuf;
use std::sync::Mutex;
use std::time::Instant;

use serde_json::json;
use sysinfo::{Pid, System};

pub struct RuntimeMetrics {
    output: PathBuf,
    started: Instant,
    state: Mutex<MetricState>,
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn sharp_paint_counts_target_misses_and_keeps_first_and_maximum() {
        let metrics = RuntimeMetrics::new("unused.json");
        metrics.viewer_sharp_painted(120);
        metrics.viewer_sharp_painted(500);
        metrics.viewer_sharp_painted(501);
        metrics.viewer_navigation_checked(true);
        metrics.viewer_navigation_checked(false);
        let state = metrics.state.lock().unwrap();
        assert_eq!(state.viewer_sharp_paint_ms, Some(120));
        assert_eq!(state.viewer_sharp_paint_max_ms, 501);
        assert_eq!(state.viewer_sharp_paint_count, 3);
        assert_eq!(state.viewer_sharp_target_misses, 1);
        assert_eq!(state.viewer_sharp_paint_samples_ms, [120, 500, 501]);
        assert_eq!(state.viewer_navigation_checked, 2);
        assert_eq!(state.viewer_navigation_unpainted, 1);
    }
}

#[derive(Default)]
struct MetricState {
    startup_ms: Option<u128>,
    library_ready_ms: Option<u128>,
    first_thumbnail_ms: Option<u128>,
    viewer_open_ms: Option<u128>,
    viewer_preview_ready_ms: Option<u128>,
    viewer_sharp_paint_ms: Option<u128>,
    viewer_sharp_paint_max_ms: u128,
    viewer_sharp_paint_count: usize,
    viewer_sharp_target_misses: usize,
    viewer_sharp_paint_samples_ms: Vec<u128>,
    viewer_navigation_checked: usize,
    viewer_navigation_unpainted: usize,
    search_ms: Option<u128>,
    scroll_ms: Option<u128>,
    row_count: usize,
    image_count: usize,
    completed_thumbnails: usize,
    peak_working_set_bytes: u64,
    cpu_percent_total: f64,
    process_sample_count: u64,
    window_size: Option<String>,
    display_scale: Option<f32>,
}

impl RuntimeMetrics {
    pub fn new(output: impl Into<PathBuf>) -> Self {
        Self {
            output: output.into(),
            started: Instant::now(),
            state: Mutex::new(MetricState::default()),
        }
    }

    pub fn library_ready(&self, rows: usize, images: usize) {
        let mut state = self.state.lock().unwrap_or_else(|err| err.into_inner());
        state
            .library_ready_ms
            .get_or_insert(self.started.elapsed().as_millis());
        state.row_count = rows;
        state.image_count = images;
    }

    pub fn window_ready(&self, width: u32, height: u32, display_scale: f32) {
        let mut state = self.state.lock().unwrap_or_else(|err| err.into_inner());
        state
            .startup_ms
            .get_or_insert(self.started.elapsed().as_millis());
        state.window_size = Some(format!("{width}x{height}"));
        state.display_scale = Some(display_scale);
    }

    pub fn sample_process(&self) {
        let mut system = System::new_all();
        system.refresh_all();
        let Some(process) = system.process(Pid::from_u32(std::process::id())) else {
            return;
        };
        let mut state = self.state.lock().unwrap_or_else(|err| err.into_inner());
        state.peak_working_set_bytes = state.peak_working_set_bytes.max(process.memory());
        state.cpu_percent_total += f64::from(process.cpu_usage());
        state.process_sample_count += 1;
    }

    pub fn thumbnail_ready(&self) {
        let mut state = self.state.lock().unwrap_or_else(|err| err.into_inner());
        state
            .first_thumbnail_ms
            .get_or_insert(self.started.elapsed().as_millis());
        state.completed_thumbnails += 1;
    }

    pub fn viewer_opened(&self) {
        self.state
            .lock()
            .unwrap_or_else(|err| err.into_inner())
            .viewer_open_ms
            .get_or_insert(self.started.elapsed().as_millis());
    }

    pub fn search_applied(&self) {
        self.state
            .lock()
            .unwrap_or_else(|err| err.into_inner())
            .search_ms
            .get_or_insert(self.started.elapsed().as_millis());
    }

    /// Time from selection until the safe preview pixels are decoded.
    /// This excludes GPU upload and first paint.
    pub fn viewer_preview_ready(&self, elapsed_ms: u128) {
        self.state
            .lock()
            .unwrap_or_else(|err| err.into_inner())
            .viewer_preview_ready_ms
            .get_or_insert(elapsed_ms);
    }

    /// First sharp-image paint per selection, after paint_image has accepted
    /// the pixels. This is not an OS compositor/display presentation timestamp.
    pub fn viewer_sharp_painted(&self, elapsed_ms: u128) {
        let mut state = self.state.lock().unwrap_or_else(|err| err.into_inner());
        state.viewer_sharp_paint_ms.get_or_insert(elapsed_ms);
        state.viewer_sharp_paint_max_ms = state.viewer_sharp_paint_max_ms.max(elapsed_ms);
        state.viewer_sharp_paint_count += 1;
        state.viewer_sharp_target_misses += usize::from(elapsed_ms > 500);
        if state.viewer_sharp_paint_samples_ms.len() < 256 {
            state.viewer_sharp_paint_samples_ms.push(elapsed_ms);
        }
    }

    pub fn viewer_navigation_checked(&self, painted: bool) {
        let mut state = self.state.lock().unwrap_or_else(|err| err.into_inner());
        state.viewer_navigation_checked += 1;
        state.viewer_navigation_unpainted += usize::from(!painted);
    }

    pub fn scroll_completed(&self, elapsed_ms: u128) {
        self.state
            .lock()
            .unwrap_or_else(|err| err.into_inner())
            .scroll_ms = Some(elapsed_ms);
    }

    pub fn write_snapshot(&self) -> Result<(), String> {
        self.sample_process();
        let state = self.state.lock().unwrap_or_else(|err| err.into_inner());
        let mut system = System::new_all();
        system.refresh_all();
        let process = system.process(Pid::from_u32(std::process::id()));
        let working_set_bytes = process.map(|item| item.memory()).unwrap_or_default();
        let cpu_name = system
            .cpus()
            .first()
            .map(|cpu| cpu.brand().to_string())
            .unwrap_or_else(|| "unknown".into());
        let value = json!({
            "schemaVersion": 2,
            "buildProfile": if cfg!(debug_assertions) { "debug" } else { "release" },
            "version": env!("CARGO_PKG_VERSION"),
            "commit": option_env!("PICLENS_BUILD_COMMIT").unwrap_or("working-tree"),
            "gpuiRevision": "c7537bdf463a998e7ec636adff33b198891e69ed",
            "os": System::long_os_version().unwrap_or_else(|| std::env::consts::OS.into()),
            "cpu": cpu_name,
            "gpu": "not reported by GPUI",
            "storage": "record fixture storage externally",
            "windowSize": state.window_size,
            "displayScale": state.display_scale,
            "elapsedMilliseconds": self.started.elapsed().as_millis(),
            "startupMilliseconds": state.startup_ms,
            "libraryReadyMilliseconds": state.library_ready_ms,
            "firstThumbnailMilliseconds": state.first_thumbnail_ms,
            "continuousScrollMilliseconds": state.scroll_ms,
            "searchMilliseconds": state.search_ms,
            "viewerOpenMilliseconds": state.viewer_open_ms,
            "viewerPreviewReadyMilliseconds": state.viewer_preview_ready_ms,
            "viewerSharpPaintMilliseconds": state.viewer_sharp_paint_ms,
            "viewerSharpPaintMaxMilliseconds": state.viewer_sharp_paint_max_ms,
            "viewerSharpPaintCount": state.viewer_sharp_paint_count,
            "viewerSharpTargetMilliseconds": 500,
            "viewerSharpTargetMisses": state.viewer_sharp_target_misses,
            "viewerSharpPaintSamplesMilliseconds": state.viewer_sharp_paint_samples_ms,
            "viewerNavigationCheckedSelections": state.viewer_navigation_checked,
            "viewerNavigationUnpaintedSelections": state.viewer_navigation_unpainted,
            "rowCount": state.row_count,
            "imageCount": state.image_count,
            "completedThumbnailRequests": state.completed_thumbnails,
            "processCpuPercentAtExit": process.map(|item| item.cpu_usage()).unwrap_or_default(),
            "averageCpuUtilizationPercent": if state.process_sample_count == 0 { 0.0 } else { state.cpu_percent_total / state.process_sample_count as f64 },
            "workingSetBytes": working_set_bytes,
            "peakWorkingSetBytes": state.peak_working_set_bytes.max(working_set_bytes),
            "logicalProcessorCount": system.cpus().len(),
            "thresholdGateEnabled": false
        });
        if let Some(parent) = self.output.parent() {
            fs::create_dir_all(parent).map_err(|err| err.to_string())?;
        }
        fs::write(
            &self.output,
            serde_json::to_vec_pretty(&value).map_err(|err| err.to_string())?,
        )
        .map_err(|err| err.to_string())
    }
}
