//! Bounded channel boundary between the UI thread and background work.

use std::collections::{HashMap, HashSet, VecDeque};
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::mpsc::{self, Receiver, SyncSender};
use std::sync::{Arc, Condvar, Mutex};
use std::thread::{self, JoinHandle};
use std::time::{Duration, Instant};

use piclens_domain::{AppSettingsPatch, ListItem, ListQuery, SortState};
use piclens_infra::{
    ensure_thumbnail_with_timeout, prune_thumbnail_cache_if_needed, scan_child_folders_cancellable,
    scan_folder_cancellable, CancellationToken, JsonSettingsStore, ScanError,
};

use crate::images::{
    decode_cached_thumbnail, DecodedThumbnail, ThumbnailRequest, ThumbnailRequestIdentity,
};

const SMOKE_CLOSE_GRACE: Duration = Duration::from_secs(2);
const COMMAND_QUEUE_CAPACITY: usize = 64;
const EVENT_QUEUE_CAPACITY: usize = 256;
const COORDINATOR_WORKERS: usize = 1;
const LIBRARY_SCAN_WORKERS: usize = 1;
const FOLDER_PICKER_WORKERS: usize = 1;
const FOLDER_PICKER_QUEUE_CAPACITY: usize = 1;
const TREE_SCAN_WORKERS: usize = 1;
const TREE_QUEUE_CAPACITY: usize = 32;
const THUMBNAIL_WORKERS: usize = 8;
const THUMBNAIL_QUEUE_CAPACITY: usize = 256;
const THUMBNAIL_TIMEOUT: Duration = Duration::from_secs(15);
const THUMBNAIL_CACHE_PRUNE_INTERVAL: Duration = Duration::from_secs(5);

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub struct WorkIdentity {
    pub generation: u64,
    pub request_id: u64,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum Command {
    Probe {
        identity: WorkIdentity,
    },
    LoadLibrary {
        identity: WorkIdentity,
        query: ListQuery,
    },
    LoadTreeChildren {
        identity: WorkIdentity,
        parent: String,
    },
    PersistLibrarySettings {
        include_subfolders: bool,
        sort: SortState,
        thumbnail_size: i32,
    },
    PersistPickerFolder {
        path: String,
    },
    PersistSidebar {
        collapsed: bool,
    },
    SyncThumbnails {
        requests: Vec<ThumbnailRequest>,
    },
    Reveal {
        path: String,
    },
    Shutdown,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum Event {
    ProbeCompleted {
        identity: WorkIdentity,
        result: Result<(), String>,
    },
    LibraryLoaded {
        identity: WorkIdentity,
        query: ListQuery,
        result: Result<Vec<ListItem>, String>,
    },
    TreeChildrenLoaded {
        identity: WorkIdentity,
        parent: String,
        result: Result<Vec<String>, String>,
    },
    SettingsSaved {
        result: Result<(), String>,
    },
    FolderPicked {
        path: Option<std::path::PathBuf>,
    },
    ThumbnailLoaded {
        request: ThumbnailRequest,
        result: Result<DecodedThumbnail, String>,
    },
    RevealCompleted {
        result: Result<(), String>,
    },
    SmokeDeadlineElapsed,
}

struct ActiveScan {
    cancellation: CancellationToken,
    thread: JoinHandle<()>,
}

struct TreeJob {
    identity: WorkIdentity,
    parent: String,
    cancellation: CancellationToken,
}

struct TreeWorker {
    jobs: SyncSender<Option<TreeJob>>,
    thread: Option<JoinHandle<()>>,
    active: Arc<Mutex<HashMap<WorkIdentity, CancellationToken>>>,
    events: SyncSender<Event>,
    ctx: egui::Context,
}

impl TreeWorker {
    fn spawn(events: SyncSender<Event>, ctx: egui::Context) -> Self {
        debug_assert_eq!(TREE_SCAN_WORKERS, 1);
        let (jobs, receiver) = mpsc::sync_channel::<Option<TreeJob>>(TREE_QUEUE_CAPACITY);
        let worker_events = events.clone();
        let worker_ctx = ctx.clone();
        let active = Arc::new(Mutex::new(HashMap::new()));
        let worker_active = Arc::clone(&active);
        let thread = thread::Builder::new()
            .name("piclens-tree-scan".into())
            .spawn(move || {
                while let Ok(Some(job)) = receiver.recv() {
                    let result = scan_child_folders_cancellable(&job.parent, &job.cancellation)
                        .map(|folders| folders.into_iter().map(|folder| folder.path).collect())
                        .map_err(|error| format!("無法載入資料夾樹：{error}"));
                    worker_active
                        .lock()
                        .unwrap_or_else(|error| error.into_inner())
                        .remove(&job.identity);
                    if !job.cancellation.is_canceled() {
                        if let Err(error) = &result {
                            piclens_infra::warn(format!(
                                "egui folder tree scan failed; parent={}; error={error}",
                                job.parent
                            ));
                        }
                    }
                    if !send_event(
                        &worker_events,
                        &worker_ctx,
                        Event::TreeChildrenLoaded {
                            identity: job.identity,
                            parent: job.parent,
                            result,
                        },
                    ) {
                        return;
                    }
                }
            })
            .expect("PicLens tree scan worker can start");
        Self {
            jobs,
            thread: Some(thread),
            active,
            events,
            ctx,
        }
    }

    fn load(&self, job: TreeJob) {
        cancel_tree_jobs(&self.active, Some(job.identity.generation));
        self.active
            .lock()
            .unwrap_or_else(|error| error.into_inner())
            .insert(job.identity, job.cancellation.clone());
        match self.jobs.try_send(Some(job)) {
            Ok(()) => {}
            Err(mpsc::TrySendError::Full(Some(job))) => {
                self.active
                    .lock()
                    .unwrap_or_else(|error| error.into_inner())
                    .remove(&job.identity);
                let _ = send_event(
                    &self.events,
                    &self.ctx,
                    Event::TreeChildrenLoaded {
                        identity: job.identity,
                        parent: job.parent,
                        result: Err("資料夾樹工作佇列已滿。".into()),
                    },
                );
            }
            Err(mpsc::TrySendError::Disconnected(Some(job))) => {
                self.active
                    .lock()
                    .unwrap_or_else(|error| error.into_inner())
                    .remove(&job.identity);
                let _ = send_event(
                    &self.events,
                    &self.ctx,
                    Event::TreeChildrenLoaded {
                        identity: job.identity,
                        parent: job.parent,
                        result: Err("資料夾樹背景服務已停止。".into()),
                    },
                );
            }
            Err(mpsc::TrySendError::Full(None) | mpsc::TrySendError::Disconnected(None)) => {}
        }
    }

    fn shutdown(&mut self) {
        cancel_tree_jobs(&self.active, None);
        let _ = self.jobs.send(None);
        if self
            .thread
            .take()
            .is_some_and(|thread| thread.join().is_err())
        {
            piclens_infra::warn("egui tree scan worker panicked during shutdown");
        }
    }
}

fn cancel_tree_jobs(
    active: &Mutex<HashMap<WorkIdentity, CancellationToken>>,
    keep_generation: Option<u64>,
) {
    active
        .lock()
        .unwrap_or_else(|error| error.into_inner())
        .retain(|identity, cancellation| {
            let keep = keep_generation == Some(identity.generation);
            if !keep {
                cancellation.cancel();
            }
            keep
        });
}

struct FolderPickerWorker {
    dialogs: SyncSender<Option<rfd::FileDialog>>,
    active: Arc<AtomicBool>,
    thread: Option<JoinHandle<()>>,
}

impl FolderPickerWorker {
    fn spawn(events: SyncSender<Event>, ctx: egui::Context) -> Self {
        debug_assert_eq!(FOLDER_PICKER_WORKERS, 1);
        let (dialogs, receiver) =
            mpsc::sync_channel::<Option<rfd::FileDialog>>(FOLDER_PICKER_QUEUE_CAPACITY);
        let active = Arc::new(AtomicBool::new(false));
        let worker_active = Arc::clone(&active);
        let thread = thread::Builder::new()
            .name("piclens-folder-picker".into())
            .spawn(move || {
                while let Ok(Some(dialog)) = receiver.recv() {
                    let path = dialog.pick_folder();
                    worker_active.store(false, Ordering::Release);
                    if !send_event(&events, &ctx, Event::FolderPicked { path }) {
                        return;
                    }
                }
            })
            .expect("PicLens folder picker worker can start");
        Self {
            dialogs,
            active,
            thread: Some(thread),
        }
    }

    fn choose(&self, dialog: rfd::FileDialog) -> Result<(), &'static str> {
        if self
            .active
            .compare_exchange(false, true, Ordering::AcqRel, Ordering::Acquire)
            .is_err()
        {
            return Err("資料夾選擇器已開啟。");
        }
        if self.dialogs.try_send(Some(dialog)).is_err() {
            self.active.store(false, Ordering::Release);
            return Err("資料夾選擇器背景服務忙碌中。");
        }
        Ok(())
    }

    fn shutdown(&mut self) {
        let _ = self.dialogs.send(None);
        if self
            .thread
            .take()
            .is_some_and(|thread| thread.join().is_err())
        {
            piclens_infra::warn("egui folder picker worker panicked during shutdown");
        }
    }
}

struct ThumbnailJob {
    request: ThumbnailRequest,
    cancellation: CancellationToken,
}

#[derive(Default)]
struct ThumbnailQueue {
    jobs: VecDeque<ThumbnailJob>,
    shutdown: bool,
}

struct ThumbnailPool {
    queue: Arc<(Mutex<ThumbnailQueue>, Condvar)>,
    active: Arc<Mutex<HashMap<ThumbnailRequestIdentity, CancellationToken>>>,
    threads: Vec<JoinHandle<()>>,
    events: SyncSender<Event>,
    ctx: egui::Context,
    cache_shutdown: Arc<(Mutex<bool>, Condvar)>,
    cache_thread: Option<JoinHandle<()>>,
}

impl ThumbnailPool {
    fn spawn(events: SyncSender<Event>, ctx: egui::Context) -> Self {
        let queue = Arc::new((Mutex::new(ThumbnailQueue::default()), Condvar::new()));
        let active = Arc::new(Mutex::new(HashMap::new()));
        let worker_executable =
            std::env::current_exe().unwrap_or_else(|_| std::path::PathBuf::from("piclens-desktop"));
        let mut threads = Vec::with_capacity(THUMBNAIL_WORKERS);
        for index in 0..THUMBNAIL_WORKERS {
            let worker_queue = Arc::clone(&queue);
            let worker_events = events.clone();
            let worker_ctx = ctx.clone();
            let worker_executable = worker_executable.clone();
            threads.push(
                thread::Builder::new()
                    .name(format!("piclens-thumbnail-{index}"))
                    .spawn(move || {
                        thumbnail_worker_loop(
                            worker_queue,
                            worker_events,
                            worker_ctx,
                            worker_executable,
                        )
                    })
                    .expect("PicLens thumbnail worker can start"),
            );
        }
        let cache_shutdown = Arc::new((Mutex::new(false), Condvar::new()));
        let cache_shutdown_for_thread = Arc::clone(&cache_shutdown);
        let cache_thread = thread::Builder::new()
            .name("piclens-thumbnail-cache".into())
            .spawn(move || thumbnail_cache_loop(cache_shutdown_for_thread))
            .expect("PicLens thumbnail cache owner can start");
        Self {
            queue,
            active,
            threads,
            events,
            ctx,
            cache_shutdown,
            cache_thread: Some(cache_thread),
        }
    }

    fn sync(&self, requests: Vec<ThumbnailRequest>) {
        let desired = requests
            .iter()
            .map(|request| request.identity)
            .collect::<HashSet<_>>();
        {
            let mut active = self
                .active
                .lock()
                .unwrap_or_else(|error| error.into_inner());
            active.retain(|identity, cancellation| {
                let keep = desired.contains(identity);
                if !keep {
                    cancellation.cancel();
                }
                keep
            });
        }

        let (queue, wake) = &*self.queue;
        let mut queue = queue.lock().unwrap_or_else(|error| error.into_inner());
        queue
            .jobs
            .retain(|job| desired.contains(&job.request.identity));
        let mut active = self
            .active
            .lock()
            .unwrap_or_else(|error| error.into_inner());
        let mut rejected = Vec::new();
        for request in requests {
            if active.contains_key(&request.identity) {
                continue;
            }
            if queue.jobs.len() >= THUMBNAIL_QUEUE_CAPACITY {
                rejected.push(request);
                continue;
            }
            let cancellation = CancellationToken::new();
            active.insert(request.identity, cancellation.clone());
            queue.jobs.push_back(ThumbnailJob {
                request,
                cancellation,
            });
        }
        drop(active);
        drop(queue);
        wake.notify_all();
        for request in rejected {
            let _ = send_event(
                &self.events,
                &self.ctx,
                Event::ThumbnailLoaded {
                    request,
                    result: Err("thumbnail queue is full".into()),
                },
            );
        }
    }

    fn shutdown(&mut self) {
        {
            let mut active = self
                .active
                .lock()
                .unwrap_or_else(|error| error.into_inner());
            for cancellation in active.values() {
                cancellation.cancel();
            }
            active.clear();
        }
        let (queue, wake) = &*self.queue;
        {
            let mut queue = queue.lock().unwrap_or_else(|error| error.into_inner());
            queue.shutdown = true;
            queue.jobs.clear();
        }
        wake.notify_all();
        for thread in self.threads.drain(..) {
            if thread.join().is_err() {
                piclens_infra::warn("egui thumbnail worker panicked during shutdown");
            }
        }
        let (shutdown, wake) = &*self.cache_shutdown;
        *shutdown.lock().unwrap_or_else(|error| error.into_inner()) = true;
        wake.notify_all();
        if self
            .cache_thread
            .take()
            .is_some_and(|thread| thread.join().is_err())
        {
            piclens_infra::warn("egui thumbnail cache owner panicked during shutdown");
        }
    }
}

fn thumbnail_cache_loop(shutdown: Arc<(Mutex<bool>, Condvar)>) {
    let (shutdown, wake) = &*shutdown;
    loop {
        let stopped = shutdown.lock().unwrap_or_else(|error| error.into_inner());
        let (stopped, _) = wake
            .wait_timeout_while(stopped, THUMBNAIL_CACHE_PRUNE_INTERVAL, |stopped| !*stopped)
            .unwrap_or_else(|error| error.into_inner());
        if *stopped {
            return;
        }
        drop(stopped);
        prune_thumbnail_cache_if_needed();
    }
}

fn thumbnail_worker_loop(
    queue: Arc<(Mutex<ThumbnailQueue>, Condvar)>,
    events: SyncSender<Event>,
    ctx: egui::Context,
    worker_executable: std::path::PathBuf,
) {
    loop {
        let job = {
            let (queue, wake) = &*queue;
            let mut queue = queue.lock().unwrap_or_else(|error| error.into_inner());
            while queue.jobs.is_empty() && !queue.shutdown {
                queue = wake.wait(queue).unwrap_or_else(|error| error.into_inner());
            }
            if queue.shutdown {
                return;
            }
            queue.jobs.pop_front()
        };
        let Some(job) = job else {
            continue;
        };
        let result = load_thumbnail(&job, &worker_executable);
        if let Err(error) = &result {
            if !error.contains("canceled") {
                piclens_infra::warn(format!(
                    "egui thumbnail failed; source={}; size={}; error={error}",
                    job.request.key.source.display(),
                    job.request.key.longest_edge
                ));
            }
        }
        if !send_event(
            &events,
            &ctx,
            Event::ThumbnailLoaded {
                request: job.request,
                result,
            },
        ) {
            return;
        }
    }
}

fn load_thumbnail(
    job: &ThumbnailJob,
    worker_executable: &std::path::Path,
) -> Result<DecodedThumbnail, String> {
    if !job.request.key.source_matches_disk() {
        return Err("thumbnail source changed since the library scan".into());
    }
    let source = job.request.key.source.to_string_lossy();
    let cache_path = ensure_thumbnail_with_timeout(
        &source,
        job.request.key.longest_edge,
        worker_executable,
        THUMBNAIL_TIMEOUT,
        &job.cancellation,
    )?;
    if !job.request.key.source_matches_disk() {
        return Err("thumbnail source changed during decode".into());
    }
    decode_cached_thumbnail(&cache_path)
}

pub struct Backend {
    commands: SyncSender<Command>,
    events: Receiver<Event>,
    thread: Option<JoinHandle<()>>,
    folder_picker: FolderPickerWorker,
}

impl Backend {
    pub fn spawn(ctx: egui::Context, smoke_after: Option<Duration>) -> Self {
        debug_assert_eq!(COORDINATOR_WORKERS, 1);
        debug_assert_eq!(LIBRARY_SCAN_WORKERS, 1);
        let (command_tx, command_rx) = mpsc::sync_channel(COMMAND_QUEUE_CAPACITY);
        let (event_tx, event_rx) = mpsc::sync_channel(EVENT_QUEUE_CAPACITY);
        let folder_picker = FolderPickerWorker::spawn(event_tx.clone(), ctx.clone());
        let thread = thread::Builder::new()
            .name("piclens-backend".into())
            .spawn(move || coordinator_loop(command_rx, event_tx, ctx, smoke_after))
            .expect("PicLens backend coordinator can start");
        Self {
            commands: command_tx,
            events: event_rx,
            thread: Some(thread),
            folder_picker,
        }
    }

    pub fn send(&self, command: Command) -> Result<(), mpsc::TrySendError<Command>> {
        self.commands.try_send(command)
    }

    pub fn poll(&self) -> impl Iterator<Item = Event> + '_ {
        self.events.try_iter()
    }

    pub fn choose_folder(&self, dialog: rfd::FileDialog) -> Result<(), &'static str> {
        self.folder_picker.choose(dialog)
    }

    #[cfg(test)]
    fn recv_timeout(&self, timeout: Duration) -> Result<Event, mpsc::RecvTimeoutError> {
        self.events.recv_timeout(timeout)
    }
}

