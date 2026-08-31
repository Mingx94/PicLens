//! Library grid tiles and empty state.

use std::time::Duration;

use crate::drag_rename::{drag_target, is_dragging};
use gpui::prelude::FluentBuilder as _;
use gpui::*;
use gpui_component::button::{Button, ButtonVariants as _};
use gpui_component::menu::ContextMenuExt;
use gpui_component::scroll::Scrollbar;
use gpui_component::{h_flex, v_flex, Icon, IconName};

use super::PicLensApp;
use crate::actions::{OpenViewer, RenameSelection, RevealInFileManager, TrashSelection};
use crate::interaction::SelectionGesture;
use crate::theme::Theme;

impl PicLensApp {
    fn activate_gallery_item_from_accessibility(
        &mut self,
        path: String,
        is_folder: bool,
        cx: &mut Context<Self>,
    ) {
        if is_folder {
            self.open_folder(path, false, false, true, cx);
        } else {
            self.select_path(&path, SelectionGesture::Replace);
            cx.notify();
        }
    }

    fn tile_preview(
        &self,
        theme: Theme,
        path: &str,
        is_folder: bool,
        animated: bool,
        size: f32,
    ) -> AnyElement {
        if is_folder {
            return div()
                .size(px(size))
                .flex()
                .items_center()
                .justify_center()
                .rounded(px(8.))
                .bg(theme.tile_frame)
                .child(
                    Icon::new(IconName::Folder)
                        .size(rems(4.))
                        .text_color(theme.accent),
                )
                .into_any_element();
        }
        if animated {
            return div()
                .size(px(size))
                .flex()
                .items_center()
                .justify_center()
                .rounded(px(8.))
                .bg(theme.tile_frame)
                .child(div().text_xs().text_color(theme.muted_text).child("動畫"))
                .into_any_element();
        }
        if let Some(cache) = self.thumbs.get(path) {
            return div()
                .size(px(size))
                .rounded(px(8.))
                .overflow_hidden()
                .bg(theme.tile_frame)
                .child(
                    img(cache.clone())
                        .object_fit(ObjectFit::Cover)
                        .size(px(size))
                        .with_animation(
                            format!("thumbnail-ready:{path}"),
                            Animation::new(Duration::from_millis(110))
                                .with_easing(ease_out_quint()),
                            |this, delta| this.opacity(delta),
                        ),
                )
                .into_any_element();
        }
        if self.thumb_pending.contains(path) {
            return div()
                .size(px(size))
                .flex()
                .items_center()
                .justify_center()
                .rounded(px(8.))
                .bg(theme.tile_frame)
                .child(div().text_xs().text_color(theme.muted_text).child("…"))
                .into_any_element();
        }
        div()
            .size(px(size))
            .flex()
            .items_center()
            .justify_center()
            .rounded(px(8.))
            .bg(theme.tile_frame)
            .child(Icon::new(IconName::File).text_color(theme.muted_text))
            .into_any_element()
    }

