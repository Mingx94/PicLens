//! Library tiles, list rows, and empty state.

use crate::drag_rename::{drag_target, is_dragging};
use gpui::*;
use gpui_component::button::{Button, ButtonVariants as _};
use gpui_component::menu::ContextMenuExt;
use gpui_component::scroll::Scrollbar;
use gpui_component::{h_flex, v_flex, Icon, IconName};

use super::{GalleryMode, PicLensApp};
use crate::actions::{OpenViewer, RenameSelection, RevealInFileManager, TrashSelection};
use crate::theme::Theme;

impl PicLensApp {
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
                .child(Icon::new(IconName::Folder).text_color(theme.accent))
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
                        .size(px(size)),
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
            .relative()
            .size_full()
            .child(
                canvas(
                    move |bounds, _, cx| {
                        if let Some(entity) = measure.upgrade() {
                            entity.update(cx, |this, cx| {
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

        if self.gallery_mode == GalleryMode::Grid {
            return h_flex()
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
                .into_any_element();
        }

        let item = &self.visible[start];
        let path = item.path().to_string();
        let name = item.name().to_string();
        let is_folder = item.is_folder();
        let animated = item.as_image().map(|i| i.is_animated).unwrap_or(false);
        let selected = self.selected.contains(&path);
        let preview = self.tile_preview(theme, &path, is_folder, animated, 48.0);
        let badge = if animated {
            "動畫"
        } else if is_folder {
            "資料夾"
        } else {
            ""
        };
        self.item_surface(
            theme,
            ("row", start),
            selected,
            is_folder,
            selected_count,
            path,
            h_flex()
                .w_full()
                .gap_3()
                .px_3()
                .py_2()
                .items_center()
                .child(preview)
                .child(
                    div()
                        .flex_1()
                        .text_sm()
                        .text_color(theme.primary_text)
                        .child(name),
                )
                .child(div().text_xs().text_color(theme.muted_text).child(badge))
                .into_any_element(),
            focus,
            cx,
        )
        .into_any_element()
    }

    #[allow(clippy::too_many_arguments)]
    fn item_surface(
        &self,
        theme: Theme,
        id: impl Into<ElementId>,
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
        let path_right = path;
        let rename_disabled = selected && selected_count != 1;
        let drop_target = drag_target(&self.drag).is_some_and(|target| target == path_left);
        let dragging = is_dragging(&self.drag);

        div()
            .id(id)
            .rounded(px(10.))
            .border_1()
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
                    let additive = event.modifiers.control || event.modifiers.shift;
                    if is_folder {
                        this.open_folder(path_left.clone(), false, true, cx);
                    } else if event.click_count >= 2 {
                        this.select_path(&path_left, false);
                        this.open_viewer(&path_left, window, cx);
                    } else {
                        this.select_path(&path_left, additive);
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
                cx.listener(move |this, _, _, cx| {
                    if !is_folder && !this.selected.contains(&path_right) {
                        this.select_path(&path_right, false);
                        cx.notify();
                    }
                }),
            )
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
                    .menu_with_icon("移至回收筒", IconName::Delete, Box::new(TrashSelection))
            })
            .child(child)
    }
}