impl Drop for Backend {
    fn drop(&mut self) {
        let (_replacement_tx, replacement_rx) = mpsc::sync_channel(0);
        drop(std::mem::replace(&mut self.events, replacement_rx));
        let _ = self.commands.send(Command::Shutdown);
        if let Some(thread) = self.thread.take() {
            let _ = thread.join();
        }
        self.folder_picker.shutdown();
    }
}

fn send_event(events: &SyncSender<Event>, ctx: &egui::Context, event: Event) -> bool {
    if events.send(event).is_err() {
        return false;
    }
    ctx.request_repaint();
    true
}

fn stop_active_scan(active_scan: &mut Option<ActiveScan>) {
    let Some(scan) = active_scan.take() else {
        return;
    };
    scan.cancellation.cancel();
    if scan.thread.join().is_err() {
        piclens_infra::warn("egui library scan worker panicked during shutdown");
    }
}

fn reap_finished_scan(active_scan: &mut Option<ActiveScan>) {
    if active_scan
        .as_ref()
        .is_some_and(|scan| scan.thread.is_finished())
    {
        stop_active_scan(active_scan);
    }
}

fn start_library_scan(
    identity: WorkIdentity,
    query: ListQuery,
    events: SyncSender<Event>,
    ctx: egui::Context,
) -> Result<ActiveScan, String> {
    let cancellation = CancellationToken::new();
    let worker_cancellation = cancellation.clone();
    let thread = thread::Builder::new()
        .name("piclens-library-scan".into())
        .spawn(move || {
            let result = scan_folder_cancellable(&query, &worker_cancellation);
            match &result {
                Ok(items) => piclens_infra::info(format!(
                    "egui library scan completed; folder={}; items={}",
                    query.folder_path,
                    items.len()
                )),
                Err(ScanError::Canceled) => piclens_infra::info(format!(
                    "egui library scan canceled; folder={}",
                    query.folder_path
                )),
                Err(error) => piclens_infra::warn(format!(
                    "egui library scan failed; folder={}; error={error}",
                    query.folder_path
                )),
            }
            let result = result.map_err(|error| format!("無法載入資料夾：{error}"));
            let _ = send_event(
                &events,
                &ctx,
                Event::LibraryLoaded {
                    identity,
                    query,
                    result,
                },
            );
        })
        .map_err(|error| format!("無法啟動資料夾掃描：{error}"))?;
    Ok(ActiveScan {
        cancellation,
        thread,
    })
}