    pub(super) fn render_gallery(&self, theme: Theme, cx: &mut Context<Self>) -> AnyElement {
        if self.visible.is_empty() {
            return v_flex()
                .size_full()
                .items_center()
                .justify_center()
                .gap_4()
                .child(
                    div()
                        .size(px(72.))
                        .rounded_full()
                        .bg(theme.accent_soft)
                        .flex()
                        .items_center()
                        .justify_center()
                        .child(Icon::new(IconName::FolderOpen).text_color(theme.accent)),
                )
                .child(
                    div()
                        .text_xl()
                        .font_weight(FontWeight::SEMIBOLD)
                        .text_color(theme.primary_text)
                        .child(if self.folder_path.is_some() {
                            "此資料夾沒有符合的項目"
                        } else {
                            "開始整理圖片"
                        }),
                )
                .child(div().text_sm().text_color(theme.secondary_text).child(
                    if self.folder_path.is_some() {
                        "試試清除搜尋，或切換「含子資料夾」。"
                    } else {
                        "選擇本機資料夾後即可瀏覽縮圖、排序與批次整理。"
                    },
                ))
                .child(
                    Button::new("empty-open")
                        .primary()
                        .label("開啟資料夾")
                        .on_click(cx.listener(|this, _, window, cx| this.pick_folder(window, cx))),
                )
                .into_any_element();
        }

        let entity = cx.entity().downgrade();
        let measure = entity.clone();
        div()
            .id("gallery-scroll")
            .role(Role::ListBox)
            .aria_label("圖庫")
            .relative()
            .size_full()
            .child(
                canvas(
                    move |bounds, _, cx| {
                        if let Some(entity) = measure.upgrade() {
                            entity.update(cx, |this, cx| {
                                this.gallery_bounds = Some(bounds);
                                this.apply_gallery_width(f32::from(bounds.size.width), cx);
                            });
                        }
                    },
                    |_, _, _, _| {},
                )
                .absolute()
                .inset_0(),
            )
            .child(
                list(self.gallery_list.clone(), move |row, _window, cx| {
                    entity
                        .upgrade()
                        .map(|entity| {
                            entity.update(cx, |this, cx| this.render_gallery_row(row, theme, cx))
                        })
                        .unwrap_or_else(|| div().into_any_element())
                })
                .size_full(),
            )
            .child(
                div()
                    .absolute()
                    .inset_0()
                    .child(Scrollbar::vertical(&self.gallery_list)),
            )
            .into_any_element()
    }

    fn render_gallery_row(&self, row: usize, theme: Theme, cx: &mut Context<Self>) -> AnyElement {
        let tile_size = self.thumb_size() as f32;
        let selected_count = self.selected.len();
        let focus = self.focus_handle.clone();
        let cols = self.gallery_columns();
        let start = row.saturating_mul(cols);
        if start >= self.visible.len() {
            return div().into_any_element();
        }
        let end = (start + cols).min(self.visible.len());

        h_flex()
            .id(("grid-row", row))
            .w_full()
            .gap_3()
            .p_1()
            .children((start..end).map(|idx| {
                let item = &self.visible[idx];
                let path = item.path().to_string();
                let name = item.name().to_string();
                let is_folder = item.is_folder();
                let animated = item.as_image().map(|i| i.is_animated).unwrap_or(false);
                let selected = self.selected.contains(&path);
                let preview = self.tile_preview(theme, &path, is_folder, animated, tile_size);
                v_flex()
                    .id(("tile", idx))
                    .w(px(tile_size))
                    .gap_1()
                    .child(self.item_surface(
                        theme,
                        ("tile-surface", idx),
                        name.clone(),
                        idx + 1,
                        self.visible.len(),
                        selected,
                        is_folder,
                        selected_count,
                        path.clone(),
                        preview,
                        focus.clone(),
                        cx,
                    ))
                    .child(
                        div()
                            .px_1()
                            .text_xs()
                            .font_weight(FontWeight::SEMIBOLD)
                            .text_color(theme.primary_text)
                            .overflow_hidden()
                            .whitespace_nowrap()
                            .child(name),
                    )
            }))
            .into_any_element()
    }

