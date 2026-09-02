//! Window layout. Views append actions and do not perform side effects.

use egui::{AtomExt, Color32, Frame, Margin, RichText, Stroke};
use piclens_domain::{path_equals, visible_tree_rows, ListItem, SortDirection, SortKey, SortState};

use crate::images::{ThumbnailKey, ThumbnailLoader};
use crate::model::{Action, AppModel, Loadable, Page, SelectionGesture};

pub fn show(
    model: &AppModel,
    images: &ThumbnailLoader,
    ui: &mut egui::Ui,
    actions: &mut Vec<Action>,
) -> Vec<ThumbnailKey> {
    let mut materialized = Vec::new();
    if model.page == Page::Viewer {
        viewer_input(ui, actions);
        egui::CentralPanel::default()
            .frame(
                Frame::new()
                    .fill(Color32::from_rgb(22, 24, 29))
                    .inner_margin(Margin::same(20)),
            )
            .show(ui, |ui| {
                viewer_content(model, images, ui, actions, &mut materialized)
            });
        return materialized;
    }
    navigation_input(ui, actions);
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
                if !model.tree_roots.is_empty() {
                    ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                        let label = if model.sidebar_collapsed {
                            "顯示資料夾樹"
                        } else {
                            "隱藏資料夾樹"
                        };
                        if ui.button(label).clicked() {
                            actions.push(Action::ToggleSidebar);
                        }
                    });
                }
            });
        });

    if !model.sidebar_collapsed && !model.tree_roots.is_empty() {
        egui::Panel::left("folder-tree")
            .default_size(230.0)
            .min_size(160.0)
            .max_size(360.0)
            .frame(
                Frame::new()
                    .fill(Color32::from_rgb(244, 246, 249))
                    .inner_margin(Margin::symmetric(12, 16)),
            )
            .show(ui, |ui| folder_tree(model, ui, actions));
    }

    egui::CentralPanel::default()
        .frame(
            Frame::new()
                .fill(Color32::from_rgb(248, 249, 251))
                .inner_margin(Margin::same(32)),
        )
        .show(ui, |ui| {
            library_content(model, images, ui, actions, &mut materialized);
        });
    materialized
}