fn persist_library_settings(
    store: &JsonSettingsStore,
    include_subfolders: bool,
    sort: SortState,
    thumbnail_size: i32,
) -> Result<(), String> {
    store
        .update(&AppSettingsPatch {
            sort: Some(sort),
            include_subfolders: Some(include_subfolders),
            thumbnail_size: Some(thumbnail_size),
            ..Default::default()
        })
        .map(|_| ())
        .map_err(|error| format!("無法儲存圖庫設定：{error}"))
}

fn persist_picker_folder(store: &JsonSettingsStore, path: String) -> Result<(), String> {
    store
        .update(&AppSettingsPatch {
            last_folder_path: Some(Some(path)),
            ..Default::default()
        })
        .map(|_| ())
        .map_err(|error| format!("無法儲存資料夾設定：{error}"))
}

fn persist_sidebar(store: &JsonSettingsStore, collapsed: bool) -> Result<(), String> {
    store
        .update(&AppSettingsPatch {
            sidebar_collapsed: Some(collapsed),
            ..Default::default()
        })
        .map(|_| ())
        .map_err(|error| format!("無法儲存側欄設定：{error}"))
}

fn coordinator_loop(
    commands: Receiver<Command>,
    events: SyncSender<Event>,
    ctx: egui::Context,
    smoke_after: Option<Duration>,
) {
    #[derive(Clone, Copy)]
    enum SmokeState {
        WaitingForDeadline(Instant),
        WaitingForCloseFallback(Instant),
    }

    let mut active_scan = None;
    let mut tree_worker = TreeWorker::spawn(events.clone(), ctx.clone());
    let mut thumbnail_pool = ThumbnailPool::spawn(events.clone(), ctx.clone());
    let mut smoke_state =
        smoke_after.map(|duration| SmokeState::WaitingForDeadline(Instant::now() + duration));
    loop {
        reap_finished_scan(&mut active_scan);
        let command = match smoke_state {
            Some(SmokeState::WaitingForDeadline(deadline))
            | Some(SmokeState::WaitingForCloseFallback(deadline)) => {
                match commands.recv_timeout(deadline.saturating_duration_since(Instant::now())) {
                    Ok(command) => command,
                    Err(mpsc::RecvTimeoutError::Timeout) => match smoke_state {
                        Some(SmokeState::WaitingForDeadline(_)) => {
                            piclens_infra::info(
                                "egui smoke deadline elapsed; requesting close frame",
                            );
                            ctx.send_viewport_cmd_to(
                                egui::ViewportId::ROOT,
                                egui::ViewportCommand::Close,
                            );
                            if !send_event(&events, &ctx, Event::SmokeDeadlineElapsed) {
                                break;
                            }
                            smoke_state = Some(SmokeState::WaitingForCloseFallback(
                                Instant::now() + SMOKE_CLOSE_GRACE,
                            ));
                            continue;
                        }
                        Some(SmokeState::WaitingForCloseFallback(_)) => {
                            piclens_infra::warn(
                                "egui smoke close was not processed; using headless fallback exit",
                            );
                            std::process::exit(0);
                        }
                        None => continue,
                    },
                    Err(mpsc::RecvTimeoutError::Disconnected) => break,
                }
            }
            None => match commands.recv() {
                Ok(command) => command,
                Err(_) => break,
            },
        };

        match command {
            Command::Probe { identity } => {
                if !send_event(
                    &events,
                    &ctx,
                    Event::ProbeCompleted {
                        identity,
                        result: Ok(()),
                    },
                ) {
                    break;
                }
            }
            Command::LoadLibrary { identity, query } => {
                stop_active_scan(&mut active_scan);
                match start_library_scan(identity, query.clone(), events.clone(), ctx.clone()) {
                    Ok(scan) => active_scan = Some(scan),
                    Err(message) => {
                        if !send_event(
                            &events,
                            &ctx,
                            Event::LibraryLoaded {
                                identity,
                                query,
                                result: Err(message),
                            },
                        ) {
                            break;
                        }
                    }
                }
            }
            Command::LoadTreeChildren { identity, parent } => {
                tree_worker.load(TreeJob {
                    identity,
                    parent,
                    cancellation: CancellationToken::new(),
                });
            }
            Command::PersistLibrarySettings {
                include_subfolders,
                sort,
                thumbnail_size,
            } => {
                let result = persist_library_settings(
                    &JsonSettingsStore::new(),
                    include_subfolders,
                    sort,
                    thumbnail_size,
                );
                if !send_event(&events, &ctx, Event::SettingsSaved { result }) {
                    break;
                }
            }
            Command::PersistPickerFolder { path } => {
                let result = persist_picker_folder(&JsonSettingsStore::new(), path);
                if !send_event(&events, &ctx, Event::SettingsSaved { result }) {
                    break;
                }
            }
            Command::PersistSidebar { collapsed } => {
                let result = persist_sidebar(&JsonSettingsStore::new(), collapsed);
                if !send_event(&events, &ctx, Event::SettingsSaved { result }) {
                    break;
                }
            }
            Command::SyncThumbnails { requests } => thumbnail_pool.sync(requests),
            Command::Reveal { path } => {
                let result = piclens_infra::reveal_in_file_manager(&path)
                    .map_err(|error| format!("無法在檔案總管中顯示圖片：{error}"));
                if !send_event(&events, &ctx, Event::RevealCompleted { result }) {
                    break;
                }
            }
            Command::Shutdown => break,
        }
    }
    stop_active_scan(&mut active_scan);
    tree_worker.shutdown();
    thumbnail_pool.shutdown();
}

