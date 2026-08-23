//! Command bar, sidebar, library chrome, and status bar.

use std::{path::PathBuf, time::Duration};

use crate::folder_tree::TreeRow;
use gpui::prelude::FluentBuilder as _;
use gpui::*;
use gpui_component::button::{Button, ButtonVariants as _};
use gpui_component::input::Input;
use gpui_component::{h_flex, v_flex, Disableable, Icon, IconName, Selectable};
use piclens_domain::path_equals;
use piclens_infra::{cleanup_same_basename, convert_to_jpg, convert_to_lossless_webp, trash_paths};

use super::{GalleryMode, PicLensApp};
use crate::theme::{self, Theme};

impl PicLensApp {
    fn compact_chrome(&self) -> bool {
        self.gallery_width > 1.0 && self.gallery_width < 720.0
    }

    pub(super) fn folder_title(&self) -> String {
        self.folder_path
            .as_deref()
            .map(|p| {
                PathBuf::from(p)
                    .file_name()
                    .and_then(|n| n.to_str())
                    .unwrap_or(p)
                    .to_string()
            })
            .unwrap_or_else(|| "未選擇資料夾".into())
    }

    pub(super) fn folder_path_label(&self) -> String {
        self.folder_path
            .clone()
            .unwrap_or_else(|| "請選擇本機圖片資料夾".into())
    }

    pub(super) fn render_command_bar(
        &self,
        theme: Theme,
        cx: &mut Context<Self>,
    ) -> impl IntoElement {
        let compact = self.compact_chrome();
        h_flex()
            .id("command-bar")
            .w_full()
            .h(px(theme::COMMAND_BAR_H))
            .px(if compact { px(12.) } else { px(20.) })
            .gap_2()
            .items_center()
            .bg(theme.command_bar)
            .border_b_1()
            .border_color(theme.line)
            .child(
                h_flex()
                    .gap_2()
                    .items_center()
                    .child(
                        div()
                            .size(px(34.))
                            .rounded(px(8.))
                            .overflow_hidden()
                            .bg(theme.accent_soft)
                            .flex()
                            .items_center()
                            .justify_center()
                            .child(
                                img(crate::assets::app_icon())
                                    .size(px(34.))
                                    .object_fit(ObjectFit::Contain),
                            ),
                    )
                    .children((!compact).then(|| {
                        div()
                            .text_lg()
                            .font_weight(FontWeight::BOLD)
                            .text_color(theme.primary_text)
                            .child("PicLens")
                    })),
            )
            .child(
                h_flex()
                    .gap_1()
                    .child(
                        Button::new("sidebar")
                            .outline()
                            .icon(if self.sidebar_collapsed {
                                IconName::PanelLeftOpen
                            } else {
                                IconName::PanelLeftClose
                            })
                            .tooltip("側欄")
                            .on_click(cx.listener(|this, _, _, cx| {
                                this.sidebar_collapsed = !this.sidebar_collapsed;
                                this.persist_sidebar();
                                this.sync_gallery_list();
                                this.request_thumbs(cx);
                                cx.notify();
                            })),
                    )
                    .child(
                        Button::new("back")
                            .ghost()
                            .icon(IconName::ArrowLeft)
                            .tooltip("上一頁")
                            .disabled(!self.history.can_back())
                            .on_click(
                                cx.listener(|this, _, _, cx| this.navigate_history(true, cx)),
                            ),
                    )
                    .child(
                        Button::new("forward")
                            .ghost()
                            .icon(IconName::ArrowRight)
                            .tooltip("下一頁")
                            .disabled(!self.history.can_forward())
                            .on_click(
                                cx.listener(|this, _, _, cx| this.navigate_history(false, cx)),
                            ),
                    )
                    .child(
                        Button::new("refresh")
                            .ghost()
                            .icon(IconName::ArrowDown)
                            .tooltip("重新整理")
                            .on_click(cx.listener(|this, _, _, cx| this.refresh(cx))),
                    ),
            )
            .child(
                div()
                    .flex_1()
                    .max_w(px(420.))
                    .mx(if compact { px(8.) } else { px(20.) })
                    .child(Input::new(&self.search)),
            )
            .child(div().flex_1())
            .child(
                Button::new("open")
                    .primary()
                    .icon(IconName::FolderOpen)
                    .label(if compact {
                        "資料夾"
                    } else {
                        "開啟資料夾"
                    })
                    .on_click(cx.listener(|this, _, window, cx| this.pick_folder(window, cx))),
            )
    }

