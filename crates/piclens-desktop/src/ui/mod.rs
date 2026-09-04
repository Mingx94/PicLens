//! Window layout. Views append actions and do not perform side effects.

use std::path::PathBuf;

use egui::{AtomExt, Color32, Frame, Margin, RichText, Stroke};
use piclens_domain::{
    is_fit_view, path_equals, visible_tree_rows, FileOperationStatus, ImageSequenceSnapshot,
    ListItem, Point, SortDirection, SortKey, SortState,
};

use crate::images::{ThumbnailKey, ThumbnailLoader};
use crate::model::{
    Action, AppModel, ConversionKind, DialogState, Loadable, Page, SelectionGesture,
};
use crate::theme;

pub(crate) fn gallery_focus_id() -> egui::Id {
    egui::Id::new("piclens-library-search")
}

pub(crate) fn rename_focus_id() -> egui::Id {
    egui::Id::new("piclens-rename-input")
}

const MINIMUM_LAYOUT_WIDTH: f32 = 800.0;

pub fn show(
    model: &AppModel,
    images: &ThumbnailLoader,
    ui: &mut egui::Ui,
    actions: &mut Vec<Action>,
) -> Vec<ThumbnailKey> {
    let mut materialized = Vec::new();
    let entered_page = entered_page(ui.ctx(), model.page);
    let palette = theme::palette(ui.ctx());
    application_input(ui, actions);
    if model.page == Page::Viewer {
        if model.dialog.is_none() {
            viewer_input(model, ui, actions);
        }
        egui::CentralPanel::default()
            .frame(
                Frame::new()
                    .fill(palette.viewer_canvas)
                    .inner_margin(Margin::same(20)),
            )
            .show(ui, |ui| {
                viewer_content(model, images, ui, actions, &mut materialized, entered_page)
            });
        show_dialog(model, ui.ctx(), actions);
        return materialized;
    }
    let compact = ui.max_rect().width() <= MINIMUM_LAYOUT_WIDTH;
    if model.dialog.is_none() {
        navigation_input(ui, actions);
    }
    if model.drag.is_some()
        && ui.input_mut(|input| input.consume_key(egui::Modifiers::NONE, egui::Key::Escape))
    {
        actions.push(Action::CancelDrag);
    }
    egui::Panel::top("app-bar")
        .frame(
            Frame::new()
                .fill(palette.command_surface)
                .stroke(Stroke::new(1.0, palette.border))
                .inner_margin(Margin::symmetric(if compact { 12 } else { 20 }, 12)),
        )
        .show(ui, |ui| {
            ui.horizontal_wrapped(|ui| {
                ui.heading("PicLens");
                ui.separator();
                ui.label(RichText::new("本機圖片圖庫").color(palette.secondary));
                if !compact && !model.tree_roots.is_empty() {
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

    if !compact && !model.sidebar_collapsed && !model.tree_roots.is_empty() {
        egui::Panel::left("folder-tree")
            .default_size(230.0)
            .min_size(160.0)
            .max_size(360.0)
            .frame(
                Frame::new()
                    .fill(palette.sidebar)
                    .inner_margin(Margin::symmetric(12, 16)),
            )
            .show(ui, |ui| folder_tree(model, ui, actions));
    }

    egui::CentralPanel::default()
        .frame(
            Frame::new()
                .fill(palette.content)
                .inner_margin(Margin::same(if compact { 16 } else { 32 })),
        )
        .show(ui, |ui| {
            library_content(model, images, ui, actions, &mut materialized, compact);
        });
    show_dialog(model, ui.ctx(), actions);
    paint_drag_preview(model, ui.ctx());
    materialized
}

fn entered_page(ctx: &egui::Context, page: Page) -> bool {
    let id = egui::Id::new("piclens-current-page");
    ctx.data_mut(|data| {
        let previous = data.get_temp::<Page>(id);
        data.insert_temp(id, page);
        previous != Some(page)
    })
}

fn mark_live(ui: &egui::Ui, response: &egui::Response, live: egui::accesskit::Live) {
    ui.ctx()
        .accesskit_node_builder(response.id, |node| node.set_live(live));
}

fn application_input(ui: &mut egui::Ui, actions: &mut Vec<Action>) {
    let ctrl = egui::Modifiers {
        ctrl: true,
        ..egui::Modifiers::NONE
    };
    if ui.input_mut(|input| input.consume_key(ctrl, egui::Key::Q)) {
        actions.push(Action::Quit);
    }
}

fn library_content(
    model: &AppModel,
    images: &ThumbnailLoader,
    ui: &mut egui::Ui,
    actions: &mut Vec<Action>,
    materialized: &mut Vec<ThumbnailKey>,
    compact: bool,
) {
    let palette = theme::palette(ui.ctx());
    if let Some(folder) = &model.current_folder {
        let name = folder
            .file_name()
            .and_then(|name| name.to_str())
            .map(str::to_owned)
            .unwrap_or_else(|| folder.display().to_string());
        ui.label(RichText::new(name).size(20.0).strong());
        ui.add(
            egui::Label::new(
                RichText::new(folder.display().to_string())
                    .small()
                    .color(palette.secondary),
            )
            .truncate(),
        )
        .on_hover_text(folder.display().to_string());
        ui.add_space(8.0);
    }
    if model.library_query.is_some() {
        ui.horizontal_wrapped(|ui| {
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
            if ui.button("重新整理").clicked() {
                actions.push(Action::ReloadLibrary);
            }
        });
    }
    if let Some(query) = &model.library_query {
        ui.add_space(8.0);
        ui.horizontal_wrapped(|ui| {
            let mut search = model.search.clone();
            let previous_search = search.clone();
            let search_response = ui.add(
                egui::TextEdit::singleline(&mut search)
                    .id(gallery_focus_id())
                    .desired_width(if compact { 220.0 } else { 280.0 })
                    .hint_text("搜尋名稱或路徑…"),
            );
            search_response.widget_info(|| {
                let mut info =
                    egui::WidgetInfo::text_edit(true, &previous_search, &search, "搜尋名稱或路徑…");
                info.label = Some("搜尋圖片".into());
                info
            });
            if search_response.changed() {
                actions.push(Action::SetSearch(search));
            }

            let mut sort = query.sort;
            ui.label(RichText::new("排序").color(palette.secondary));
            let sort_response = egui::ComboBox::from_id_salt("piclens-library-sort")
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
            sort_response.response.widget_info(|| {
                egui::WidgetInfo::labeled(egui::WidgetType::ComboBox, true, "排序")
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
            ui.label(RichText::new("縮圖").color(palette.secondary));
            if ui
                .add(
                    egui::Slider::new(&mut thumbnail_size, 120..=240)
                        .step_by(20.0)
                        .show_value(true),
                )
                .on_hover_text("調整縮圖大小")
                .changed()
            {
                actions.push(Action::SetThumbnailSize(thumbnail_size));
            }
        });
    }
    ui.add_space(12.0);

    status_feedback(model, ui, actions);

    let tile_width = model.thumbnail_size as f32;
    let tile_height = gallery_tile_height(tile_width);
    let columns = (((ui.available_width() + 8.0) / (tile_width + 8.0)).floor() as usize).max(1);
    let page_rows = ((ui.available_height() / (tile_height + 8.0)).floor() as usize).max(1);
    gallery_input(model, ui, actions, columns, page_rows);

    match &model.library {
        Loadable::Idle => {
            ui.vertical_centered(|ui| {
                ui.add_space(60.0);
                ui.heading("選擇資料夾後開始瀏覽圖片。");
                ui.label(
                    RichText::new("PicLens 只會讀取你選擇的本機資料夾。").color(palette.secondary),
                );
                ui.add_space(12.0);
                if ui.button("選擇資料夾").clicked() {
                    actions.push(Action::ChooseFolder);
                }
            });
        }
        Loadable::Loading => {
            ui.vertical_centered(|ui| {
                ui.add_space(60.0);
                ui.spinner();
                let response = ui.label("正在載入圖庫…");
                mark_live(ui, &response, egui::accesskit::Live::Polite);
            });
        }
        Loadable::Ready(items) => {
            ui.horizontal_wrapped(|ui| {
                ui.label(
                    RichText::new(format!("{} 個項目", model.visible_items.len()))
                        .color(palette.secondary),
                );
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
                let visible_image_count = model
                    .visible_items
                    .iter()
                    .filter(|item| item.as_image().is_some())
                    .count();
                ui.add_enabled_ui(visible_image_count > 0, |ui| {
                    ui.menu_button("批次操作", |ui| {
                        if ui.button("目前結果轉 JPG").clicked() {
                            actions.push(Action::RequestConversion(ConversionKind::Jpg));
                            ui.close();
                        }
                        if ui.button("目前結果轉無損 WebP").clicked() {
                            actions.push(Action::RequestConversion(ConversionKind::Webp));
                            ui.close();
                        }
                        ui.separator();
                        if ui.button("清除目前結果的同名格式").clicked() {
                            actions.push(Action::RequestCleanup);
                            ui.close();
                        }
                    });
                });
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
                let response =
                    ui.label(RichText::new(message).color(theme::palette(ui.ctx()).danger));
                mark_live(ui, &response, egui::accesskit::Live::Assertive);
                if ui.button("重新載入").clicked() {
                    actions.push(Action::ReloadLibrary);
                }
            });
        }
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

fn gallery_input(
    model: &AppModel,
    ui: &mut egui::Ui,
    actions: &mut Vec<Action>,
    columns: usize,
    page_rows: usize,
) {
    if model.dialog.is_some() {
        return;
    }
    let search_focused = ui
        .ctx()
        .memory(|memory| memory.has_focus(gallery_focus_id()));
    let ctrl = egui::Modifiers {
        ctrl: true,
        ..egui::Modifiers::NONE
    };
    let ctrl_shift = egui::Modifiers {
        ctrl: true,
        shift: true,
        ..egui::Modifiers::NONE
    };
    let ctrl_alt = egui::Modifiers {
        ctrl: true,
        alt: true,
        ..egui::Modifiers::NONE
    };
    let mut focus_search = false;
    let action = ui.input_mut(|input| {
        let unmodified = egui::Modifiers::NONE;
        if input.consume_key(ctrl, egui::Key::F)
            || (!search_focused && input.consume_key(unmodified, egui::Key::Slash))
        {
            focus_search = true;
            None
        } else if input.consume_key(ctrl_shift, egui::Key::S) {
            Some(Action::ToggleIncludeSubfolders)
        } else if input.consume_key(ctrl_shift, egui::Key::R) {
            Some(Action::RequestDropRename)
        } else if input.consume_key(ctrl_shift, egui::Key::C) {
            Some(Action::RequestCleanup)
        } else if input.consume_key(ctrl_shift, egui::Key::E) {
            Some(Action::RevealSelection)
        } else if input.consume_key(ctrl_alt, egui::Key::Num1) {
            Some(Action::SetSort(SortState {
                key: SortKey::Name,
                direction: SortDirection::Asc,
            }))
        } else if input.consume_key(ctrl_alt, egui::Key::Num2) {
            Some(Action::SetSort(SortState {
                key: SortKey::Name,
                direction: SortDirection::Desc,
            }))
        } else if input.consume_key(ctrl_alt, egui::Key::Num3) {
            Some(Action::SetSort(SortState {
                key: SortKey::ModifiedAt,
                direction: SortDirection::Asc,
            }))
        } else if input.consume_key(ctrl_alt, egui::Key::Num4) {
            Some(Action::SetSort(SortState {
                key: SortKey::ModifiedAt,
                direction: SortDirection::Desc,
            }))
        } else if input.consume_key(ctrl, egui::Key::O) {
            Some(Action::ChooseFolder)
        } else if input.consume_key(ctrl, egui::Key::R)
            || input.consume_key(unmodified, egui::Key::F5)
        {
            Some(Action::ReloadLibrary)
        } else if input.consume_key(ctrl, egui::Key::B) {
            Some(Action::ToggleSidebar)
        } else if input.consume_key(ctrl, egui::Key::S) {
            Some(Action::CycleSort)
        } else if !search_focused && input.consume_key(ctrl, egui::Key::A) {
            Some(Action::SelectAllVisible)
        } else if !search_focused && input.consume_key(ctrl, egui::Key::J) {
            Some(Action::RequestConversion(ConversionKind::Jpg))
        } else if !search_focused && input.consume_key(ctrl, egui::Key::W) {
            Some(Action::RequestConversion(ConversionKind::Webp))
        } else if input.consume_key(ctrl, egui::Key::Escape) {
            Some(Action::CancelFileOperation)
        } else if search_focused
            && input.consume_key(unmodified, egui::Key::Escape)
            && !model.search.is_empty()
        {
            Some(Action::SetSearch(String::new()))
        } else if !search_focused && input.consume_key(unmodified, egui::Key::Escape) {
            if model.selection.focused_path.is_some() || !model.selection.ordered_paths.is_empty() {
                Some(Action::ClearSelection)
            } else if !model.search.is_empty() {
                Some(Action::SetSearch(String::new()))
            } else {
                None
            }
        } else if !search_focused && input.consume_key(unmodified, egui::Key::Backspace) {
            Some(Action::NavigateHistory { back: true })
        } else if !search_focused && input.consume_key(unmodified, egui::Key::ArrowUp) {
            Some(Action::MoveGallerySelection(-(columns as i32)))
        } else if !search_focused && input.consume_key(unmodified, egui::Key::ArrowDown) {
            Some(Action::MoveGallerySelection(columns as i32))
        } else if !search_focused && input.consume_key(unmodified, egui::Key::ArrowLeft) {
            Some(Action::MoveGallerySelection(-1))
        } else if !search_focused && input.consume_key(unmodified, egui::Key::ArrowRight) {
            Some(Action::MoveGallerySelection(1))
        } else if !search_focused && input.consume_key(unmodified, egui::Key::PageUp) {
            Some(Action::MoveGallerySelection(
                -((columns * page_rows) as i32),
            ))
        } else if !search_focused && input.consume_key(unmodified, egui::Key::PageDown) {
            Some(Action::MoveGallerySelection((columns * page_rows) as i32))
        } else if !search_focused && input.consume_key(unmodified, egui::Key::Home) {
            Some(Action::SelectGalleryBoundary { end: false })
        } else if !search_focused && input.consume_key(unmodified, egui::Key::End) {
            Some(Action::SelectGalleryBoundary { end: true })
        } else if !search_focused
            && (input.consume_key(unmodified, egui::Key::Enter)
                || input.consume_key(unmodified, egui::Key::Space))
        {
            Some(Action::OpenFocusedItem)
        } else if !search_focused && input.consume_key(unmodified, egui::Key::Delete) {
            Some(Action::RequestTrash)
        } else if !search_focused && input.consume_key(unmodified, egui::Key::F2) {
            Some(Action::OpenRename)
        } else if !search_focused
            && (input.consume_key(unmodified, egui::Key::Plus)
                || input.consume_key(unmodified, egui::Key::Equals))
        {
            Some(Action::SetThumbnailSize(model.thumbnail_size + 20))
        } else if !search_focused && input.consume_key(unmodified, egui::Key::Minus) {
            Some(Action::SetThumbnailSize(model.thumbnail_size - 20))
        } else {
            None
        }
    });
    if focus_search {
        ui.ctx()
            .memory_mut(|memory| memory.request_focus(gallery_focus_id()));
    }
    actions.extend(action);
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
    let tile_height = gallery_tile_height(tile_width);
    let columns = (((ui.available_width() + GAP) / (tile_width + GAP)).floor() as usize).max(1);
    let rows = model.visible_items.len().div_ceil(columns);
    let mut hovered_target = None;
    let scroll_id = ui.make_persistent_id("piclens-gallery-scroll");
    let mut scroll_area = egui::ScrollArea::vertical().id_salt("piclens-gallery-scroll");
    let current_offset =
        egui::scroll_area::State::load(ui.ctx(), scroll_id).map_or(0.0, |state| state.offset.y);
    if let Some(index) = model
        .gallery_scroll_target
        .filter(|index| *index < model.visible_items.len())
    {
        let row_stride = tile_height + ui.spacing().item_spacing.y;
        let offset = scroll_offset_for_row(
            current_offset,
            ui.available_height(),
            row_stride,
            index / columns,
        );
        scroll_area = scroll_area.vertical_scroll_offset(offset);
    } else if let Some(delta) = model.gallery_scroll_delta {
        scroll_area = scroll_area.vertical_scroll_offset((current_offset + delta).max(0.0));
    }
    if model.gallery_scroll_target.is_some() || model.gallery_scroll_delta.is_some() {
        actions.push(Action::ClearGalleryScrollTarget);
    }
    scroll_area.show_rows(ui, tile_height, rows, |ui, visible_rows| {
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
                            let selected =
                                model.selection.focused_path.as_ref().is_some_and(|path| {
                                    path_equals(&path.to_string_lossy(), &folder.path)
                                });
                            let response = ui
                                .add_sized(
                                    egui::vec2(tile_width, tile_height),
                                    egui::Button::new(format!("{}\n資料夾", folder.name))
                                        .selected(selected)
                                        .truncate(),
                                )
                                .on_hover_text(&folder.path);
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
                                format!("{}\n縮圖載入失敗", image.name)
                            } else {
                                image.name.clone()
                            };
                            let hover = images
                                .failure(&key)
                                .map(|failure| format!("{}\n{failure}", image.path))
                                .unwrap_or_else(|| image.path.clone());
                            let accessible_label = label.clone();
                            let response = ui
                                .push_id(&image.path, |ui| {
                                    let button = if let Some(texture) = images.texture(&key) {
                                        let preview_size =
                                            egui::Vec2::splat(gallery_thumbnail_size(tile_width));
                                        let contents = egui::AtomLayout::new((
                                            egui::Image::from_texture(texture)
                                                .alt_text(image.name.clone())
                                                .uv(cover_uv(texture.size_vec2()))
                                                .atom_size(preview_size),
                                            label,
                                        ))
                                        .direction(egui::Direction::TopDown)
                                        .wrap_mode(egui::TextWrapMode::Truncate)
                                        .min_size(egui::vec2(tile_width - 24.0, tile_height - 12.0))
                                        .max_size(egui::vec2(
                                            tile_width - 24.0,
                                            tile_height - 12.0,
                                        ));
                                        egui::Button::new(
                                            egui::Atom::layout(contents)
                                                .atom_size(egui::vec2(
                                                    tile_width - 24.0,
                                                    tile_height - 12.0,
                                                ))
                                                .atom_shrink(true)
                                                .atom_max_size(egui::vec2(
                                                    tile_width - 24.0,
                                                    tile_height - 12.0,
                                                )),
                                        )
                                        .selected(selected)
                                    } else {
                                        egui::Button::new(label).selected(selected)
                                    }
                                    .truncate();
                                    let button = if model.drag.as_ref().is_some_and(|drag| {
                                        drag.dragging
                                            && drag.target.as_ref().is_some_and(|target| {
                                                path_equals(&target.to_string_lossy(), &image.path)
                                            })
                                    }) {
                                        button.stroke(Stroke::new(
                                            3.0,
                                            theme::palette(ui.ctx()).drag_target,
                                        ))
                                    } else {
                                        button
                                    };
                                    ui.add_sized(egui::vec2(tile_width, tile_height), button)
                                })
                                .inner
                                .on_hover_text(hover);
                            response.widget_info(|| {
                                egui::WidgetInfo::selected(
                                    egui::WidgetType::Button,
                                    true,
                                    selected,
                                    accessible_label.clone(),
                                )
                            });
                            let primary_pressed = ui.input(|input| {
                                input.pointer.button_pressed(egui::PointerButton::Primary)
                                    && !input.pointer.button_released(egui::PointerButton::Primary)
                            });
                            if response.hovered() && primary_pressed && !response.clicked() {
                                if let Some(pointer) = response.interact_pointer_pos() {
                                    actions.push(Action::StartDrag {
                                        source: image.path.clone().into(),
                                        pointer: Point {
                                            x: pointer.x as f64,
                                            y: pointer.y as f64,
                                        },
                                    });
                                }
                            }
                            if model.drag.is_some()
                                && ui.rect_contains_pointer(response.rect)
                                && model.drag.as_ref().is_some_and(|drag| {
                                    !drag.sources.iter().any(|source| {
                                        path_equals(&source.to_string_lossy(), &image.path)
                                    })
                                })
                            {
                                hovered_target = Some(PathBuf::from(&image.path));
                            }
                            let scope = context_action_scope(model, &image.path);
                            response.context_menu(|ui| {
                                if !selected {
                                    actions.push(Action::SelectImage {
                                        path: image.path.clone().into(),
                                        gesture: SelectionGesture::Replace,
                                    });
                                }
                                let open = ui.button("開啟檢視");
                                if open.clicked() {
                                    actions.push(Action::OpenViewer(image.path.clone().into()));
                                    ui.close();
                                }
                                let reveal = ui.button("在檔案管理器中顯示");
                                if reveal.clicked() {
                                    actions.push(Action::RevealPath(image.path.clone().into()));
                                    ui.close();
                                }
                                let rename =
                                    ui.add_enabled(scope.len() == 1, egui::Button::new("重新命名"));
                                if rename.clicked() {
                                    actions.push(Action::OpenRename);
                                    ui.close();
                                }
                                let trash = ui.button(format!("移至回收筒（{} 張）", scope.len()));
                                if trash.clicked() {
                                    actions.push(Action::RequestTrash);
                                    ui.close();
                                }
                                let menu_has_focus = ui.memory(|memory| {
                                    [open.id, reveal.id, rename.id, trash.id]
                                        .into_iter()
                                        .any(|id| memory.has_focus(id))
                                });
                                if !menu_has_focus {
                                    open.request_focus();
                                }
                            });
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
        if model.drag.as_ref().is_some_and(|drag| drag.dragging) {
            let pointer = ui.input(|input| input.pointer.interact_pos());
            if let Some(pointer) = pointer {
                let clip = ui.clip_rect();
                let step = edge_autoscroll_step(pointer.y, clip.top(), clip.bottom());
                if step != 0.0 {
                    ui.scroll_with_delta(egui::vec2(0.0, -step));
                    ui.ctx().request_repaint();
                }
            }
        }
    });
    if model.drag.is_some() {
        let (pointer, released, down) = ui.input(|input| {
            (
                input.pointer.interact_pos(),
                input.pointer.any_released(),
                input.pointer.any_down(),
            )
        });
        if let Some(pointer) = pointer {
            actions.push(Action::UpdateDrag {
                pointer: Point {
                    x: pointer.x as f64,
                    y: pointer.y as f64,
                },
                target: hovered_target,
            });
        }
        if released {
            actions.push(Action::FinishDrag);
        } else if !down {
            actions.push(Action::CancelDrag);
        }
    }
}

fn gallery_tile_height(tile_width: f32) -> f32 {
    gallery_thumbnail_size(tile_width) + 46.0
}

fn gallery_thumbnail_size(tile_width: f32) -> f32 {
    (tile_width - 24.0).max(48.0)
}

fn cover_uv(source_size: egui::Vec2) -> egui::Rect {
    if source_size.x <= 0.0 || source_size.y <= 0.0 {
        return egui::Rect::from_min_max(egui::Pos2::ZERO, egui::pos2(1.0, 1.0));
    }
    let aspect = source_size.x / source_size.y;
    if aspect > 1.0 {
        let width = 1.0 / aspect;
        let margin = (1.0 - width) / 2.0;
        egui::Rect::from_min_max(egui::pos2(margin, 0.0), egui::pos2(1.0 - margin, 1.0))
    } else {
        let height = aspect;
        let margin = (1.0 - height) / 2.0;
        egui::Rect::from_min_max(egui::pos2(0.0, margin), egui::pos2(1.0, 1.0 - margin))
    }
}

fn scroll_offset_for_row(
    current_offset: f32,
    viewport_height: f32,
    row_stride: f32,
    row: usize,
) -> f32 {
    let row_top = row as f32 * row_stride;
    let row_bottom = row_top + row_stride;
    if row_top < current_offset {
        row_top
    } else if row_bottom > current_offset + viewport_height {
        (row_bottom - viewport_height).max(0.0)
    } else {
        current_offset
    }
}

fn context_action_scope(model: &AppModel, clicked_path: &str) -> Vec<std::path::PathBuf> {
    if model
        .selection
        .ordered_paths
        .iter()
        .any(|path| path_equals(&path.to_string_lossy(), clicked_path))
    {
        model.selection.ordered_paths.clone()
    } else {
        vec![clicked_path.into()]
    }
}

fn danger_button(ui: &mut egui::Ui, label: &str) -> egui::Response {
    let danger = theme::palette(ui.ctx()).danger;
    ui.add(egui::Button::new(RichText::new(label).color(danger)).stroke(Stroke::new(2.0, danger)))
}

fn show_dialog(model: &AppModel, ctx: &egui::Context, actions: &mut Vec<Action>) {
    let Some(dialog) = &model.dialog else {
        return;
    };
    let response = egui::Modal::new(egui::Id::new("piclens-dialog")).show(ctx, |ui| {
        ui.set_min_width(360.0);
        match dialog {
            DialogState::Rename { source, basename } => {
                ui.heading("重新命名圖片");
                ui.label("只修改檔名；副檔名會保留。");
                let mut draft = basename.clone();
                let previous_draft = draft.clone();
                let response = ui.add(
                    egui::TextEdit::singleline(&mut draft)
                        .id(rename_focus_id())
                        .hint_text("輸入新檔名"),
                );
                response.widget_info(|| {
                    let mut info = egui::WidgetInfo::text_edit(
                        true,
                        &previous_draft,
                        &draft,
                        "輸入新檔名",
                    );
                    info.label = Some("新檔名".into());
                    info
                });
                if response.changed() {
                    actions.push(Action::SetRenameBasename(draft));
                }
                if let Some(extension) = source.extension().and_then(|extension| extension.to_str())
                {
                    ui.label(format!("副檔名：.{extension}"));
                }
                ui.horizontal(|ui| {
                    if ui.button("取消").clicked() {
                        actions.push(Action::CloseDialog);
                    }
                    if ui
                        .add_enabled(!basename.trim().is_empty(), egui::Button::new("重新命名"))
                        .clicked()
                    {
                        actions.push(Action::ConfirmRename);
                    }
                });
            }
            DialogState::TrashConfirmation { paths } => {
                ui.heading("移至回收筒");
                ui.label(format!(
                    "將 {} 張圖片移至作業系統回收筒。取消不會修改檔案。",
                    paths.len()
                ));
                ui.horizontal(|ui| {
                    if ui.button("取消").clicked() {
                        actions.push(Action::CloseDialog);
                    }
                    if danger_button(ui, "移至回收筒").clicked() {
                        actions.push(Action::ConfirmTrash);
                    }
                });
            }
            DialogState::ConversionConfirmation { kind, paths } => {
                ui.heading(conversion_label(*kind));
                ui.label(conversion_confirmation(*kind, paths.len()));
                ui.horizontal(|ui| {
                    if ui.button("取消").clicked() {
                        actions.push(Action::CloseDialog);
                    }
                    if ui.button("開始轉換").clicked() {
                        actions.push(Action::ConfirmConversion);
                    }
                });
            }
            DialogState::CleanupConfirmation { paths } => {
                ui.heading("清除同名格式");
                ui.label(format!(
                    "將檢查目前結果的 {} 張圖片。JPG/JPEG 與 WebP 會保留；其他同名格式會移至作業系統回收筒。取消不會修改檔案。",
                    paths.len()
                ));
                ui.horizontal(|ui| {
                    if ui.button("取消").clicked() {
                        actions.push(Action::CloseDialog);
                    }
                    if danger_button(ui, "開始清除").clicked() {
                        actions.push(Action::ConfirmCleanup);
                    }
                });
            }
            DialogState::DropRenameConfirmation { plan } => {
                ui.heading("依目標重新命名");
                ui.label(format!(
                    "確認後才會重新命名 {} 張圖片。目標衝突會略過，不會覆寫檔案。",
                    plan.total
                ));
                egui::ScrollArea::vertical()
                    .max_height(260.0)
                    .show(ui, |ui| {
                        for item in &plan.items {
                            let source = file_name(&item.source_path);
                            let target = file_name(&item.target_path);
                            let suffix = if item.should_skip { "（略過）" } else { "" };
                            ui.label(format!("{source} → {target}{suffix}"));
                        }
                    });
                ui.horizontal(|ui| {
                    if ui.button("取消").clicked() {
                        actions.push(Action::CloseDialog);
                    }
                    if ui.button("確認重新命名").clicked() {
                        actions.push(Action::ConfirmDropRename);
                    }
                });
            }
            DialogState::Progress { title, message } => {
                ui.heading(title);
                ui.label(message);
                if ui.button("取消").clicked() {
                    actions.push(Action::CancelFileOperation);
                }
            }
            DialogState::BatchResult(result) => {
                ui.heading("檔案操作結果");
                ui.label(format!(
                    "共 {}；成功 {}；略過 {}；取消 {}；失敗 {}",
                    result.total(),
                    result.succeeded(),
                    result.skipped(),
                    result.canceled(),
                    result.failed()
                ));
                egui::ScrollArea::vertical()
                    .max_height(300.0)
                    .show(ui, |ui| {
                        for item in &result.items {
                            let name = std::path::Path::new(&item.path)
                                .file_name()
                                .and_then(|name| name.to_str())
                                .unwrap_or(&item.path);
                            ui.label(format!("{}：{}", file_operation_label(item.status), name));
                            if let Some(message) = &item.message {
                                ui.small(message);
                            }
                        }
                    });
                if ui.button("關閉").clicked() {
                    actions.push(Action::CloseDialog);
                }
            }
        }
    });
    if response.should_close() {
        actions.push(if matches!(dialog, DialogState::Progress { .. }) {
            Action::CancelFileOperation
        } else {
            Action::CloseDialog
        });
    }
}

fn file_operation_label(status: FileOperationStatus) -> &'static str {
    match status {
        FileOperationStatus::Converted => "已轉換",
        FileOperationStatus::Trashed => "已移至回收筒",
        FileOperationStatus::Renamed => "已重新命名",
        FileOperationStatus::Canceled => "已取消",
        FileOperationStatus::Skipped => "已略過",
        FileOperationStatus::Failed => "失敗",
    }
}

fn conversion_label(kind: ConversionKind) -> &'static str {
    match kind {
        ConversionKind::Jpg => "轉 JPG",
        ConversionKind::Webp => "轉無損 WebP",
    }
}

fn conversion_confirmation(kind: ConversionKind, count: usize) -> String {
    match kind {
        ConversionKind::Jpg => format!(
            "將目前結果的 {count} 張圖片轉為 JPG。原始檔案會保留，且不會覆寫既有目標檔。取消不會修改檔案。"
        ),
        ConversionKind::Webp => format!(
            "將目前結果的 {count} 張圖片轉為無損 WebP。原始檔案會保留；JPG/JPEG、WebP 與動畫圖片會略過，且不會覆寫既有目標檔。取消不會修改檔案。"
        ),
    }
}

const AUTOSCROLL_EDGE: f32 = 72.0;
const AUTOSCROLL_MAX_STEP: f32 = 48.0;

fn edge_autoscroll_step(pointer_y: f32, top: f32, bottom: f32) -> f32 {
    if bottom <= top || pointer_y < top || pointer_y > bottom {
        return 0.0;
    }
    let top_distance = pointer_y - top;
    if top_distance < AUTOSCROLL_EDGE {
        return -AUTOSCROLL_MAX_STEP * (1.0 - top_distance / AUTOSCROLL_EDGE);
    }
    let bottom_distance = bottom - pointer_y;
    if bottom_distance < AUTOSCROLL_EDGE {
        return AUTOSCROLL_MAX_STEP * (1.0 - bottom_distance / AUTOSCROLL_EDGE);
    }
    0.0
}

fn file_name(path: &str) -> &str {
    std::path::Path::new(path)
        .file_name()
        .and_then(|name| name.to_str())
        .unwrap_or(path)
}

fn paint_drag_preview(model: &AppModel, ctx: &egui::Context) {
    let Some(drag) = &model.drag else {
        return;
    };
    if !drag.dragging {
        return;
    }
    let target = drag
        .target
        .as_ref()
        .and_then(|path| path.file_name())
        .and_then(|name| name.to_str())
        .map(|name| format!("放到 {name}"))
        .unwrap_or_else(|| "移到另一張圖片上".into());
    egui::Area::new(egui::Id::new("piclens-drag-preview"))
        .order(egui::Order::Tooltip)
        .fixed_pos(egui::pos2(
            drag.pointer.x as f32 + 16.0,
            drag.pointer.y as f32 + 16.0,
        ))
        .interactable(false)
        .show(ctx, |ui| {
            Frame::popup(ui.style()).show(ui, |ui| {
                ui.label(RichText::new(format!("{} 張圖片", drag.sources.len())).strong());
                ui.small(target);
            });
        });
}

fn viewer_input(model: &AppModel, ui: &mut egui::Ui, actions: &mut Vec<Action>) {
    let can_step = model
        .viewer
        .as_ref()
        .is_none_or(|viewer| is_fit_view(viewer.zoom.zoom, viewer.zoom.offset));
    let ctrl_shift = egui::Modifiers {
        ctrl: true,
        shift: true,
        ..egui::Modifiers::NONE
    };
    let action = ui.input_mut(|input| {
        if input.consume_key(egui::Modifiers::NONE, egui::Key::Escape) {
            Some(Action::CloseViewer)
        } else if input.consume_key(ctrl_shift, egui::Key::E) {
            Some(Action::RevealViewer)
        } else if input.consume_key(egui::Modifiers::NONE, egui::Key::Delete) {
            Some(Action::RequestTrash)
        } else if input.consume_key(egui::Modifiers::NONE, egui::Key::Plus)
            || input.consume_key(egui::Modifiers::NONE, egui::Key::Equals)
        {
            Some(Action::AdjustViewerZoom(1))
        } else if input.consume_key(egui::Modifiers::NONE, egui::Key::Minus) {
            Some(Action::AdjustViewerZoom(-1))
        } else if input.consume_key(egui::Modifiers::NONE, egui::Key::Num0) {
            Some(Action::ResetViewerZoom)
        } else if can_step && input.consume_key(egui::Modifiers::NONE, egui::Key::PageUp) {
            Some(Action::StepViewer(-1))
        } else if can_step && input.consume_key(egui::Modifiers::NONE, egui::Key::PageDown) {
            Some(Action::StepViewer(1))
        } else if can_step && input.consume_key(egui::Modifiers::NONE, egui::Key::ArrowLeft) {
            Some(Action::StepViewer(-1))
        } else if can_step && input.consume_key(egui::Modifiers::NONE, egui::Key::ArrowRight) {
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
    focus_primary: bool,
) {
    let palette = theme::palette(ui.ctx());
    let Some(viewer) = &model.viewer else {
        ui.label(RichText::new("檢視器狀態無效。").color(palette.viewer_text));
        let response = ui.button("返回圖庫");
        if focus_primary {
            response.request_focus();
        }
        if response.clicked() {
            actions.push(Action::CloseViewer);
        }
        return;
    };
    let Some(current) = viewer.snapshot.current() else {
        ui.label(RichText::new("快照中沒有圖片。").color(palette.viewer_text));
        let response = ui.button("返回圖庫");
        if focus_primary {
            response.request_focus();
        }
        if response.clicked() {
            actions.push(Action::CloseViewer);
        }
        return;
    };

    ui.horizontal_wrapped(|ui| {
        let back = ui.button("返回圖庫");
        if focus_primary {
            back.request_focus();
        }
        if back.clicked() {
            actions.push(Action::CloseViewer);
        }
        ui.separator();
        if ui.button("上一張").clicked() {
            actions.push(Action::StepViewer(-1));
        }
        if ui.button("下一張").clicked() {
            actions.push(Action::StepViewer(1));
        }
        ui.separator();
        if ui.button("縮小").clicked() {
            actions.push(Action::AdjustViewerZoom(-1));
        }
        if ui
            .button(format!("重設 {:.0}%", viewer.zoom.zoom * 100.0))
            .clicked()
        {
            actions.push(Action::ResetViewerZoom);
        }
        if ui.button("放大").clicked() {
            actions.push(Action::AdjustViewerZoom(1));
        }
        ui.separator();
        if ui.button("在檔案總管中顯示").clicked() {
            actions.push(Action::RevealViewer);
        }
    });
    ui.add_space(8.0);
    ui.horizontal(|ui| {
        ui.label(
            RichText::new(format!(
                "{} / {}",
                viewer.snapshot.current_index + 1,
                viewer.snapshot.images.len()
            ))
            .color(palette.secondary),
        );
        ui.separator();
        ui.add(
            egui::Label::new(RichText::new(&current.name).color(palette.viewer_text)).truncate(),
        )
        .on_hover_text(&current.name);
    });
    ui.add_space(12.0);

    let key = ThumbnailKey::from_image(current, 1024);
    materialized.extend(viewer_preview_keys(&viewer.snapshot, images));
    let (canvas, response) =
        ui.allocate_exact_size(ui.available_size(), egui::Sense::click_and_drag());
    response.widget_info(|| {
        egui::WidgetInfo::labeled(egui::WidgetType::Image, true, current.name.clone())
    });
    let painter = ui.painter().with_clip_rect(canvas);
    painter.rect_filled(canvas, 0.0, palette.viewer_canvas);

    if current.is_animated {
        paint_viewer_message(
            &painter,
            canvas,
            "此動畫圖片目前不支援預覽。",
            palette.viewer_text,
        );
    } else if let Some(texture) = images.texture(&key) {
        let texture_size = texture.size_vec2();
        let fit_scale = (canvas.width() / texture_size.x)
            .min(canvas.height() / texture_size.y)
            .max(0.0);
        let image_size = texture_size * fit_scale * viewer.zoom.zoom as f32;
        let center =
            canvas.center() + egui::vec2(viewer.zoom.offset.x as f32, viewer.zoom.offset.y as f32);
        painter.image(
            texture.id(),
            egui::Rect::from_center_size(center, image_size),
            egui::Rect::from_min_max(egui::Pos2::ZERO, egui::pos2(1.0, 1.0)),
            palette.viewer_text,
        );
    } else if let Some(message) = images.failure(&key) {
        paint_viewer_message(
            &painter,
            canvas,
            &format!("圖片載入失敗：{message}"),
            palette.viewer_error,
        );
    } else {
        paint_viewer_message(&painter, canvas, "正在載入圖片…", palette.viewer_text);
    }

    if response.hovered() {
        let scroll_y = ui.input_mut(|input| {
            let scroll_y = input.smooth_scroll_delta.y;
            input.smooth_scroll_delta.y = 0.0;
            scroll_y
        });
        if scroll_y != 0.0 {
            let pointer = response.hover_pos().unwrap_or_else(|| canvas.center());
            actions.push(Action::ZoomViewerAt {
                pointer: Point {
                    x: f64::from(pointer.x),
                    y: f64::from(pointer.y),
                },
                viewport_center: Point {
                    x: f64::from(canvas.center().x),
                    y: f64::from(canvas.center().y),
                },
                delta: if scroll_y > 0.0 { 1 } else { -1 },
            });
        }
    }
    if response.dragged() && viewer.zoom.zoom > 1.01 {
        let delta = ui.input(|input| input.pointer.delta());
        if delta != egui::Vec2::ZERO {
            actions.push(Action::PanViewer(Point {
                x: f64::from(delta.x),
                y: f64::from(delta.y),
            }));
        }
    }
}

fn viewer_preview_keys(
    snapshot: &ImageSequenceSnapshot,
    images: &ThumbnailLoader,
) -> Vec<ThumbnailKey> {
    let Some(current) = snapshot.current().filter(|image| !image.is_animated) else {
        return Vec::new();
    };
    let current_key = ThumbnailKey::from_image(current, 1024);
    let mut keys = vec![current_key.clone()];
    if !images.is_settled(&current_key) {
        return keys;
    }

    let len = snapshot.images.len() as i32;
    for delta in [1, -1] {
        let index = (snapshot.current_index + delta).rem_euclid(len) as usize;
        let neighbor = &snapshot.images[index];
        if neighbor.is_animated {
            continue;
        }
        let key = ThumbnailKey::from_image(neighbor, 1024);
        if keys.contains(&key) {
            continue;
        }
        let settled = images.is_settled(&key);
        keys.push(key);
        if !settled {
            break;
        }
    }
    keys
}

fn paint_viewer_message(
    painter: &egui::Painter,
    canvas: egui::Rect,
    message: &str,
    color: Color32,
) {
    painter.text(
        canvas.center(),
        egui::Align2::CENTER_CENTER,
        message,
        egui::FontId::proportional(18.0),
        color,
    );
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
    let rows = visible_tree_rows(
        &model.tree_roots,
        &model.tree_children,
        &model.tree_expanded,
    );
    let row_height = ui.spacing().interact_size.y;
    egui::ScrollArea::vertical().show_rows(ui, row_height, rows.len(), |ui, visible| {
        for row in &rows[visible] {
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
                    let response = ui
                        .small_button(label)
                        .on_hover_text(format!("{label} {name}"));
                    response.widget_info(|| {
                        egui::WidgetInfo::labeled(
                            egui::WidgetType::Button,
                            true,
                            format!("{label} {name}"),
                        )
                    });
                    if response.clicked() {
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

fn status_feedback(model: &AppModel, ui: &mut egui::Ui, actions: &mut Vec<Action>) {
    let palette = theme::palette(ui.ctx());
    match &model.backend {
        Loadable::Idle | Loadable::Loading => {
            ui.horizontal(|ui| {
                ui.spinner();
                let response = ui.label("正在啟動背景服務…");
                mark_live(ui, &response, egui::accesskit::Live::Polite);
            });
        }
        Loadable::Ready(()) => {}
        Loadable::Failed(message) => {
            ui.horizontal_wrapped(|ui| {
                let response = ui.label(RichText::new(message).color(palette.danger).strong());
                mark_live(ui, &response, egui::accesskit::Live::Assertive);
                if ui.button("再試一次").clicked() {
                    actions.push(Action::RetryBackendProbe);
                }
            });
        }
    }
    if let Some(notice) = &model.notice {
        ui.horizontal_wrapped(|ui| {
            let response = ui.label(notice);
            mark_live(ui, &response, egui::accesskit::Live::Polite);
            if ui.small_button("關閉").clicked() {
                actions.push(Action::DismissStatus);
            }
        });
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
    fn rename_dialog_requires_an_explicit_action() {
        let mut model = crate::demo::loaded_library();
        model.dialog = Some(DialogState::Rename {
            source: "C:/fixture/image2.png".into(),
            basename: "image2".into(),
        });
        let mut harness = Harness::new_ui_state(
            move |ui, actions: &mut Vec<Action>| {
                show(&model, &ThumbnailLoader::default(), ui, actions);
            },
            Vec::new(),
        );
        harness.run();

        let _ = harness.get_by_label("重新命名圖片");
        assert!(harness.state().is_empty());
        harness.get_by_label("取消").click();
        harness.run();
        assert_eq!(harness.state().as_slice(), [Action::CloseDialog]);
    }

    #[test]
    fn image_press_starts_pending_drag_without_changing_selection() {
        let model = crate::demo::loaded_library();
        let mut harness = Harness::new_ui_state(
            move |ui, actions: &mut Vec<Action>| {
                show(&model, &ThumbnailLoader::default(), ui, actions);
            },
            Vec::new(),
        );
        harness.run();
        let center = harness.get_by_label_contains("image2.png").rect().center();

        harness.drag_at(center);
        harness.run();
        assert!(harness
            .state()
            .iter()
            .any(|action| matches!(action, Action::StartDrag { .. })));
        assert!(!harness
            .state()
            .iter()
            .any(|action| matches!(action, Action::SelectImage { .. })));
        harness.drop_at(center);
    }

    #[test]
    fn drag_autoscroll_is_bounded_and_zero_in_the_middle() {
        assert_eq!(edge_autoscroll_step(0.0, 0.0, 400.0), -48.0);
        assert_eq!(edge_autoscroll_step(36.0, 0.0, 400.0), -24.0);
        assert_eq!(edge_autoscroll_step(200.0, 0.0, 400.0), 0.0);
        assert_eq!(edge_autoscroll_step(364.0, 0.0, 400.0), 24.0);
        assert_eq!(edge_autoscroll_step(400.0, 0.0, 400.0), 48.0);
    }

    #[test]
    fn keyboard_scroll_offset_keeps_the_target_row_in_view() {
        assert_eq!(scroll_offset_for_row(100.0, 300.0, 80.0, 0), 0.0);
        assert_eq!(scroll_offset_for_row(100.0, 300.0, 80.0, 3), 100.0);
        assert_eq!(scroll_offset_for_row(100.0, 300.0, 80.0, 8), 420.0);
    }

    fn dragging_model() -> AppModel {
        let mut model = crate::demo::loaded_library();
        model.drag = Some(crate::model::DragSession {
            sources: vec!["C:/fixture/image2.png".into()],
            origin: Point { x: 10.0, y: 10.0 },
            pointer: Point { x: 30.0, y: 30.0 },
            target: None,
            dragging: true,
            replaces_selection: false,
        });
        model
    }

    #[test]
    fn escape_cancels_the_drag_session() {
        let model = dragging_model();
        let mut harness = Harness::new_ui_state(
            move |ui, actions: &mut Vec<Action>| {
                show(&model, &ThumbnailLoader::default(), ui, actions);
            },
            Vec::new(),
        );
        harness.drag_at(egui::pos2(1.0, 1.0));
        harness.key_press(egui::Key::Escape);
        harness.run();

        assert!(harness.state().contains(&Action::CancelDrag));
    }

    #[test]
    fn cover_uv_center_crops_landscape_and_portrait_images() {
        assert_eq!(
            cover_uv(egui::vec2(200.0, 100.0)),
            egui::Rect::from_min_max(egui::pos2(0.25, 0.0), egui::pos2(0.75, 1.0))
        );
        assert_eq!(
            cover_uv(egui::vec2(100.0, 200.0)),
            egui::Rect::from_min_max(egui::pos2(0.0, 0.25), egui::pos2(1.0, 0.75))
        );
        assert_eq!(
            cover_uv(egui::vec2(100.0, 100.0)),
            egui::Rect::from_min_max(egui::Pos2::ZERO, egui::pos2(1.0, 1.0))
        );
    }

    #[test]
    fn capture_lost_cancels_the_drag_session() {
        let model = dragging_model();
        let mut harness = Harness::new_ui_state(
            move |ui, actions: &mut Vec<Action>| {
                show(&model, &ThumbnailLoader::default(), ui, actions);
            },
            Vec::new(),
        );
        harness.run();

        assert!(harness.state().contains(&Action::CancelDrag));
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
            zoom: piclens_domain::reset_zoom_state(),
        });
        let mut harness = Harness::new_ui_state(
            move |ui, materialized: &mut Vec<ThumbnailKey>| {
                let mut actions = Vec::new();
                *materialized = show(&model, &ThumbnailLoader::default(), ui, &mut actions);
            },
            Vec::new(),
        );
        harness.run();

        assert!(harness
            .get_by_role_and_label(egui::accesskit::Role::Button, "返回圖庫")
            .is_focused());
        let _ = harness.get_by_role_and_label(egui::accesskit::Role::Image, "image2.png");
        assert_eq!(harness.state().len(), 1);
        assert_eq!(harness.state()[0].longest_edge, 1024);
    }

    #[test]
    fn viewer_previews_load_current_then_next_then_previous_within_three_textures() {
        let model = crate::demo::large_library(3);
        let query = model.library_query.as_ref().unwrap();
        let snapshot = ImageSequenceSnapshot::from_visible(
            query.folder_path.clone(),
            query.include_subfolders,
            query.sort,
            &model.visible_items,
            "C:/fixture/image1.png",
        )
        .unwrap();
        let mut loader = ThumbnailLoader::default();
        let ctx = egui::Context::default();
        let ready = || crate::images::DecodedThumbnail {
            width: 1,
            height: 1,
            rgba: vec![255, 255, 255, 255],
        };

        let current = viewer_preview_keys(&snapshot, &loader);
        assert_eq!(current.len(), 1);
        assert!(current[0].source.ends_with("image1.png"));
        let request = loader.sync_materialized(current, 1).unwrap().remove(0);
        assert!(loader.handle_result(&request, Ok(ready()), &ctx));

        let current_and_next = viewer_preview_keys(&snapshot, &loader);
        assert_eq!(current_and_next.len(), 2);
        assert!(current_and_next[1].source.ends_with("image2.png"));
        let request = loader
            .sync_materialized(current_and_next, 1)
            .unwrap()
            .remove(0);
        assert!(loader.handle_result(&request, Ok(ready()), &ctx));

        let all = viewer_preview_keys(&snapshot, &loader);
        assert_eq!(all.len(), 3);
        assert!(all[2].source.ends_with("image0.png"));
        assert!(all.iter().all(|key| key.longest_edge == 1024));
    }

    #[test]
    fn zoomed_viewer_blocks_arrow_navigation_but_keeps_controls_and_escape() {
        let mut model = crate::demo::loaded_library();
        let query = model.library_query.as_ref().unwrap();
        let snapshot = ImageSequenceSnapshot::from_visible(
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
            zoom: piclens_domain::ZoomState {
                zoom: piclens_domain::ZOOM_STEP,
                offset: Point::default(),
            },
        });
        let mut harness = Harness::new_ui_state(
            move |ui, actions: &mut Vec<Action>| {
                show(&model, &ThumbnailLoader::default(), ui, actions);
            },
            Vec::new(),
        );
        harness.run();

        harness.key_press(egui::Key::ArrowRight);
        harness.run();
        assert!(harness.state().is_empty());

        harness.get_by_label("放大").click();
        harness.run();
        harness.key_press(egui::Key::Escape);
        harness.run();
        assert_eq!(
            harness.state().as_slice(),
            [Action::AdjustViewerZoom(1), Action::CloseViewer]
        );
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
        let mut harness = Harness::builder()
            .with_size(egui::vec2(1280.0, 800.0))
            .build_ui_state(
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
    fn folder_tree_materializes_only_visible_rows() {
        let mut model = crate::demo::loaded_library();
        model.tree_roots = (0..10_000)
            .map(|index| format!("C:/tree/root-{index:05}"))
            .collect();
        let mut harness = Harness::builder()
            .with_size(egui::vec2(1280.0, 800.0))
            .build_ui(move |ui| {
                let mut actions = Vec::new();
                show(&model, &ThumbnailLoader::default(), ui, &mut actions);
            });
        harness.run();

        let materialized = harness.query_all_by_label_contains("root-").count();
        assert!(materialized > 0);
        assert!(materialized < 100, "materialized {materialized} tree rows");
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
    fn keyboard_scroll_materializes_a_target_outside_the_initial_viewport() {
        let mut model = crate::demo::large_library(10_000);
        model.gallery_scroll_target = Some(9_999);
        let mut harness = Harness::builder()
            .with_size(egui::vec2(1280.0, 800.0))
            .build_ui_state(
                move |ui, actions: &mut Vec<Action>| {
                    show(&model, &ThumbnailLoader::default(), ui, actions);
                },
                Vec::new(),
            );
        harness.run();

        let _ = harness.get_by_label_contains("image9999.png");
        assert!(harness.state().contains(&Action::ClearGalleryScrollTarget));
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

        let selection_actions = harness
            .state()
            .iter()
            .filter(|action| matches!(action, Action::SelectImage { .. }))
            .cloned()
            .collect::<Vec<_>>();
        assert_eq!(
            selection_actions,
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

    #[test]
    fn context_scope_keeps_selected_group_and_replaces_unselected_target() {
        let mut model = crate::demo::large_library(3);
        model.selection.ordered_paths = vec![
            "C:/fixture/image0.png".into(),
            "C:/fixture/image1.png".into(),
        ];

        assert_eq!(
            context_action_scope(&model, "C:/fixture/image0.png"),
            model.selection.ordered_paths
        );
        assert_eq!(
            context_action_scope(&model, "C:/fixture/image2.png"),
            vec![std::path::PathBuf::from("C:/fixture/image2.png")]
        );
    }

    #[test]
    fn gallery_shortcuts_match_selection_and_search_baseline() {
        let model = crate::demo::loaded_library();
        let mut harness = Harness::new_ui_state(
            move |ui, actions: &mut Vec<Action>| {
                show(&model, &ThumbnailLoader::default(), ui, actions);
            },
            Vec::new(),
        );
        harness.run();

        harness.key_press(egui::Key::ArrowRight);
        harness.run();
        assert_eq!(
            harness.state().as_slice(),
            [Action::MoveGallerySelection(1)]
        );

        harness.state_mut().clear();
        harness.key_press_modifiers(
            egui::Modifiers {
                ctrl: true,
                ..Default::default()
            },
            egui::Key::F,
        );
        harness.run();
        assert!(harness.state().is_empty());
        assert!(harness
            .get_by_role_and_label(egui::accesskit::Role::TextInput, "搜尋圖片")
            .is_focused());

        harness.key_press_modifiers(
            egui::Modifiers {
                ctrl: true,
                ..Default::default()
            },
            egui::Key::A,
        );
        harness.run();
        assert!(!harness.state().contains(&Action::SelectAllVisible));
    }

    #[test]
    fn primary_controls_have_accessible_names_roles_and_states() {
        let model = crate::demo::loaded_library();
        let mut harness = Harness::new_ui(move |ui| {
            let mut actions = Vec::new();
            show(&model, &ThumbnailLoader::default(), ui, &mut actions);
        });
        harness.run();

        let _ = harness.get_by_role_and_label(egui::accesskit::Role::TextInput, "搜尋圖片");
        let _ = harness.get_by_role_and_label(egui::accesskit::Role::Button, "選擇資料夾");
        let previous = harness.get_by_role_and_label(egui::accesskit::Role::Button, "上一頁");
        assert!(egui_kittest::kittest::NodeT::accesskit_node(&previous).is_disabled());
        let image = harness.get_by_label_contains("image2.png");
        assert!(egui_kittest::kittest::NodeT::accesskit_node(&image)
            .data()
            .supports_action(egui::accesskit::Action::Click));
        assert_eq!(
            egui_kittest::kittest::NodeT::accesskit_node(&image).toggled(),
            Some(egui::accesskit::Toggled::False)
        );
    }

    #[test]
    fn context_menu_focuses_its_primary_action() {
        let model = crate::demo::loaded_library();
        let mut harness = Harness::new_ui(move |ui| {
            let mut actions = Vec::new();
            show(&model, &ThumbnailLoader::default(), ui, &mut actions);
        });
        harness.run();

        harness
            .get_by_label_contains("image2.png")
            .click_secondary();
        harness.run();

        assert!(harness.get_by_label("開啟檢視").is_focused());
    }

    #[test]
    fn dynamic_statuses_expose_accesskit_live_semantics() {
        let mut model = crate::demo::loaded_library();
        model.backend = Loadable::Failed("背景服務測試錯誤".into());
        model.notice = Some("測試通知".into());
        let mut harness = Harness::new_ui(move |ui| {
            let mut actions = Vec::new();
            show(&model, &ThumbnailLoader::default(), ui, &mut actions);
        });
        harness.run();

        let error = harness.get_by_label("背景服務測試錯誤");
        assert_eq!(
            egui_kittest::kittest::NodeT::accesskit_node(&error).live(),
            egui::accesskit::Live::Assertive
        );
        let notice = harness.get_by_label("測試通知");
        assert_eq!(
            egui_kittest::kittest::NodeT::accesskit_node(&notice).live(),
            egui::accesskit::Live::Polite
        );
    }

    #[test]
    fn tab_focus_follows_the_visible_primary_control_order() {
        let model = crate::demo::loaded_library();
        let mut harness = Harness::new_ui(move |ui| {
            let mut actions = Vec::new();
            show(&model, &ThumbnailLoader::default(), ui, &mut actions);
        });

        harness.key_press(egui::Key::Tab);
        harness.run();
        assert!(harness.get_by_label("選擇資料夾").is_focused());

        harness.key_press(egui::Key::Tab);
        harness.run();
        assert!(harness.get_by_label("重新整理").is_focused());

        harness.key_press(egui::Key::Tab);
        harness.run();
        assert!(harness
            .get_by_role_and_label(egui::accesskit::Role::TextInput, "搜尋圖片")
            .is_focused());

        harness.key_press(egui::Key::Tab);
        harness.run();
        assert!(harness
            .get_by_role_and_label(egui::accesskit::Role::ComboBox, "排序")
            .is_focused());
    }

    #[test]
    fn dialog_escape_closes_only_the_dialog_above_the_viewer() {
        let mut model = crate::demo::loaded_library();
        let query = model.library_query.as_ref().unwrap();
        let snapshot = ImageSequenceSnapshot::from_visible(
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
            zoom: piclens_domain::reset_zoom_state(),
        });
        model.dialog = Some(DialogState::TrashConfirmation {
            paths: vec!["C:/fixture/image2.png".into()],
        });
        let mut harness = Harness::new_ui_state(
            move |ui, actions: &mut Vec<Action>| {
                show(&model, &ThumbnailLoader::default(), ui, actions);
            },
            Vec::new(),
        );
        harness.run();

        harness.key_press(egui::Key::Escape);
        harness.run();

        assert_eq!(harness.state().as_slice(), [Action::CloseDialog]);
    }

    #[test]
    fn minimum_layout_hides_sidebar_and_keeps_long_traditional_chinese_controls_visible() {
        let mut model = crate::demo::loaded_library();
        let long_name = "這是一個很長的繁體中文資料夾名稱，用來驗證最小視窗寬度時文字可以換行且不會遮住主要操作";
        model.current_folder = Some(format!("C:/fixture/{long_name}").into());
        model.tree_roots = vec!["C:/tree-root".into()];
        if let Some(ListItem::Image(image)) = model
            .visible_items
            .iter_mut()
            .find(|item| item.as_image().is_some())
        {
            image.name = format!("{long_name}.png");
        }
        let long_image_label = format!("{long_name}.png");
        let tile_width = model.thumbnail_size as f32;
        let mut harness = Harness::builder()
            .with_size(egui::vec2(800.0, 600.0))
            .build_ui(move |ui| {
                let mut actions = Vec::new();
                show(&model, &ThumbnailLoader::default(), ui, &mut actions);
            });
        crate::theme::install(&harness.ctx);
        harness.run();

        assert!(harness.query_by_label("資料夾").is_none());
        for label in ["選擇資料夾", "搜尋圖片", "含子資料夾"] {
            let rect = harness.get_by_label(label).rect();
            assert!(
                rect.min.x >= 0.0 && rect.max.x <= 800.0,
                "{label}: {rect:?}"
            );
            assert!(
                rect.min.y >= 0.0 && rect.max.y <= 600.0,
                "{label}: {rect:?}"
            );
        }
        assert!(
            harness
                .query_all_by_label_contains("這是一個很長的繁體中文資料夾名稱")
                .count()
                >= 1
        );
        let image = harness.get_by_role_and_label(egui::accesskit::Role::Button, &long_image_label);
        let rect = image.rect();
        assert!(rect.min.x >= 0.0 && rect.max.x <= 800.0, "{rect:?}");
        assert!(rect.width() <= tile_width + f32::EPSILON, "{rect:?}");
    }

    #[test]
    fn textured_gallery_tile_keeps_its_configured_width_with_a_long_filename() {
        let mut model = crate::demo::loaded_library();
        let long_name =
            "這是一個非常長的圖片檔名，用來確認縮圖載入後不會把固定寬度的圖庫欄位撐開.png";
        let image = model
            .visible_items
            .iter_mut()
            .find_map(|item| match item {
                ListItem::Image(image) => Some(image),
                ListItem::Folder(_) => None,
            })
            .expect("demo library has an image");
        image.name = long_name.into();
        let key = ThumbnailKey::from_image(image, model.thumbnail_size as u32);
        let tile_width = model.thumbnail_size as f32;
        let mut harness = Harness::builder()
            .with_size(egui::vec2(800.0, 600.0))
            .build_ui(move |ui| {
                let mut images = ThumbnailLoader::default();
                let request = images
                    .sync_materialized(vec![key.clone()], 1)
                    .expect("new materialized request")
                    .pop()
                    .expect("one thumbnail request");
                assert!(images.handle_result(
                    &request,
                    Ok(crate::images::DecodedThumbnail {
                        width: 4,
                        height: 4,
                        rgba: vec![255; 4 * 4 * 4],
                    }),
                    ui.ctx(),
                ));
                let mut actions = Vec::new();
                show(&model, &images, ui, &mut actions);
            });
        crate::theme::install(&harness.ctx);
        harness.run();

        let tile = harness.get_by_role_and_label(egui::accesskit::Role::Button, long_name);
        assert!(tile.rect().width() <= tile_width + f32::EPSILON);
    }

    #[test]
    fn common_display_scales_render_the_library_at_supported_sizes() {
        for (size, scale) in [
            (egui::vec2(1280.0, 800.0), 1.0),
            (egui::vec2(1280.0, 800.0), 1.25),
            (egui::vec2(800.0, 600.0), 1.5),
            (egui::vec2(800.0, 600.0), 2.0),
        ] {
            let model = crate::demo::loaded_library();
            let mut harness = Harness::builder()
                .with_size(size)
                .with_pixels_per_point(scale)
                .build_ui(move |ui| {
                    let mut actions = Vec::new();
                    show(&model, &ThumbnailLoader::default(), ui, &mut actions);
                });
            crate::theme::install(&harness.ctx);
            harness.run();

            let _ = harness.get_by_label("搜尋圖片");
            let _ = harness.get_by_label_contains("image2.png");
        }
    }

    #[test]
    fn headless_suite_covers_loading_error_and_all_dialog_kinds() {
        let mut loading = crate::demo::loaded_library();
        loading.library = Loadable::Loading;
        let mut harness = Harness::new_ui(move |ui| {
            let mut actions = Vec::new();
            show(&loading, &ThumbnailLoader::default(), ui, &mut actions);
        });
        harness.step();
        let _ = harness.get_by_label("正在載入圖庫…");

        let mut failed = crate::demo::loaded_library();
        failed.library = Loadable::Failed("無法讀取測試資料夾；請檢查權限後重試。".into());
        let mut harness = Harness::new_ui(move |ui| {
            let mut actions = Vec::new();
            show(&failed, &ThumbnailLoader::default(), ui, &mut actions);
        });
        harness.run();
        let _ = harness.get_by_label("無法讀取測試資料夾；請檢查權限後重試。");
        let _ = harness.get_by_label("重新載入");

        let dialogs = [
            (
                DialogState::Rename {
                    source: "C:/fixture/image.png".into(),
                    basename: "image".into(),
                },
                "重新命名圖片",
            ),
            (
                DialogState::TrashConfirmation {
                    paths: vec!["C:/fixture/image.png".into()],
                },
                "移至回收筒",
            ),
            (
                DialogState::ConversionConfirmation {
                    kind: ConversionKind::Jpg,
                    paths: vec!["C:/fixture/image.png".into()],
                },
                "轉 JPG",
            ),
            (
                DialogState::CleanupConfirmation {
                    paths: vec!["C:/fixture/image.png".into()],
                },
                "清除同名格式",
            ),
            (
                DialogState::DropRenameConfirmation {
                    plan: piclens_domain::DropTargetBatchRenamePlan::default(),
                },
                "依目標重新命名",
            ),
            (
                DialogState::Progress {
                    title: "轉檔中".into(),
                    message: "正在處理 1 張圖片…".into(),
                },
                "轉檔中",
            ),
            (
                DialogState::BatchResult(piclens_domain::FileOperationBatchResult::default()),
                "檔案操作結果",
            ),
        ];
        for (dialog, heading) in dialogs {
            let mut model = crate::demo::loaded_library();
            model.dialog = Some(dialog);
            let mut harness = Harness::new_ui(move |ui| {
                let mut actions = Vec::new();
                show(&model, &ThumbnailLoader::default(), ui, &mut actions);
            });
            harness.run();
            assert!(harness.query_all_by_label(heading).count() >= 1);
        }
    }
}
