//! Bounded channel boundary between the UI thread and background work.

use std::sync::mpsc::{self, Receiver, SyncSender};
use std::thread::{self, JoinHandle};
use std::time::{Duration, Instant};

use piclens_domain::{AppSettingsPatch, ListItem, ListQuery, SortState};
use piclens_infra::{scan_folder_cancellable, CancellationToken, JsonSettingsStore, ScanError};

const SMOKE_CLOSE_GRACE: Duration = Duration::from_secs(2);
const COMMAND_QUEUE_CAPACITY: usize = 64;
const EVENT_QUEUE_CAPACITY: usize = 256;
const COORDINATOR_WORKERS: usize = 1;
const LIBRARY_SCAN_WORKERS: usize = 1;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
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
    PersistLibrarySettings {
        include_subfolders: bool,
        sort: SortState,
        thumbnail_size: i32,
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
    LibrarySettingsSaved {
        result: Result<(), String>,
    },
    SmokeDeadlineElapsed,
}

struct ActiveScan {
    cancellation: CancellationToken,
    thread: JoinHandle<()>,
}

pub struct Backend {
    commands: SyncSender<Command>,
    events: Receiver<Event>,
    thread: Option<JoinHandle<()>>,
}

impl Backend {
    pub fn spawn(ctx: egui::Context, smoke_after: Option<Duration>) -> Self {
        debug_assert_eq!(COORDINATOR_WORKERS, 1);
        debug_assert_eq!(LIBRARY_SCAN_WORKERS, 1);
        let (command_tx, command_rx) = mpsc::sync_channel(COMMAND_QUEUE_CAPACITY);
        let (event_tx, event_rx) = mpsc::sync_channel(EVENT_QUEUE_CAPACITY);
        let thread = thread::Builder::new()
            .name("piclens-backend".into())
            .spawn(move || coordinator_loop(command_rx, event_tx, ctx, smoke_after))
            .expect("PicLens backend coordinator can start");
        Self {
            commands: command_tx,
            events: event_rx,
            thread: Some(thread),
        }
    }

    pub fn send(&self, command: Command) -> Result<(), mpsc::TrySendError<Command>> {
        self.commands.try_send(command)
    }

    pub fn poll(&self) -> impl Iterator<Item = Event> + '_ {
        self.events.try_iter()
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
                if !send_event(&events, &ctx, Event::LibrarySettingsSaved { result }) {
                    break;
                }
            }
            Command::Shutdown => break,
        }
    }
    stop_active_scan(&mut active_scan);
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
}