    pub(super) fn render_sidebar(&self, theme: Theme, cx: &mut Context<Self>) -> AnyElement {
        if self.sidebar_collapsed {
            return div().id("sidebar-off").w(px(0.)).into_any_element();
        }
        let root = self
            .tree_root
            .clone()
            .or_else(|| self.folder_path.clone())
            .unwrap_or_default();
        v_flex()
            .id("sidebar")
            .w(px(theme::SIDEBAR_W))
            .h_full()
            .bg(theme.sidebar)
            .border_r_1()
            .border_color(theme.line)
            .child(
                h_flex()
                    .w_full()
                    .h(px(48.))
                    .px_4()
                    .items_center()
                    .border_b_1()
                    .border_color(theme.line)
                    .child(
                        div()
                            .text_sm()
                            .font_weight(FontWeight::SEMIBOLD)
                            .text_color(theme.primary_text)
                            .child("資料夾"),
                    ),
            )
            .child(
                div()
                    .id("sidebar-scroll")
                    .flex_1()
                    .p_3()
                    .overflow_y_scroll()
                    .child(
                        v_flex().gap_1().children(
                            self.tree_rows()
                                .into_iter()
                                .enumerate()
                                .map(|(idx, row)| self.render_tree_row(idx, row, theme, cx)),
                        ),
                    )
                    .child(if root.is_empty() {
                        div().into_any_element()
                    } else {
                        div()
                            .mt_3()
                            .px_1()
                            .text_xs()
                            .text_color(theme.muted_text)
                            .child(root)
                            .into_any_element()
                    }),
            )
            .into_any_element()
    }

    fn render_tree_row(
        &self,
        idx: usize,
        row: TreeRow,
        _theme: Theme,
        cx: &mut Context<Self>,
    ) -> impl IntoElement {
        let path = row.path.clone();
        let open_path = row.path.clone();
        let name = PathBuf::from(&row.path)
            .file_name()
            .and_then(|n| n.to_str())
            .unwrap_or(row.path.as_str())
            .to_string();
        let active = self
            .folder_path
            .as_ref()
            .map(|p| path_equals(p, &row.path))
            .unwrap_or(false);
        let expanded = row.expanded;
        let chevron = if self.tree_motion_path.as_deref() == Some(row.path.as_str()) {
            Icon::new(IconName::ChevronRight)
                .with_animation(
                    format!("tree-chevron:{}:{}", row.path, self.tree_motion_revision),
                    Animation::new(Duration::from_millis(130)).with_easing(ease_out_quint()),
                    move |this, delta| {
                        let turn = if expanded { delta } else { 1.0 - delta };
                        this.rotate(percentage(turn * 0.25))
                    },
                )
                .into_any_element()
        } else {
            Icon::new(IconName::ChevronRight)
                .rotate(percentage(if expanded { 0.25 } else { 0.0 }))
                .into_any_element()
        };
        h_flex()
            .id(("tree-row", idx))
            .w_full()
            .pl(px(8.0 + row.depth as f32 * 14.0))
            .gap_1()
            .items_center()
            .child(
                Button::new(("tree-exp", idx))
                    .ghost()
                    .child(chevron)
                    .on_click(cx.listener(move |this, _, _, cx| {
                        this.toggle_tree_path(path.clone(), cx);
                    })),
            )
            .child(
                Button::new(("tree-open", idx))
                    .ghost()
                    .selected(active)
                    .icon(IconName::Folder)
                    .label(name)
                    .on_click(cx.listener(move |this, _, _, cx| {
                        this.open_folder(open_path.clone(), false, true, cx);
                    })),
            )
    }

