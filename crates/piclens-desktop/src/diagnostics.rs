//! Release-run performance metrics, including the viewer's 500ms paint target.

use std::fs;
use std::path::PathBuf;
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::{Arc, Mutex};
use std::thread::{self, JoinHandle};
use std::time::{Duration, Instant};

use serde_json::json;
use sysinfo::{Pid, ProcessesToUpdate, System};

const PROCESS_SAMPLE_INTERVAL: Duration = Duration::from_millis(250);
const METRICS_SCHEMA_VERSION: u8 = 4;

#[derive(Debug)]
pub struct RuntimeMetrics {
    output: PathBuf,
    started: Instant,
    system: Mutex<System>,
    state: Mutex<MetricState>,
}

#[derive(Debug, Default)]
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
    batch_operation_ms: Option<u128>,
    batch_total: usize,
    batch_succeeded: usize,
    batch_skipped: usize,
    batch_canceled: usize,
    batch_failed: usize,
    row_count: usize,
    image_count: usize,
    completed_thumbnails: usize,
    peak_working_set_bytes: u64,
    cpu_percent_total: f64,
    process_sample_count: u64,
    window_size: Option<String>,
    display_scale: Option<f32>,
}

pub struct ProcessSampler {
    stop: Arc<AtomicBool>,
    thread: Option<JoinHandle<()>>,
}

impl ProcessSampler {
    pub fn stop(&mut self) {
        self.stop.store(true, Ordering::Release);
        if let Some(thread) = self.thread.take() {
            thread.thread().unpark();
            if thread.join().is_err() {
                piclens_infra::warn("egui metrics sampler panicked during shutdown");
            }
        }
    }
}

impl Drop for ProcessSampler {
    fn drop(&mut self) {
        self.stop();
    }
}

impl RuntimeMetrics {
    pub fn new(output: impl Into<PathBuf>) -> Self {
        let mut system = System::new_all();
        let pid = Pid::from_u32(std::process::id());
        system.refresh_processes(ProcessesToUpdate::Some(&[pid]));
        Self {
            output: output.into(),
            started: Instant::now(),
            system: Mutex::new(system),
            state: Mutex::new(MetricState::default()),
        }
    }

    pub fn start_sampler(self: &Arc<Self>) -> ProcessSampler {
        let metrics = Arc::clone(self);
        let stop = Arc::new(AtomicBool::new(false));
        let thread_stop = Arc::clone(&stop);
        let thread = thread::Builder::new()
            .name("piclens-metrics".into())
            .spawn(move || {
                while !thread_stop.load(Ordering::Acquire) {
                    thread::park_timeout(PROCESS_SAMPLE_INTERVAL);
                    if thread_stop.load(Ordering::Acquire) {
                        break;
                    }
                    metrics.sample_process();
                }
            })
            .expect("PicLens metrics sampler can start");
        ProcessSampler {
            stop,
            thread: Some(thread),
        }
    }

    pub fn window_ready(&self, width: u32, height: u32, display_scale: f32) {
        let mut state = self.state.lock().unwrap_or_else(|error| error.into_inner());
        state
            .startup_ms
            .get_or_insert(self.started.elapsed().as_millis());
        state.window_size = Some(format!("{width}x{height}"));
        state.display_scale = Some(display_scale);
    }

    pub fn library_ready(&self, rows: usize, images: usize) {
        let mut state = self.state.lock().unwrap_or_else(|error| error.into_inner());
        state
            .library_ready_ms
            .get_or_insert(self.started.elapsed().as_millis());
        state.row_count = rows;
        state.image_count = images;
    }

    pub fn sample_process(&self) {
        let pid = Pid::from_u32(std::process::id());
        let sample = {
            let mut system = self
                .system
                .lock()
                .unwrap_or_else(|error| error.into_inner());
            system.refresh_processes(ProcessesToUpdate::Some(&[pid]));
            system
                .process(pid)
                .map(|process| (process.memory(), process.cpu_usage()))
        };
        let Some((memory, cpu_usage)) = sample else {
            return;
        };
        let mut state = self.state.lock().unwrap_or_else(|error| error.into_inner());
        state.peak_working_set_bytes = state.peak_working_set_bytes.max(memory);
        state.cpu_percent_total += f64::from(cpu_usage);
        state.process_sample_count += 1;
    }

    pub fn thumbnail_ready(&self) {
        let mut state = self.state.lock().unwrap_or_else(|error| error.into_inner());
        state
            .first_thumbnail_ms
            .get_or_insert(self.started.elapsed().as_millis());
        state.completed_thumbnails += 1;
    }

    pub fn viewer_opened(&self) {
        self.state
            .lock()
            .unwrap_or_else(|error| error.into_inner())
            .viewer_open_ms
            .get_or_insert(self.started.elapsed().as_millis());
    }

    pub fn search_applied(&self) {
        self.state
            .lock()
            .unwrap_or_else(|error| error.into_inner())
            .search_ms
            .get_or_insert(self.started.elapsed().as_millis());
    }

    pub fn viewer_preview_ready(&self, elapsed_ms: u128) {
        self.state
            .lock()
            .unwrap_or_else(|error| error.into_inner())
            .viewer_preview_ready_ms
            .get_or_insert(elapsed_ms);
    }