#[cfg(test)]
mod tests {
    use super::*;
    use piclens_domain::{AppSettings, SortDirection};

    fn identity(request_id: u64) -> WorkIdentity {
        WorkIdentity {
            generation: 1,
            request_id,
        }
    }

    #[test]
    fn command_returns_matching_event_identity() {
        let backend = Backend::spawn(egui::Context::default(), None);
        backend
            .send(Command::Probe {
                identity: identity(42),
            })
            .unwrap();
        assert_eq!(
            backend.recv_timeout(Duration::from_secs(1)).unwrap(),
            Event::ProbeCompleted {
                identity: identity(42),
                result: Ok(())
            }
        );
    }

    #[test]
    fn library_scan_runs_behind_command_boundary() {
        let fixture =
            std::env::temp_dir().join(format!("piclens-egui-scan-{}", std::process::id()));
        let _ = std::fs::remove_dir_all(&fixture);
        std::fs::create_dir_all(&fixture).unwrap();
        std::fs::write(fixture.join("image.png"), b"not decoded by scan").unwrap();
        std::fs::write(fixture.join("ignored.txt"), b"ignored").unwrap();
        let query = ListQuery {
            folder_path: fixture.to_string_lossy().into_owned(),
            include_subfolders: false,
            sort: SortState::default(),
        };
        let backend = Backend::spawn(egui::Context::default(), None);
        backend
            .send(Command::LoadLibrary {
                identity: identity(9),
                query: query.clone(),
            })
            .unwrap();

        let Event::LibraryLoaded {
            identity: returned_identity,
            query: returned_query,
            result,
        } = backend.recv_timeout(Duration::from_secs(2)).unwrap()
        else {
            panic!("expected library event")
        };
        assert_eq!(returned_identity, identity(9));
        assert_eq!(returned_query, query);
        let items = result.unwrap();
        assert_eq!(items.len(), 1);
        assert_eq!(items[0].name(), "image.png");
        drop(backend);
        std::fs::remove_dir_all(fixture).unwrap();
    }