fn library_content(
    model: &AppModel,
    images: &ThumbnailLoader,
    ui: &mut egui::Ui,
    actions: &mut Vec<Action>,
    materialized: &mut Vec<ThumbnailKey>,
) {
    ui.horizontal(|ui| {
        if ui
            .add_enabled(model.history.can_back(), egui::Button::new("上一頁"))
            .clicked()
        {
            actions.push(Action::NavigateHistory { back: true });
        }
        if ui
            .add_enabled(model.history.can_forward(), egui::Button::new("下一頁"))
            .clicked()
        {
            actions.push(Action::NavigateHistory { back: false });
        }
        if ui.button("選擇資料夾").clicked() {
            actions.push(Action::ChooseFolder);
        }
        if model.library_query.is_some() && ui.button("重新整理").clicked() {
            actions.push(Action::ReloadLibrary);
        }
        if let Some(folder) = &model.current_folder {
            ui.separator();
            let name = folder
                .file_name()
                .and_then(|name| name.to_str())
                .map(str::to_owned)
                .unwrap_or_else(|| folder.display().to_string());
            ui.vertical(|ui| {
                ui.label(RichText::new(name).strong());
                ui.label(RichText::new(folder.display().to_string()).small());
            });
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
            ui.horizontal(|ui| {
                ui.label(format!("{} 個項目", model.visible_items.len()));
                ui.separator();
                ui.label(format!(
                    "選取 {} 張圖片",
                    model.selection.ordered_paths.len()
                ));
                if !model.selection.ordered_paths.is_empty()
                    && ui.small_button("清除選取").clicked()
                {
                    actions.push(Action::ClearSelection);
                }
            });
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
                gallery_grid(model, images, ui, actions, materialized);
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

fn gallery_grid(
    model: &AppModel,
    images: &ThumbnailLoader,
    ui: &mut egui::Ui,
    actions: &mut Vec<Action>,
    materialized: &mut Vec<ThumbnailKey>,
) {
    const GAP: f32 = 8.0;

    let tile_width = model.thumbnail_size as f32;
    let tile_height = (tile_width * 0.58).max(76.0);
    let columns = (((ui.available_width() + GAP) / (tile_width + GAP)).floor() as usize).max(1);
    let rows = model.visible_items.len().div_ceil(columns);
    egui::ScrollArea::vertical().show_rows(ui, tile_height + GAP, rows, |ui, visible_rows| {
        for row in visible_rows {
            ui.horizontal(|ui| {
                ui.spacing_mut().item_spacing.x = GAP;
                for column in 0..columns {
                    let index = row * columns + column;
                    let Some(item) = model.visible_items.get(index) else {
                        break;
                    };
                    match item {
                        ListItem::Folder(folder) => {
                            let response = ui.add_sized(
                                egui::vec2(tile_width, tile_height),
                                egui::Button::new(format!("{}\n資料夾", folder.name)),
                            );
                            if response.clicked() {
                                actions.push(Action::NavigateFolder(folder.path.clone().into()));
                            }
                        }
                        ListItem::Image(image) => {
                            let key = ThumbnailKey::from_image(image, model.thumbnail_size as u32);
                            if !image.is_animated {
                                materialized.push(key.clone());
                            }
                            let selected = model
                                .selection
                                .ordered_paths
                                .iter()
                                .any(|path| path_equals(&path.to_string_lossy(), &image.path));
                            let label = if image.is_animated {
                                format!("{}\n動畫圖片\n不支援預覽", image.name)
                            } else if images.failure(&key).is_some() {
                                format!(
                                    "{}\n{}\n縮圖載入失敗",
                                    image.name,
                                    image.extension.to_uppercase()
                                )
                            } else {
                                format!("{}\n{}", image.name, image.extension.to_uppercase())
                            };
                            let hover = images
                                .failure(&key)
                                .map(str::to_owned)
                                .unwrap_or_else(|| image.extension.to_uppercase());
                            let response = ui
                                .push_id(&image.path, |ui| {
                                    let button = if let Some(texture) = images.texture(&key) {
                                        let preview_size = egui::vec2(
                                            (tile_width * 0.55).max(48.0),
                                            (tile_height - 16.0).max(48.0),
                                        );
                                        egui::Button::new((
                                            egui::Image::from_texture(texture)
                                                .alt_text(image.name.clone())
                                                .atom_size(preview_size),
                                            label,
                                        ))
                                        .selected(selected)
                                    } else {
                                        egui::Button::new(label).selected(selected)
                                    };
                                    ui.add_sized(egui::vec2(tile_width, tile_height), button)
                                })
                                .inner
                                .on_hover_text(hover);
                            if response.double_clicked() {
                                actions.push(Action::OpenViewer(image.path.clone().into()));
                            } else if response.clicked() {
                                let modifiers = ui.input(|input| input.modifiers);
                                let gesture = if modifiers.shift {
                                    SelectionGesture::Range {
                                        additive: modifiers.ctrl,
                                    }
                                } else if modifiers.ctrl {
                                    SelectionGesture::Toggle
                                } else {
                                    SelectionGesture::Replace
                                };
                                actions.push(Action::SelectImage {
                                    path: image.path.clone().into(),
                                    gesture,
                                });
                            }
                        }
                    }
                }
            });
        }
    });
}

fn viewer_input(ui: &mut egui::Ui, actions: &mut Vec<Action>) {
    let action = ui.input_mut(|input| {
        if input.consume_key(egui::Modifiers::NONE, egui::Key::Escape) {
            Some(Action::CloseViewer)
        } else if input.consume_key(egui::Modifiers::NONE, egui::Key::ArrowLeft) {
            Some(Action::StepViewer(-1))
        } else if input.consume_key(egui::Modifiers::NONE, egui::Key::ArrowRight) {
            Some(Action::StepViewer(1))
        } else {
            None
        }
    });
    actions.extend(action);
}

fn viewer_content(
    model: &AppModel,
    images: &ThumbnailLoader,
    ui: &mut egui::Ui,
    actions: &mut Vec<Action>,
    materialized: &mut Vec<ThumbnailKey>,
) {
    let Some(viewer) = &model.viewer else {
        ui.label(RichText::new("檢視器狀態無效。").color(Color32::WHITE));
        if ui.button("返回圖庫").clicked() {
            actions.push(Action::CloseViewer);
        }
        return;
    };
    let Some(current) = viewer.snapshot.current() else {
        ui.label(RichText::new("快照中沒有圖片。").color(Color32::WHITE));
        if ui.button("返回圖庫").clicked() {
            actions.push(Action::CloseViewer);
        }
        return;
    };

    ui.horizontal(|ui| {
        if ui.button("返回圖庫").clicked() {
            actions.push(Action::CloseViewer);
        }
        if ui.button("上一張").clicked() {
            actions.push(Action::StepViewer(-1));
        }
        if ui.button("下一張").clicked() {
            actions.push(Action::StepViewer(1));
        }
        if ui.button("在檔案總管中顯示").clicked() {
            actions.push(Action::RevealViewer);
        }
        ui.separator();
        ui.label(
            RichText::new(format!(
                "{} / {}  {}",
                viewer.snapshot.current_index + 1,
                viewer.snapshot.images.len(),
                current.name
            ))
            .color(Color32::WHITE)
            .strong(),
        );
    });
    ui.add_space(16.0);

    if current.is_animated {
        ui.centered_and_justified(|ui| {
            ui.label(
                RichText::new("此動畫圖片目前不支援預覽。")
                    .color(Color32::WHITE)
                    .size(18.0),
            );
        });
        return;
    }

    let key = ThumbnailKey::from_image(current, 1024);
    materialized.push(key.clone());
    if let Some(texture) = images.texture(&key) {
        ui.centered_and_justified(|ui| {
            ui.add(
                egui::Image::from_texture(texture)
                    .alt_text(current.name.clone())
                    .max_size(ui.available_size())
                    .maintain_aspect_ratio(true),
            );
        });
    } else if let Some(message) = images.failure(&key) {
        ui.centered_and_justified(|ui| {
            ui.label(
                RichText::new(format!("圖片載入失敗：{message}"))
                    .color(Color32::from_rgb(255, 150, 150)),
            );
        });
    } else {
        ui.centered_and_justified(|ui| {
            ui.spinner();
            ui.label(RichText::new("正在載入圖片…").color(Color32::WHITE));
        });
    }
}

fn navigation_input(ui: &mut egui::Ui, actions: &mut Vec<Action>) {
    let action = ui.input_mut(|input| {
        if input.consume_key(egui::Modifiers::ALT, egui::Key::ArrowLeft)
            || input.pointer.button_pressed(egui::PointerButton::Extra1)
        {
            Some(Action::NavigateHistory { back: true })
        } else if input.consume_key(egui::Modifiers::ALT, egui::Key::ArrowRight)
            || input.pointer.button_pressed(egui::PointerButton::Extra2)
        {
            Some(Action::NavigateHistory { back: false })
        } else {
            None
        }
    });
    actions.extend(action);
}

fn folder_tree(model: &AppModel, ui: &mut egui::Ui, actions: &mut Vec<Action>) {
    ui.heading("資料夾");
    ui.add_space(8.0);
    egui::ScrollArea::vertical().show(ui, |ui| {
        for row in visible_tree_rows(
            &model.tree_roots,
            &model.tree_children,
            &model.tree_expanded,
        ) {
            let path = std::path::Path::new(&row.path);
            let name = path
                .file_name()
                .and_then(|name| name.to_str())
                .map(str::to_owned)
                .unwrap_or_else(|| row.path.clone());
            ui.horizontal(|ui| {
                ui.add_space(row.depth as f32 * 14.0);
                if row.expandable {
                    let label = if row.expanded { "收合" } else { "展開" };
                    if ui
                        .small_button(label)
                        .on_hover_text(format!("{label} {name}"))
                        .clicked()
                    {
                        actions.push(Action::ToggleTreeFolder(row.path.clone()));
                    }
                } else {
                    ui.add_space(42.0);
                }
                let selected = model
                    .current_folder
                    .as_ref()
                    .is_some_and(|current| path_equals(&current.to_string_lossy(), &row.path));
                if ui
                    .selectable_label(selected, &name)
                    .on_hover_text(&row.path)
                    .clicked()
                {
                    actions.push(Action::NavigateFolder(row.path.clone().into()));
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
            show(&model, &ThumbnailLoader::default(), ui, &mut actions);
        });
        harness.run();
        let _ = harness.get_by_label("選擇資料夾");
    }

    #[test]
    fn renders_error_shell_headlessly() {
        let model = crate::demo::startup_error("測試錯誤");
        let mut harness = Harness::new_ui(move |ui| {
            let mut actions = Vec::new();
            show(&model, &ThumbnailLoader::default(), ui, &mut actions);
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
            show(&model, &ThumbnailLoader::default(), ui, &mut actions);
        });
        harness.run();
        let _ = harness.get_by_label("2 個項目");
        let _ = harness.get_by_label_contains("album");
        let _ = harness.get_by_label_contains("image2.png");
    }

    #[test]
    fn viewer_renders_only_the_current_preview_request() {
        let mut model = crate::demo::loaded_library();
        let query = model.library_query.as_ref().unwrap();
        let snapshot = piclens_domain::ImageSequenceSnapshot::from_visible(
            query.folder_path.clone(),
            query.include_subfolders,
            query.sort,
            &model.visible_items,
            "C:/fixture/image2.png",
        )
        .unwrap();
        model.page = Page::Viewer;
        model.viewer = Some(crate::model::ViewerState {
            snapshot,
            preview: Loadable::Idle,
        });
        let mut harness = Harness::new_ui_state(
            move |ui, materialized: &mut Vec<ThumbnailKey>| {
                let mut actions = Vec::new();
                *materialized = show(&model, &ThumbnailLoader::default(), ui, &mut actions);
            },
            Vec::new(),
        );
        harness.step();

        let _ = harness.get_by_label("返回圖庫");
        let _ = harness.get_by_label_contains("image2.png");
        assert_eq!(harness.state().len(), 1);
        assert_eq!(harness.state()[0].longest_edge, 1024);
    }

    #[test]
    fn folder_tile_emits_navigation_action() {
        let model = crate::demo::loaded_library();
        let mut harness = Harness::new_ui_state(
            move |ui, actions: &mut Vec<Action>| {
                show(&model, &ThumbnailLoader::default(), ui, actions);
            },
            Vec::new(),
        );
        harness.run();

        harness.get_by_label_contains("album").click();
        harness.run();

        assert_eq!(
            harness.state().as_slice(),
            [Action::NavigateFolder("C:/fixture/album".into())]
        );
    }

    #[test]
    fn folder_tree_renders_fixed_root_and_expandable_child() {
        let mut model = crate::demo::loaded_library();
        model.tree_roots = vec!["C:/tree-root".into()];
        model
            .tree_children
            .insert("C:/tree-root".into(), vec!["C:/tree-root/nested".into()]);
        model.tree_expanded.insert("C:/tree-root".into());
        let mut harness = Harness::new_ui_state(
            move |ui, actions: &mut Vec<Action>| {
                show(&model, &ThumbnailLoader::default(), ui, actions);
            },
            Vec::new(),
        );
        harness.run();

        let _ = harness.get_by_label("tree-root");
        harness.get_by_label("nested").click();
        harness.run();

        assert_eq!(
            harness.state().as_slice(),
            [Action::NavigateFolder("C:/tree-root/nested".into())]
        );
    }

    #[test]
    fn renders_large_library_through_virtualized_rows() {
        let model = crate::demo::large_library(10_000);
        let mut harness = Harness::new_ui_state(
            move |ui, materialized: &mut Vec<ThumbnailKey>| {
                let mut actions = Vec::new();
                *materialized = show(&model, &ThumbnailLoader::default(), ui, &mut actions);
            },
            Vec::new(),
        );
        harness.run();
        let _ = harness.get_by_label("10000 個項目");
        let _ = harness.get_by_label_contains("image0.png");
        assert!(harness.state().len() < 100);
    }

    #[test]
    fn animated_images_show_unsupported_state_without_thumbnail_request() {
        let mut model = crate::demo::loaded_library();
        for item in &mut model.visible_items {
            if let ListItem::Image(image) = item {
                image.is_animated = true;
            }
        }
        let mut harness = Harness::new_ui_state(
            move |ui, materialized: &mut Vec<ThumbnailKey>| {
                let mut actions = Vec::new();
                *materialized = show(&model, &ThumbnailLoader::default(), ui, &mut actions);
            },
            Vec::new(),
        );
        harness.run();

        let _ = harness.get_by_label_contains("不支援預覽");
        assert!(harness.state().is_empty());
    }

    #[test]
    fn image_clicks_emit_modifier_specific_selection_actions() {
        let model = crate::demo::loaded_library();
        let mut harness = Harness::new_ui_state(
            move |ui, actions: &mut Vec<Action>| {
                show(&model, &ThumbnailLoader::default(), ui, actions);
            },
            Vec::new(),
        );
        harness.run();

        harness.get_by_label_contains("image2.png").click();
        harness.run();
        harness
            .get_by_label_contains("image2.png")
            .click_modifiers(egui::Modifiers {
                ctrl: true,
                ..Default::default()
            });
        harness.run();
        harness
            .get_by_label_contains("image2.png")
            .click_modifiers(egui::Modifiers {
                shift: true,
                ..Default::default()
            });
        harness.run();
        harness
            .get_by_label_contains("image2.png")
            .click_modifiers(egui::Modifiers {
                ctrl: true,
                shift: true,
                ..Default::default()
            });
        harness.run();

        assert_eq!(
            harness.state().as_slice(),
            [
                Action::SelectImage {
                    path: "C:/fixture/image2.png".into(),
                    gesture: SelectionGesture::Replace,
                },
                Action::SelectImage {
                    path: "C:/fixture/image2.png".into(),
                    gesture: SelectionGesture::Toggle,
                },
                Action::SelectImage {
                    path: "C:/fixture/image2.png".into(),
                    gesture: SelectionGesture::Range { additive: false },
                },
                Action::SelectImage {
                    path: "C:/fixture/image2.png".into(),
                    gesture: SelectionGesture::Range { additive: true },
                },
            ]
        );
    }
}
