//! App state, frame lifecycle, event handling, and action reducer.

use std::mem;

use crate::backend::{Backend, Command, Event};
use crate::model::{Action, AppModel, BackendStatus};
use crate::{theme, ui, LaunchOptions};

pub struct PicLensApp {
    pub model: AppModel,
    backend: Backend,
    actions: Vec<Action>,
    next_request_id: u64,
    pending_probe: Option<u64>,
    close_requested: bool,
}

impl PicLensApp {
    pub fn new(creation: &eframe::CreationContext<'_>, options: LaunchOptions) -> Self {
        piclens_infra::info("egui application state created");
        theme::install(&creation.egui_ctx);
        let backend = Backend::spawn(creation.egui_ctx.clone(), options.smoke_after);
        let mut app = Self {
            model: AppModel::new(options.initial_folder),
            backend,
            actions: Vec::new(),
            next_request_id: 1,
            pending_probe: None,
            close_requested: false,
        };
        app.probe_backend();
        app
    }

    fn handle_events(&mut self) {
        let events = self.backend.poll().collect::<Vec<_>>();
        for event in events {
            match event {
                Event::Ready { request_id } if self.pending_probe == Some(request_id) => {
                    self.pending_probe = None;
                    self.model.backend = BackendStatus::Ready;
                }
                Event::Ready { .. } => {}
                Event::SmokeDeadlineElapsed => {
                    piclens_infra::info("egui smoke close event received");
                    self.close_requested = true;
                }
            }
        }
    }

    fn apply_actions(&mut self, ctx: &egui::Context) {
        let mut actions = mem::take(&mut self.actions);
        let mut applied = false;
        while !actions.is_empty() {
            for action in actions.drain(..) {
                applied = true;
                match action {
                    Action::ChooseFolder => {
                        self.model.notice = Some("資料夾選擇器會在圖庫垂直切片階段接上。".into());
                    }
                    Action::RetryBackendProbe => self.probe_backend(),
                    Action::DismissStatus => self.model.notice = None,
                }
            }
            actions = mem::take(&mut self.actions);
        }
        if applied {
            ctx.request_repaint();
        }
    }

    fn probe_backend(&mut self) {
        let request_id = self.next_request_id;
        self.next_request_id = self.next_request_id.wrapping_add(1).max(1);
        self.pending_probe = Some(request_id);
        self.model.backend = BackendStatus::Starting;
        if self.backend.send(Command::Probe { request_id }).is_err() {
            self.pending_probe = None;
            self.model.backend = BackendStatus::Failed("背景服務無法接收工作。".into());
        }
    }

    fn close_if_requested(&mut self, ctx: &egui::Context) {
        if self.close_requested {
            self.close_requested = false;
            piclens_infra::info("egui main viewport close requested");
            ctx.send_viewport_cmd(egui::ViewportCommand::Close);
        }
    }
}

impl eframe::App for PicLensApp {
    fn logic(&mut self, ctx: &egui::Context, _frame: &mut eframe::Frame) {
        self.handle_events();
        self.apply_actions(ctx);
        self.close_if_requested(ctx);
    }

    fn ui(&mut self, ui: &mut egui::Ui, _frame: &mut eframe::Frame) {
        ui::show(&self.model, ui, &mut self.actions);
        self.apply_actions(ui.ctx());
    }
}