    pub fn viewer_sharp_painted(&self, elapsed_ms: u128) {
        let mut state = self.state.lock().unwrap_or_else(|error| error.into_inner());
        state.viewer_sharp_paint_ms.get_or_insert(elapsed_ms);
        state.viewer_sharp_paint_max_ms = state.viewer_sharp_paint_max_ms.max(elapsed_ms);
        state.viewer_sharp_paint_count += 1;
        state.viewer_sharp_target_misses += usize::from(elapsed_ms > 500);
        if state.viewer_sharp_paint_samples_ms.len() < 256 {
            state.viewer_sharp_paint_samples_ms.push(elapsed_ms);
        }
    }

    pub fn viewer_navigation_checked(&self, painted: bool) {
        let mut state = self.state.lock().unwrap_or_else(|error| error.into_inner());
        state.viewer_navigation_checked += 1;
        state.viewer_navigation_unpainted += usize::from(!painted);
    }

    pub fn scroll_completed(&self, elapsed_ms: u128) {
        self.state
            .lock()
            .unwrap_or_else(|error| error.into_inner())
            .scroll_ms = Some(elapsed_ms);
    }

    pub fn batch_completed(
        &self,
        elapsed_ms: u128,
        total: usize,
        succeeded: usize,
        skipped: usize,
        canceled: usize,
        failed: usize,
    ) {
        let mut state = self.state.lock().unwrap_or_else(|error| error.into_inner());
        state.batch_operation_ms = Some(elapsed_ms);
        state.batch_total = total;
        state.batch_succeeded = succeeded;
        state.batch_skipped = skipped;
        state.batch_canceled = canceled;
        state.batch_failed = failed;
    }

    pub fn write_snapshot(&self) -> Result<(), String> {
        self.sample_process();
        let (working_set_bytes, process_cpu_percent, cpu_name, logical_processor_count) = {
            let system = self
                .system
                .lock()
                .unwrap_or_else(|error| error.into_inner());
            let process = system.process(Pid::from_u32(std::process::id()));
            (
                process.map(|item| item.memory()).unwrap_or_default(),
                process.map(|item| item.cpu_usage()).unwrap_or_default(),
                system
                    .cpus()
                    .first()
                    .map(|cpu| cpu.brand().to_string())
                    .unwrap_or_else(|| "unknown".into()),
                system.cpus().len(),
            )
        };
        let state = self.state.lock().unwrap_or_else(|error| error.into_inner());
        let mut value = json!({
            "schemaVersion": METRICS_SCHEMA_VERSION,
            "frontEnd": "eframe-egui-wgpu",
            "buildProfile": if cfg!(debug_assertions) { "debug" } else { "release" },
            "version": env!("CARGO_PKG_VERSION"),
            "commit": option_env!("PICLENS_BUILD_COMMIT").unwrap_or("working-tree"),
            "os": System::long_os_version().unwrap_or_else(|| std::env::consts::OS.into()),
            "cpu": cpu_name,
            "gpu": "record externally",
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
            "processCpuPercentAtExit": process_cpu_percent,
            "averageCpuUtilizationPercent": if state.process_sample_count == 0 {
                0.0
            } else {
                state.cpu_percent_total / state.process_sample_count as f64
            },
            "workingSetBytes": working_set_bytes,
            "peakWorkingSetBytes": state.peak_working_set_bytes.max(working_set_bytes),
            "logicalProcessorCount": logical_processor_count,
            "thresholdGateEnabled": false
        });
        let object = value.as_object_mut().expect("JSON object");
        object.insert(
            "batchOperationMilliseconds".into(),
            serde_json::to_value(state.batch_operation_ms).expect("serializable metric"),
        );
        object.insert("batchTotal".into(), state.batch_total.into());
        object.insert("batchSucceeded".into(), state.batch_succeeded.into());
        object.insert("batchSkipped".into(), state.batch_skipped.into());
        object.insert("batchCanceled".into(), state.batch_canceled.into());
        object.insert("batchFailed".into(), state.batch_failed.into());
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
    fn records_workloads_and_viewer_target_misses() {
        let metrics = RuntimeMetrics::new("unused.json");
        metrics.thumbnail_ready();
        metrics.thumbnail_ready();
        metrics.search_applied();
        metrics.scroll_completed(1_980);
        metrics.batch_completed(250, 4, 2, 1, 0, 1);
        metrics.viewer_sharp_painted(120);
        metrics.viewer_sharp_painted(501);
        metrics.viewer_navigation_checked(true);
        metrics.viewer_navigation_checked(false);

        let state = metrics.state.lock().unwrap();
        assert_eq!(metrics.output(), std::path::Path::new("unused.json"));
        assert!(state.first_thumbnail_ms.is_some());
        assert_eq!(state.completed_thumbnails, 2);
        assert!(state.search_ms.is_some());
        assert_eq!(state.scroll_ms, Some(1_980));
        assert_eq!(state.batch_operation_ms, Some(250));
        assert_eq!(state.batch_total, 4);
        assert_eq!(state.batch_succeeded, 2);
        assert_eq!(state.batch_skipped, 1);
        assert_eq!(state.batch_canceled, 0);
        assert_eq!(state.batch_failed, 1);
        assert_eq!(state.viewer_sharp_paint_ms, Some(120));
        assert_eq!(state.viewer_sharp_paint_max_ms, 501);
        assert_eq!(state.viewer_sharp_target_misses, 1);
        assert_eq!(state.viewer_navigation_checked, 2);
        assert_eq!(state.viewer_navigation_unpainted, 1);
        assert_eq!(METRICS_SCHEMA_VERSION, 4);
    }
}