    #[allow(clippy::too_many_arguments)]
    fn item_surface(
        &self,
        theme: Theme,
        id: impl Into<ElementId>,
        name: String,
        position: usize,
        set_size: usize,
        selected: bool,
        is_folder: bool,
        selected_count: usize,
        path: String,
        child: impl IntoElement,
        focus: FocusHandle,
        cx: &mut Context<Self>,
    ) -> impl IntoElement {
        let path_left = path.clone();
        let path_hover = path.clone();
        let path_right = path.clone();
        let path_accessible = path;
        let entity = cx.entity().downgrade();
        let file_operation_busy = self.file_operation_label.is_some();
        let rename_disabled = file_operation_busy || (selected && selected_count != 1);
        let drop_target = drag_target(&self.drag).is_some_and(|target| target == path_left);
        let dragging = is_dragging(&self.drag);

        div()
            .id(id)
            .debug_selector(move || format!("gallery-item-{position}"))
            .role(Role::ListBoxOption)
            .aria_label(name)
            .aria_selected(selected)
            .aria_position_in_set(position)
            .aria_size_of_set(set_size)
            .when(selected, |this| this.aria_active_descendant())
            .rounded(px(10.))
            .border(px(if selected { 3.0 } else { 1.0 }))
            .border_color(if drop_target || selected {
                theme.accent
            } else {
                theme.line
            })
            .bg(if drop_target {
                theme.accent_soft
            } else if selected {
                theme.selected
            } else {
                theme.surface
            })
            .overflow_hidden()
            .cursor_pointer()
            .hover(|s| s.border_color(theme.strong_line).bg(theme.hover))
            .on_mouse_move(cx.listener(move |this, _, _, cx| {
                if this.hover_path.as_deref() != Some(path_hover.as_str()) {
                    this.hover_path = Some(path_hover.clone());
                    if is_dragging(&this.drag) {
                        cx.notify();
                    }
                }
            }))
            .on_mouse_down(
                MouseButton::Left,
                cx.listener(move |this, event: &MouseDownEvent, window, cx| {
                    if dragging {
                        return;
                    }
                    if is_folder {
                        this.open_folder(path_left.clone(), false, false, true, cx);
                    } else if event.click_count >= 2 {
                        this.select_path(&path_left, SelectionGesture::Replace);
                        this.open_viewer(&path_left, window, cx);
                    } else {
                        let gesture = if event.modifiers.shift {
                            SelectionGesture::Range {
                                additive: event.modifiers.control,
                            }
                        } else if event.modifiers.control {
                            SelectionGesture::Toggle
                        } else {
                            SelectionGesture::Replace
                        };
                        this.select_path(&path_left, gesture);
                        this.begin_image_drag(
                            &path_left,
                            (f64::from(event.position.x), f64::from(event.position.y)),
                        );
                        cx.notify();
                    }
                }),
            )
            .on_mouse_down(
                MouseButton::Right,
                cx.listener(move |this, _, window, cx| {
                    if is_folder {
                        return;
                    }
                    if !this.selected.contains(&path_right) {
                        this.select_path(&path_right, SelectionGesture::Replace);
                        cx.notify();
                    }
                    // ContextMenu focuses its PopupMenu during the next layout pass.
                    // Clear the current focus before that frame so GPUI does not
                    // publish both the gallery and menu as accessibility focus.
                    window.defer(cx, |window, _| window.blur());
                }),
            )
            .on_a11y_action(AccessibleAction::Click, move |_, _, cx| {
                let _ = entity.update(cx, |this, cx| {
                    this.activate_gallery_item_from_accessibility(
                        path_accessible.clone(),
                        is_folder,
                        cx,
                    );
                });
            })
            .context_menu(move |menu, _, _| {
                if is_folder {
                    return menu;
                }
                menu.action_context(focus.clone())
                    .menu_with_icon("開啟檢視", IconName::Eye, Box::new(OpenViewer))
                    .menu_with_icon(
                        "在檔案管理器中顯示",
                        IconName::ExternalLink,
                        Box::new(RevealInFileManager),
                    )
                    .menu_with_icon_and_disabled(
                        "重新命名",
                        IconName::File,
                        Box::new(RenameSelection),
                        rename_disabled,
                    )
                    .separator()
                    .menu_with_icon_and_disabled(
                        "移至回收筒",
                        IconName::Delete,
                        Box::new(TrashSelection),
                        file_operation_busy,
                    )
            })
            .child(child)
    }
}

#[cfg(test)]
mod tests {
    use std::{path::PathBuf, sync::Arc};

    use gpui::{px, size, Modifiers, TestAppContext, VisualTestContext};
    use piclens_domain::{ImageListItem, ListItem};
    use piclens_infra::JsonSettingsStore;

    use super::PicLensApp;

    fn image(path: &str) -> ListItem {
        ListItem::Image(ImageListItem {
            path: path.into(),
            name: PathBuf::from(path)
                .file_name()
                .unwrap()
                .to_string_lossy()
                .into_owned(),
            extension: "png".into(),
            modified_at_ms: None,
            size_bytes: 1,
            is_animated: false,
        })
    }