    #[test]
    fn tree_scan_runs_on_its_bounded_worker() {
        let fixture =
            std::env::temp_dir().join(format!("piclens-egui-tree-{}", std::process::id()));
        let _ = std::fs::remove_dir_all(&fixture);
        std::fs::create_dir_all(fixture.join("child")).unwrap();
        std::fs::write(fixture.join("file.txt"), b"ignored").unwrap();
        let parent = fixture.to_string_lossy().into_owned();
        let backend = Backend::spawn(egui::Context::default(), None);
        backend
            .send(Command::LoadTreeChildren {
                identity: identity(10),
                parent: parent.clone(),
            })
            .unwrap();

        let Event::TreeChildrenLoaded {
            identity: returned_identity,
            parent: returned_parent,
            result,
        } = backend.recv_timeout(Duration::from_secs(2)).unwrap()
        else {
            panic!("expected tree event")
        };
        assert_eq!(returned_identity, identity(10));
        assert_eq!(returned_parent, parent);
        let children = result.unwrap();
        assert_eq!(children.len(), 1);
        assert!(piclens_domain::path_equals(
            &children[0],
            &fixture.join("child").to_string_lossy()
        ));
        drop(backend);
        std::fs::remove_dir_all(fixture).unwrap();
    }

    #[test]
    fn smoke_deadline_wakes_backend_without_busy_polling() {
        let backend = Backend::spawn(egui::Context::default(), Some(Duration::from_millis(10)));
        assert_eq!(
            backend.recv_timeout(Duration::from_secs(1)).unwrap(),
            Event::SmokeDeadlineElapsed
        );
    }

