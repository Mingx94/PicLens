//! Channel boundary between the UI thread and background work.

use std::sync::mpsc::{self, Receiver, SyncSender};
use std::thread::{self, JoinHandle};
use std::time::{Duration, Instant};

const SMOKE_CLOSE_GRACE: Duration = Duration::from_secs(2);
const COMMAND_QUEUE_CAPACITY: usize = 64;
const EVENT_QUEUE_CAPACITY: usize = 256;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct WorkIdentity {
    pub generation: u64,
    pub request_id: u64,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum Command {
    Probe { identity: WorkIdentity },
    Shutdown,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum Event {
    ProbeCompleted {
        identity: WorkIdentity,
        result: Result<(), String>,
    },
    SmokeDeadlineElapsed,
}

pub struct Backend {
    commands: SyncSender<Command>,
    events: Receiver<Event>,
    thread: Option<JoinHandle<()>>,
}

impl Backend {
    pub fn spawn(ctx: egui::Context, smoke_after: Option<Duration>) -> Self {
        let (command_tx, command_rx) = mpsc::sync_channel(COMMAND_QUEUE_CAPACITY);
        let (event_tx, event_rx) = mpsc::sync_channel(EVENT_QUEUE_CAPACITY);
        let thread = thread::Builder::new()
            .name("piclens-backend".into())
            .spawn(move || worker_loop(command_rx, event_tx, ctx, smoke_after))
            .expect("PicLens backend thread can start");
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
    fn recv_timeout(&self, timeout: std::time::Duration) -> Result<Event, mpsc::RecvTimeoutError> {
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

fn worker_loop(
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

    let mut smoke_state =
        smoke_after.map(|duration| SmokeState::WaitingForDeadline(Instant::now() + duration));
    loop {
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
                            if events.send(Event::SmokeDeadlineElapsed).is_err() {
                                break;
                            }
                            ctx.request_repaint_of(egui::ViewportId::ROOT);
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
        let event = match command {
            Command::Probe { identity } => Event::ProbeCompleted {
                identity,
                result: Ok(()),
            },
            Command::Shutdown => break,
        };
        if events.send(event).is_err() {
            break;
        }
        ctx.request_repaint();
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn command_returns_matching_event_identity() {
        let backend = Backend::spawn(egui::Context::default(), None);
        let identity = WorkIdentity {
            generation: 3,
            request_id: 42,
        };
        backend.send(Command::Probe { identity }).unwrap();
        assert_eq!(
            backend
                .recv_timeout(std::time::Duration::from_secs(1))
                .unwrap(),
            Event::ProbeCompleted {
                identity,
                result: Ok(())
            }
        );
    }

    #[test]
    fn smoke_deadline_wakes_backend_without_busy_polling() {
        let backend = Backend::spawn(
            egui::Context::default(),
            Some(std::time::Duration::from_millis(10)),
        );
        assert_eq!(
            backend
                .recv_timeout(std::time::Duration::from_secs(1))
                .unwrap(),
            Event::SmokeDeadlineElapsed
        );
    }

    #[test]
    fn drop_stops_and_joins_backend_owner() {
        let backend = Backend::spawn(egui::Context::default(), None);
        let identity = WorkIdentity {
            generation: 1,
            request_id: 7,
        };
        backend.send(Command::Probe { identity }).unwrap();
        assert_eq!(
            backend
                .recv_timeout(std::time::Duration::from_secs(1))
                .unwrap(),
            Event::ProbeCompleted {
                identity,
                result: Ok(())
            }
        );
        drop(backend);
    }

    #[test]
    fn command_queue_is_bounded_and_never_blocks_sender() {
        let (sender, _receiver) = mpsc::sync_channel(COMMAND_QUEUE_CAPACITY);
        for request_id in 0..COMMAND_QUEUE_CAPACITY {
            sender
                .try_send(Command::Probe {
                    identity: WorkIdentity {
                        generation: 0,
                        request_id: request_id as u64,
                    },
                })
                .unwrap();
        }
        assert!(matches!(
            sender.try_send(Command::Shutdown),
            Err(mpsc::TrySendError::Full(Command::Shutdown))
        ));
    }
}