    pub(super) fn render_library_header(
        &self,
        theme: Theme,
        folder_title: String,
        folder_path: String,
        visible_count: usize,
        cx: &mut Context<Self>,
    ) -> impl IntoElement {
        let compact = self.compact_chrome();
        h_flex()
            .w_full()
            .px(px(28.))
            .pt_4()
            .pb_3()
            .gap_3()
            .items_start()
            .justify_between()
            .when(compact, |this| {
                this.flex_col().items_stretch().px_4().gap_3()
            })
            .child(
                v_flex()
                    .gap_1()
                    .overflow_hidden()
                    .child(
                        div()
                            .text_xl()
                            .font_weight(FontWeight::SEMIBOLD)
                            .text_color(theme.primary_text)
                            .child(folder_title),
                    )
                    .child(
                        div()
                            .px_2()
                            .py_1()
                            .rounded_full()
                            .bg(theme.app_background)
                            .border_1()
                            .border_color(theme.line)
                            .text_xs()
                            .text_color(theme.secondary_text)
                            .child(format!("共 {visible_count} 個項目")),
                    )
                    .child(
                        div()
                            .text_xs()
                            .text_color(theme.muted_text)
                            .text_ellipsis()
                            .child(folder_path),
                    ),
            )
            .child(
                h_flex()
                    .gap_1()
                    .flex_wrap()
                    .justify_end()
                    .child(
                        Button::new("recursive")
                            .outline()
                            .selected(self.settings.include_subfolders)
                            .label("含子資料夾")
                            .on_click(
                                cx.listener(|this, _, _, cx| this.toggle_include_subfolders(cx)),
                            ),
                    )
                    .child(
                        Button::new("sort")
                            .outline()
                            .label(self.sort_label())
                            .on_click(cx.listener(|this, _, _, cx| this.cycle_sort(cx))),
                    )
                    .child(
                        Button::new("mode")
                            .outline()
                            .selected(self.gallery_mode == GalleryMode::Grid)
                            .label(if self.gallery_mode == GalleryMode::Grid {
                                "格狀"
                            } else {
                                "列表"
                            })
                            .on_click(cx.listener(|this, _, _, cx| {
                                this.gallery_mode = match this.gallery_mode {
                                    GalleryMode::Grid => GalleryMode::List,
                                    GalleryMode::List => GalleryMode::Grid,
                                };
                                this.sync_gallery_list();
                                this.request_thumbs(cx);
                                cx.notify();
                            })),
                    )
                    .child(
                        Button::new("open-view")
                            .outline()
                            .label("開啟檢視")
                            .on_click(cx.listener(|this, _, window, cx| {
                                if let Some(img) = this.selected_images().first() {
                                    let path = img.path.clone();
                                    this.open_viewer(&path, window, cx);
                                } else {
                                    this.status = "請先選取圖片。".into();
                                    cx.notify();
                                }
                            })),
                    )
                    .child(
                        Button::new("rename").outline().label("重新命名").on_click(
                            cx.listener(|this, _, window, cx| this.start_rename(window, cx)),
                        ),
                    )
                    .child(
                        Button::new("drop-rename")
                            .outline()
                            .label("依目標重新命名")
                            .on_click(cx.listener(|this, _, _, cx| {
                                this.plan_drop_rename_from_selection(cx)
                            })),
                    )
                    .child(
                        Button::new("to-jpg")
                            .outline()
                            .label("轉 JPG")
                            .on_click(cx.listener(|this, _, window, cx| {
                                let paths = this.visible_image_paths();
                                let batch = convert_to_jpg(&paths);
                                this.apply_batch("轉 JPG", &batch, window, cx);
                                this.refresh(cx);
                            })),
                    )
                    .child(
                        Button::new("to-webp")
                            .outline()
                            .label("轉 WebP")
                            .on_click(cx.listener(|this, _, window, cx| {
                                let paths = this.visible_image_paths();
                                let batch = convert_to_lossless_webp(&paths);
                                this.apply_batch("轉 WebP", &batch, window, cx);
                                this.refresh(cx);
                            })),
                    )
                    .child(
                        Button::new("cleanup")
                            .outline()
                            .label("清除同名格式")
                            .on_click(cx.listener(|this, _, window, cx| {
                                let paths = this.visible_image_paths();
                                let batch = cleanup_same_basename(&paths);
                                this.apply_batch("清除同名格式", &batch, window, cx);
                                this.refresh(cx);
                            })),
                    )
                    .child(
                        Button::new("reveal")
                            .outline()
                            .label("顯示位置")
                            .on_click(cx.listener(|this, _, _, cx| this.reveal_focus(cx))),
                    )
                    .child(Button::new("clear-sel").ghost().label("清除選取").on_click(
                        cx.listener(|this, _, _, cx| {
                            this.clear_selection();
                            cx.notify();
                        }),
                    ))
                    .child(
                        Button::new("trash")
                            .danger()
                            .icon(IconName::Delete)
                            .label("回收筒")
                            .on_click(cx.listener(|this, _, window, cx| {
                                let paths: Vec<String> =
                                    this.selected_images().into_iter().map(|i| i.path).collect();
                                if paths.is_empty() {
                                    this.status = "請先選取圖片。".into();
                                    cx.notify();
                                    return;
                                }
                                let batch = trash_paths(&paths);
                                this.apply_batch("移至回收筒", &batch, window, cx);
                                this.refresh(cx);
                            })),
                    ),
            )
    }

    pub(super) fn render_status_bar(
        &self,
        theme: Theme,
        visible_count: usize,
        selected_count: usize,
        cx: &mut Context<Self>,
    ) -> impl IntoElement {
        let compact = self.compact_chrome();
        h_flex()
            .id("status-bar")
            .w_full()
            .h(px(theme::STATUS_BAR_H))
            .px_5()
            .gap_3()
            .items_center()
            .bg(theme.command_bar)
            .border_t_1()
            .border_color(theme.line)
            .child(
                div()
                    .flex_1()
                    .text_sm()
                    .text_color(theme.secondary_text)
                    .overflow_hidden()
                    .whitespace_nowrap()
                    .child(self.status.clone()),
            )
            .child(
                h_flex()
                    .gap_1()
                    .items_center()
                    .child(
                        Button::new("thumb-")
                            .ghost()
                            .icon(IconName::Minus)
                            .tooltip("縮小縮圖")
                            .on_click(
                                cx.listener(|this, _, _, cx| this.adjust_thumb_size(-20, cx)),
                            ),
                    )
                    .child(
                        div()
                            .text_xs()
                            .text_color(theme.muted_text)
                            .child(format!("縮圖 {}", self.settings.thumbnail_size)),
                    )
                    .child(
                        Button::new("thumb+")
                            .ghost()
                            .icon(IconName::Plus)
                            .tooltip("放大縮圖")
                            .on_click(cx.listener(|this, _, _, cx| this.adjust_thumb_size(20, cx))),
                    ),
            )
            .child(
                div()
                    .text_xs()
                    .text_color(theme.muted_text)
                    .child(if compact {
                        format!("{} 項 · 選取 {}", visible_count, selected_count)
                    } else {
                        format!(
                            "{} 項 · 選取 {} · Esc 關閉 · Del 回收",
                            visible_count, selected_count
                        )
                    }),
            )
    }
}