    #[test]
    fn drop_stops_and_joins_backend_owner() {
        let backend = Backend::spawn(egui::Context::default(), None);
        backend
            .send(Command::Probe {
                identity: identity(7),
            })
            .unwrap();
        assert_eq!(
            backend.recv_timeout(Duration::from_secs(1)).unwrap(),
            Event::ProbeCompleted {
                identity: identity(7),
                result: Ok(())
            }
        );
        drop(backend);
    }

    #[test]
    fn stopping_active_scan_cancels_and_joins_worker() {
        let cancellation = CancellationToken::new();
        let observed = cancellation.clone();
        let worker_token = cancellation.clone();
        let thread = thread::spawn(move || {
            while !worker_token.is_canceled() {
                thread::yield_now();
            }
        });
        let mut active = Some(ActiveScan {
            cancellation,
            thread,
        });

        stop_active_scan(&mut active);

        assert!(observed.is_canceled());
        assert!(active.is_none());
    }

    #[test]
    fn tree_generation_change_and_shutdown_cancel_owned_jobs() {
        let stale = CancellationToken::new();
        let current = CancellationToken::new();
        let active = Mutex::new(HashMap::from([
            (
                WorkIdentity {
                    generation: 1,
                    request_id: 1,
                },
                stale.clone(),
            ),
            (
                WorkIdentity {
                    generation: 2,
                    request_id: 2,
                },
                current.clone(),
            ),
        ]));

        cancel_tree_jobs(&active, Some(2));
        assert!(stale.is_canceled());
        assert!(!current.is_canceled());
        assert_eq!(active.lock().unwrap().len(), 1);

        cancel_tree_jobs(&active, None);
        assert!(current.is_canceled());
        assert!(active.lock().unwrap().is_empty());
    }

