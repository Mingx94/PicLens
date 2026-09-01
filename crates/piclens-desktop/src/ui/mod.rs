//! Window layout. Views append actions and do not perform side effects.

use egui::{Color32, Frame, Margin, RichText, Stroke};

use crate::model::{Action, AppModel, Loadable};

pub fn show(model: &AppModel, ui: &mut egui::Ui, actions: &mut Vec<Action>) {
    egui::Panel::top("app-bar")
        .frame(
            Frame::new()
                .fill(Color32::WHITE)
                .stroke(Stroke::new(1.0, Color32::from_rgb(224, 228, 235)))
                .inner_margin(Margin::symmetric(20, 12)),
        )
        .show(ui, |ui| {
            ui.horizontal(|ui| {
                ui.heading("PicLens");
                ui.separator();
                ui.label("egui 遷移骨架");
            });
        });

    egui::CentralPanel::default()
        .frame(
            Frame::new()
                .fill(Color32::from_rgb(248, 249, 251))
                .inner_margin(Margin::same(32)),
        )
        .show(ui, |ui| {
            ui.vertical_centered(|ui| {
                ui.add_space(72.0);
                ui.heading("本機圖片圖庫");
                ui.add_space(8.0);
                match &model.initial_folder {
                    Some(folder) => {
                        ui.label(format!("準備載入：{}", folder.display()));
                    }
                    None => {
                        ui.label("選擇資料夾後開始瀏覽圖片。");
                    }
                }
                ui.add_space(16.0);
                if ui.button("選擇資料夾").clicked() {
                    actions.push(Action::ChooseFolder);
                }
                ui.add_space(24.0);
                backend_status(model, ui, actions);
                if let Some(notice) = &model.notice {
                    ui.add_space(12.0);
                    ui.horizontal(|ui| {
                        ui.label(notice);
                        if ui.small_button("關閉").clicked() {
                            actions.push(Action::DismissStatus);
                        }
                    });
                }
            });
        });
}

fn backend_status(model: &AppModel, ui: &mut egui::Ui, actions: &mut Vec<Action>) {
    match &model.backend {
        Loadable::Idle | Loadable::Loading => {
            ui.spinner();
            ui.label("正在啟動背景服務…");
        }
        Loadable::Ready(()) => {
            ui.label(RichText::new("背景服務已就緒").color(Color32::from_rgb(30, 130, 76)));
        }
        Loadable::Failed(message) => {
            ui.label(
                RichText::new(message)
                    .color(Color32::from_rgb(183, 35, 35))
                    .strong(),
            );
            if ui.button("再試一次").clicked() {
                actions.push(Action::RetryBackendProbe);
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use egui_kittest::{kittest::Queryable, Harness};

    use super::*;

    #[test]
    fn renders_empty_shell_headlessly() {
        let model = crate::demo::empty_library();
        let mut harness = Harness::new_ui(move |ui| {
            let mut actions = Vec::new();
            show(&model, ui, &mut actions);
        });
        harness.run();
        let _ = harness.get_by_label("選擇資料夾");
    }

    #[test]
    fn renders_error_shell_headlessly() {
        let model = crate::demo::startup_error("測試錯誤");
        let mut harness = Harness::new_ui(move |ui| {
            let mut actions = Vec::new();
            show(&model, ui, &mut actions);
        });
        harness.run();
        let _ = harness.get_by_label("測試錯誤");
        let _ = harness.get_by_label("再試一次");
    }
}
