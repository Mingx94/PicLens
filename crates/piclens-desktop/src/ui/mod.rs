//! Window layout. Views append actions and do not perform side effects.

use egui::{Align, Color32, Frame, Layout, Margin, RichText, Stroke};
use piclens_domain::{ListItem, SortDirection, SortKey, SortState};

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
                ui.label("本機圖片圖庫");
            });
        });

    egui::CentralPanel::default()
        .frame(
            Frame::new()
                .fill(Color32::from_rgb(248, 249, 251))
                .inner_margin(Margin::same(32)),
        )
        .show(ui, |ui| {
            library_content(model, ui, actions);
        });
}

fn library_content(model: &AppModel, ui: &mut egui::Ui, actions: &mut Vec<Action>) {
    ui.horizontal(|ui| {
        if ui.button("選擇資料夾").clicked() {
            actions.push(Action::ChooseFolder);
        }
        if model.library_query.is_some() && ui.button("重新整理").clicked() {
            actions.push(Action::ReloadLibrary);
        }
        if let Some(folder) = &model.current_folder {
            ui.separator();
            ui.label(folder.display().to_string());
        }
    });
    if let Some(query) = &model.library_query {
        ui.add_space(8.0);
        ui.horizontal(|ui| {
            let mut search = model.search.clone();
            if ui
                .add(egui::TextEdit::singleline(&mut search).hint_text("搜尋名稱或路徑…"))
                .changed()
            {
                actions.push(Action::SetSearch(search));
            }

            let mut sort = query.sort;
            egui::ComboBox::from_id_salt("library-sort")
                .selected_text(sort_label(sort))
                .show_ui(ui, |ui| {
                    ui.selectable_value(
                        &mut sort,
                        SortState {
                            key: SortKey::Name,
                            direction: SortDirection::Asc,
                        },
                        "名稱：小到大",
                    );
                    ui.selectable_value(
                        &mut sort,
                        SortState {
                            key: SortKey::Name,
                            direction: SortDirection::Desc,
                        },
                        "名稱：大到小",
                    );
                    ui.selectable_value(
                        &mut sort,
                        SortState {
                            key: SortKey::ModifiedAt,
                            direction: SortDirection::Asc,
                        },
                        "修改時間：舊到新",
                    );
                    ui.selectable_value(
                        &mut sort,
                        SortState {
                            key: SortKey::ModifiedAt,
                            direction: SortDirection::Desc,
                        },
                        "修改時間：新到舊",
                    );
                });
            if sort != query.sort {
                actions.push(Action::SetSort(sort));
            }

            if ui
                .selectable_label(query.include_subfolders, "含子資料夾")
                .clicked()
            {
                actions.push(Action::ToggleIncludeSubfolders);
            }

            let mut thumbnail_size = model.thumbnail_size;
            if ui
                .add(
                    egui::Slider::new(&mut thumbnail_size, 120..=240)
                        .step_by(20.0)
                        .text("縮圖"),
                )
                .changed()
            {
                actions.push(Action::SetThumbnailSize(thumbnail_size));
            }
        });
    }
    ui.add_space(12.0);

    match &model.library {
        Loadable::Idle => {
            ui.vertical_centered(|ui| {
                ui.add_space(60.0);
                ui.heading("選擇資料夾後開始瀏覽圖片。");
            });
        }
        Loadable::Loading => {
            ui.vertical_centered(|ui| {
                ui.add_space(60.0);
                ui.spinner();
                ui.label("正在載入圖庫…");
            });
        }
        Loadable::Ready(items) => {
            ui.label(format!("{} 個項目", model.visible_items.len()));
            ui.add_space(8.0);
            if model.visible_items.is_empty() {
                ui.vertical_centered(|ui| {
                    ui.add_space(48.0);
                    ui.label(if items.is_empty() {
                        "此資料夾沒有可顯示的圖片或子資料夾。"
                    } else {
                        "找不到符合搜尋條件的項目。"
                    });
                });
            } else {
                gallery_grid(&model.visible_items, model.thumbnail_size, ui);
            }
        }
        Loadable::Failed(message) => {
            ui.vertical_centered(|ui| {
                ui.add_space(48.0);
                ui.label(RichText::new(message).color(Color32::from_rgb(183, 35, 35)));
                if ui.button("重新載入").clicked() {
                    actions.push(Action::ReloadLibrary);
                }
            });
        }
    }

    ui.add_space(16.0);
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
}

fn sort_label(sort: SortState) -> &'static str {
    match (sort.key, sort.direction) {
        (SortKey::Name, SortDirection::Asc) => "名稱：小到大",
        (SortKey::Name, SortDirection::Desc) => "名稱：大到小",
        (SortKey::ModifiedAt, SortDirection::Asc) => "修改時間：舊到新",
        (SortKey::ModifiedAt, SortDirection::Desc) => "修改時間：新到舊",
    }
}

fn gallery_grid(items: &[ListItem], thumbnail_size: i32, ui: &mut egui::Ui) {
    const GAP: f32 = 8.0;

    let tile_width = thumbnail_size as f32;
    let tile_height = (tile_width * 0.58).max(76.0);
    let columns = (((ui.available_width() + GAP) / (tile_width + GAP)).floor() as usize).max(1);
    let rows = items.len().div_ceil(columns);
    egui::ScrollArea::vertical().show_rows(ui, tile_height + GAP, rows, |ui, visible_rows| {
        for row in visible_rows {
            ui.horizontal(|ui| {
                ui.spacing_mut().item_spacing.x = GAP;
                for column in 0..columns {
                    let index = row * columns + column;
                    let Some(item) = items.get(index) else {
                        break;
                    };
                    Frame::group(ui.style())
                        .fill(Color32::WHITE)
                        .inner_margin(Margin::same(10))
                        .show(ui, |ui| {
                            ui.allocate_ui_with_layout(
                                egui::vec2(tile_width, tile_height),
                                Layout::top_down(Align::Min),
                                |ui| {
                                    ui.strong(item.name());
                                    match item {
                                        ListItem::Folder(_) => {
                                            ui.label("資料夾");
                                        }
                                        ListItem::Image(image) => {
                                            ui.label(image.extension.to_uppercase());
                                        }
                                    }
                                },
                            );
                        });
                }
            });
        }
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

    #[test]
    fn renders_loaded_library_headlessly() {
        let model = crate::demo::loaded_library();
        let mut harness = Harness::new_ui(move |ui| {
            let mut actions = Vec::new();
            show(&model, ui, &mut actions);
        });
        harness.run();
        let _ = harness.get_by_label("2 個項目");
        let _ = harness.get_by_label("album");
        let _ = harness.get_by_label("image2.png");
    }

    #[test]
    fn renders_large_library_through_virtualized_rows() {
        let model = crate::demo::large_library(10_000);
        let mut harness = Harness::new_ui(move |ui| {
            let mut actions = Vec::new();
            show(&model, ui, &mut actions);
        });
        harness.run();
        let _ = harness.get_by_label("10000 個項目");
        let _ = harness.get_by_label("image0.png");
    }
}