    #[test]
    fn command_queue_is_bounded_and_never_blocks_sender() {
        let (sender, _receiver) = mpsc::sync_channel(COMMAND_QUEUE_CAPACITY);
        for request_id in 0..COMMAND_QUEUE_CAPACITY {
            sender
                .try_send(Command::Probe {
                    identity: identity(request_id as u64),
                })
                .unwrap();
        }
        assert!(matches!(
            sender.try_send(Command::Shutdown),
            Err(mpsc::TrySendError::Full(Command::Shutdown))
        ));
    }

    #[test]
    fn library_settings_persist_without_changing_picker_folder() {
        let fixture =
            std::env::temp_dir().join(format!("piclens-egui-settings-{}.json", std::process::id()));
        let _ = std::fs::remove_file(&fixture);
        let store = JsonSettingsStore::with_path(&fixture);
        store
            .save(&AppSettings {
                last_folder_path: Some("C:/picker-root".into()),
                ..Default::default()
            })
            .unwrap();

        persist_library_settings(
            &store,
            true,
            SortState {
                direction: SortDirection::Desc,
                ..Default::default()
            },
            220,
        )
        .unwrap();

        let loaded = store.load();
        assert_eq!(loaded.last_folder_path.as_deref(), Some("C:/picker-root"));
        assert!(loaded.include_subfolders);
        assert_eq!(loaded.sort.direction, SortDirection::Desc);
        assert_eq!(loaded.thumbnail_size, 220);
        std::fs::remove_file(fixture).unwrap();
    }

