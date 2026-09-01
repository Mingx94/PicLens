//! App state, frame lifecycle, event handling, and action reducer.

use std::collections::VecDeque;

use crate::backend::{Backend, Command, Event, WorkIdentity};
use crate::model::{Action, AppModel, Loadable};
use crate::{theme, ui, LaunchOptions};

struct Reducer {
    model: AppModel,
    actions: VecDeque<Action>,
    commands: VecDeque<Command>,
    generation: u64,
    next_request_id: u64,
    pending_probe: Option<WorkIdentity>,
    close_requested: bool,
}

impl Reducer {
    fn new(initial_folder: Option<std::path::PathBuf>) -> Self {
        Self {
            model: AppModel::new(initial_folder),
            actions: VecDeque::new(),
            commands: VecDeque::new(),
            generation: 0,
            next_request_id: 1,
            pending_probe: None,
            close_requested: false,
        }
    }

    fn push_action(&mut self, action: Action) {
        self.actions.push_back(action);
    }

    fn reduce_actions(&mut self) -> usize {
        let mut applied = 0;
        while let Some(action) = self.actions.pop_front() {
            applied += 1;
            match action {
                Action::ChooseFolder => self.push_action(Action::ShowNotice(
                    "資料夾選擇器會在圖庫垂直切片階段接上。".into(),
                )),
                Action::RetryBackendProbe => self.push_action(Action::StartBackendProbe),
                Action::DismissStatus => self.model.notice = None,
                Action::ShowNotice(message) => self.model.notice = Some(message),
                Action::StartBackendProbe => self.start_backend_probe(),
            }
        }
        applied
    }

    fn start_backend_probe(&mut self) {
        let identity = WorkIdentity {
            generation: self.generation,
            request_id: self.next_request_id,
        };
        self.next_request_id = self.next_request_id.wrapping_add(1).max(1);
        self.pending_probe = Some(identity);
        self.model.backend = Loadable::Loading;
        self.commands.push_back(Command::Probe { identity });
    }

    fn handle_event(&mut self, event: Event) -> bool {
        match event {
            Event::ProbeCompleted { identity, result } if self.pending_probe == Some(identity) => {
                self.pending_probe = None;
                self.model.backend = match result {
                    Ok(()) => Loadable::Ready(()),
                    Err(message) => Loadable::Failed(message),
                };
                true
            }
            Event::ProbeCompleted { .. } => false,
            Event::SmokeDeadlineElapsed => {
                self.close_requested = true;
                true
            }
        }
    }

    fn fail_command(&mut self, command: &Command, message: String) -> bool {
        match command {
            Command::Probe { identity } => self.handle_event(Event::ProbeCompleted {
                identity: *identity,
                result: Err(message),
            }),
            Command::Shutdown => false,
        }
    }
}

pub struct PicLensApp {
    reducer: Reducer,
    backend: Backend,
}

impl PicLensApp {
    pub fn new(creation: &eframe::CreationContext<'_>, options: LaunchOptions) -> Self {
        piclens_infra::info("egui application state created");
        theme::install(&creation.egui_ctx);
        let backend = Backend::spawn(creation.egui_ctx.clone(), options.smoke_after);
        let mut app = Self {
            reducer: Reducer::new(options.initial_folder),
            backend,
        };
        app.reducer.push_action(Action::StartBackendProbe);
        app.reduce_and_dispatch(&creation.egui_ctx);
        app
    }

    fn handle_events(&mut self) -> bool {
        let mut changed = false;
        for event in self.backend.poll().collect::<Vec<_>>() {
            changed |= self.reducer.handle_event(event);
        }
        changed
    }

    fn reduce_and_dispatch(&mut self, ctx: &egui::Context) {
        let mut changed = self.reducer.reduce_actions() > 0;
        while let Some(command) = self.reducer.commands.pop_front() {
            if let Err(error) = self.backend.send(command.clone()) {
                changed |= self
                    .reducer
                    .fail_command(&command, format!("背景服務無法接收工作：{error}"));
            }
        }
        if changed {
            ctx.request_repaint();
        }
    }

    fn close_if_requested(&mut self, ctx: &egui::Context) {
        if self.reducer.close_requested {
            self.reducer.close_requested = false;
            piclens_infra::info("egui main viewport close requested");
            ctx.send_viewport_cmd(egui::ViewportCommand::Close);
        }
    }
}

impl eframe::App for PicLensApp {
    fn logic(&mut self, ctx: &egui::Context, _frame: &mut eframe::Frame) {
        if self.handle_events() {
            ctx.request_repaint();
        }
        self.reduce_and_dispatch(ctx);
        self.close_if_requested(ctx);
    }

    fn ui(&mut self, ui: &mut egui::Ui, _frame: &mut eframe::Frame) {
        let mut frame_actions = Vec::new();
        ui::show(&self.reducer.model, ui, &mut frame_actions);
        self.reducer.actions.extend(frame_actions);
        self.reduce_and_dispatch(ui.ctx());
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn next_probe(reducer: &mut Reducer) -> WorkIdentity {
        match reducer.commands.pop_front().unwrap() {
            Command::Probe { identity } => identity,
            Command::Shutdown => panic!("expected probe command"),
        }
    }

    #[test]
    fn reducer_processes_actions_queued_by_actions_in_order() {
        let mut reducer = Reducer::new(None);
        reducer.push_action(Action::ChooseFolder);

        assert_eq!(reducer.reduce_actions(), 2);
        assert_eq!(
            reducer.model.notice.as_deref(),
            Some("資料夾選擇器會在圖庫垂直切片階段接上。")
        );
        assert!(reducer.actions.is_empty());
    }

    #[test]
    fn latest_request_rejects_stale_success_and_error() {
        let mut reducer = Reducer::new(None);
        reducer.push_action(Action::RetryBackendProbe);
        reducer.reduce_actions();
        let first = next_probe(&mut reducer);
        reducer.push_action(Action::RetryBackendProbe);
        reducer.reduce_actions();
        let second = next_probe(&mut reducer);

        assert!(!reducer.handle_event(Event::ProbeCompleted {
            identity: first,
            result: Ok(()),
        }));
        assert_eq!(reducer.model.backend, Loadable::Loading);
        assert!(reducer.handle_event(Event::ProbeCompleted {
            identity: second,
            result: Err("測試失敗".into()),
        }));
        assert_eq!(reducer.model.backend, Loadable::Failed("測試失敗".into()));
        assert!(!reducer.handle_event(Event::ProbeCompleted {
            identity: first,
            result: Err("過期錯誤".into()),
        }));
        assert_eq!(reducer.model.backend, Loadable::Failed("測試失敗".into()));
    }

    #[test]
    fn matching_success_clears_pending_request() {
        let mut reducer = Reducer::new(None);
        reducer.push_action(Action::StartBackendProbe);
        reducer.reduce_actions();
        let identity = next_probe(&mut reducer);

        assert!(reducer.handle_event(Event::ProbeCompleted {
            identity,
            result: Ok(()),
        }));
        assert_eq!(reducer.model.backend, Loadable::Ready(()));
        assert_eq!(reducer.pending_probe, None);
    }
}