    #[gpui::test]
    fn pointer_keyboard_and_accessibility_share_selection_state(cx: &mut TestAppContext) {
        cx.update(|cx| {
            gpui_component::init(cx);
            crate::theme::init(cx);
            crate::actions::init(cx);
        });

        let settings_path = std::env::temp_dir().join(format!(
            "piclens-selection-test-{}.json",
            std::process::id()
        ));
        let (app, cx) = cx.add_window_view(move |window, cx| {
            PicLensApp::new_with_settings_store(
                window,
                cx,
                None,
                super::super::LaunchOptions::default(),
                Arc::new(JsonSettingsStore::with_path(settings_path)),
            )
        });
        app.update(cx, |app, cx| {
            app.items = ('a'..='h')
                .map(|name| image(&format!("/{name}.png")))
                .collect();
            app.visible = app.items.clone();
            app.thumb_failed = app
                .visible
                .iter()
                .map(|item| item.path().to_string())
                .collect();
            app.gallery_width = 800.0;
            app.sync_gallery_list();
            cx.notify();
        });

        let cx: &mut VisualTestContext = cx;
        cx.simulate_resize(size(px(1280.), px(800.)));
        cx.run_until_parked();
        cx.update(|window, cx| {
            _ = window.draw(cx);
        });

        let first = cx.debug_bounds("gallery-item-1").unwrap().center();
        let second = cx.debug_bounds("gallery-item-2").unwrap().center();
        let third = cx.debug_bounds("gallery-item-3").unwrap().center();
        assert_eq!(first.y, second.y);
        assert!(second.x > first.x);

        cx.simulate_click(first, Modifiers::default());
        app.read_with(cx, |app, _| {
            assert_eq!(app.selection_order, vec!["/a.png"]);
        });

        cx.simulate_click(
            second,
            Modifiers {
                control: true,
                ..Default::default()
            },
        );
        app.read_with(cx, |app, _| {
            assert_eq!(app.selection_order, vec!["/a.png", "/b.png"]);
        });

        cx.simulate_click(
            first,
            Modifiers {
                control: true,
                ..Default::default()
            },
        );
        app.read_with(cx, |app, _| {
            assert_eq!(app.selection_order, vec!["/b.png"]);
            assert_eq!(app.selection_anchor.as_deref(), Some("/a.png"));
        });

        cx.simulate_click(
            third,
            Modifiers {
                shift: true,
                ..Default::default()
            },
        );
        app.read_with(cx, |app, _| {
            assert_eq!(app.selection_order, vec!["/a.png", "/b.png", "/c.png"]);
        });

        app.update(cx, |app, cx| {
            app.clear_selection();
            cx.notify();
        });
        let focus = app.read_with(cx, |app, _| app.focus_handle.clone());
        cx.update(|window, cx| {
            focus.focus(window, cx);
            assert_eq!(window.focused(cx).as_ref(), Some(&focus));
        });
        cx.simulate_keystrokes("ctrl-a");
        app.read_with(cx, |app, _| {
            assert_eq!(
                app.selection_order,
                app.visible
                    .iter()
                    .map(|item| item.path())
                    .collect::<Vec<_>>()
            );
        });

        // Vertical navigation must follow grid columns, not single list items.
        let columns = app.read_with(cx, |app, _| app.gallery_columns());
        assert!(columns > 1 && columns < 8, "grid columns: {columns}");
        cx.simulate_click(first, Modifiers::default());
        cx.simulate_keystrokes("down");
        app.read_with(cx, |app, _| {
            assert_eq!(app.selection_order, vec![app.visible[columns].path()]);
        });
        cx.simulate_keystrokes("up");
        app.read_with(cx, |app, _| {
            assert_eq!(app.selection_order, vec!["/a.png"]);
        });

        // The pinned GPUI TestPlatform cannot inject AccessKit ActionRequest values.
        // Exercise the same handler that the registered accessibility Click action uses.
        app.update(cx, |app, cx| {
            app.activate_gallery_item_from_accessibility("/b.png".into(), false, cx);
        });
        app.read_with(cx, |app, _| {
            assert_eq!(app.selection_order, vec!["/b.png"]);
            assert_eq!(app.selection_anchor.as_deref(), Some("/b.png"));
        });
    }
}