    #[test]
    fn picker_folder_persist_does_not_change_library_settings() {
        let fixture = std::env::temp_dir().join(format!(
            "piclens-egui-picker-settings-{}.json",
            std::process::id()
        ));
        let _ = std::fs::remove_file(&fixture);
        let store = JsonSettingsStore::with_path(&fixture);
        store
            .save(&AppSettings {
                include_subfolders: true,
                thumbnail_size: 220,
                ..Default::default()
            })
            .unwrap();

        persist_picker_folder(&store, "C:/picked".into()).unwrap();

        let loaded = store.load();
        assert_eq!(loaded.last_folder_path.as_deref(), Some("C:/picked"));
        assert!(loaded.include_subfolders);
        assert_eq!(loaded.thumbnail_size, 220);
        std::fs::remove_file(fixture).unwrap();
    }

    #[test]
    fn thumbnail_sync_cancels_requests_removed_from_materialized_snapshot() {
        let (events, _event_rx) = mpsc::sync_channel(EVENT_QUEUE_CAPACITY);
        let mut pool = ThumbnailPool::spawn(events, egui::Context::default());
        let request = ThumbnailRequest {
            identity: ThumbnailRequestIdentity {
                generation: 3,
                request_id: 7,
            },
            key: crate::images::ThumbnailKey {
                source: "missing-thumbnail.png".into(),
                modified_unix_ms: None,
                file_size: 1,
                longest_edge: 160,
            },
        };
        pool.sync(vec![request.clone()]);
        let observed = pool
            .active
            .lock()
            .unwrap_or_else(|error| error.into_inner())
            .get(&request.identity)
            .unwrap()
            .clone();

        pool.sync(Vec::new());

        assert!(observed.is_canceled());
        assert!(pool
            .active
            .lock()
            .unwrap_or_else(|error| error.into_inner())
            .is_empty());
        assert!(pool
            .queue
            .0
            .lock()
            .unwrap_or_else(|error| error.into_inner())
            .jobs
            .is_empty());
        pool.shutdown();
    }
}
