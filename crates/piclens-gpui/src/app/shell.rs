//! Command bar, sidebar, library chrome, and status bar.

use std::{path::PathBuf, time::Duration};

use crate::folder_tree::TreeRow;
use gpui::prelude::FluentBuilder as _;
use gpui::*;
use gpui_component::button::{Button, ButtonVariants as _};
use gpui_component::input::Input;
use gpui_component::menu::DropdownMenu as _;
use gpui_component::scroll::Scrollbar;
use gpui_component::{h_flex, v_flex, Disableable, Icon, IconName, Selectable};
use piclens_domain::{path_equals, FileOperationStatus};

use super::{accessible_icon_button, AdaptiveLayout, PicLensApp};
use crate::actions::{
    CleanupSameBasename, ConvertJpg, ConvertWebp, DropRenamePlan, RenameSelection,
    SortModifiedAscending, SortModifiedDescending, SortNameAscending, SortNameDescending,
    TrashSelection,
};
use crate::interaction::{
    batch_result_detail, batch_result_file_name, batch_result_reveal_path,
    batch_result_status_label,
};
use crate::theme::{self, Theme};

impl PicLensApp {
    pub(super) fn render_batch_report(
        &self,
        theme: Theme,
        cx: &mut Context<Self>,
    ) -> Option<impl IntoElement> {
        let report = self.batch_report.as_ref()?;
        let label = report.label.clone();
        let total = report.batch.total();
        let succeeded = report.batch.succeeded();
        let skipped = report.batch.skipped();
        let failed = report.batch.failed();
        let list_state = self.batch_report_list.clone();
        let entity = cx.entity().downgrade();

        Some(
            v_flex()
                .id("batch-report")
                .absolute()
                .top(px(theme::COMMAND_BAR_H + 12.0))
                .right(px(12.0))
                .bottom(px(theme::STATUS_BAR_H + 12.0))
                .w(px(420.0))
                .max_w(relative(0.92))
                .rounded(px(12.0))
                .bg(theme.surface)
                .border_1()
                .border_color(theme.strong_line)
                .overflow_hidden()
                .occlude()
                .child(
                    h_flex()
                        .px_4()
                        .py_3()
                        .gap_3()
                        .border_b_1()
                        .border_color(theme.line)
                        .child(
                            v_flex()
                                .min_w_0()
                                .flex_1()
                                .gap_1()
                                .child(
                                    div()
                                        .text_lg()
                                        .font_weight(FontWeight::SEMIBOLD)
                                        .text_color(theme.primary_text)
                                        .child(format!("{label}結果")),
                                )
                                .child(
                                    div()
                                        .text_sm()
                                        .text_color(theme.secondary_text)
                                        .child(format!(
                                            "成功 {succeeded} · 略過 {skipped} · 失敗 {failed} · 共 {total}"
                                        )),
                                ),
                        )
                        .child(
                            Button::new("batch-report-close")
                                .ghost()
                                .label("關閉")
                                .on_click(cx.listener(|this, _, _, cx| {
                                    this.close_batch_report(cx)
                                })),
                        ),
                )
                .child(
                    div()
                        .relative()
                        .flex_1()
                        .min_h_0()
                        .child(
                            list(list_state.clone(), move |row, _window, cx| {
                                entity
                                    .upgrade()
                                    .map(|entity| {
                                        entity.update(cx, |this, cx| {
                                            this.render_batch_report_row(row, theme, cx)
                                        })
                                    })
                                    .unwrap_or_else(|| div().into_any_element())
                            })
                            .size_full(),
                        )
                        .child(
                            div()
                                .absolute()
                                .inset_0()
                                .child(Scrollbar::vertical(&list_state)),
                        ),
                ),
        )
    }

    fn render_batch_report_row(
        &self,
        row: usize,
        theme: Theme,
        cx: &mut Context<Self>,
    ) -> AnyElement {
        let Some(result) = self
            .batch_report
            .as_ref()
            .and_then(|report| report.batch.items.get(row))
        else {
            return div().into_any_element();
        };
        let file_name = batch_result_file_name(result);
        let status = batch_result_status_label(result.status);
        let detail = batch_result_detail(result);
        let reveal_path = batch_result_reveal_path(result).map(str::to_string);
        let status_color = if result.status == FileOperationStatus::Failed {
            theme.primary_text
        } else if result.status == FileOperationStatus::Skipped {
            theme.secondary_text
        } else {
            theme.accent
        };

        h_flex()
            .id(("batch-result", row))
            .h(px(104.0))
            .w_full()
            .px_4()
            .gap_3()
            .border_b_1()
            .border_color(theme.line)
            .child(
                div()
                    .w(px(72.0))
                    .text_xs()
                    .font_weight(FontWeight::SEMIBOLD)
                    .text_color(status_color)
                    .child(status),
            )
            .child(
                v_flex()
                    .min_w_0()
                    .flex_1()
                    .gap_1()
                    .child(
                        div()
                            .text_sm()
                            .font_weight(FontWeight::SEMIBOLD)
                            .text_color(theme.primary_text)
                            .overflow_hidden()
                            .whitespace_nowrap()
                            .child(file_name),
                    )
                    .child(
                        div()
                            .text_xs()
                            .text_color(theme.secondary_text)
                            .overflow_hidden()
                            .child(detail),
                    ),
            )
            .children(reveal_path.map(|path| {
                Button::new(("batch-reveal", row))
                    .ghost()
                    .label("顯示位置")
                    .on_click(cx.listener(move |this, _, _, cx| this.reveal_path(&path, cx)))
            }))
            .into_any_element()
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
        layout: AdaptiveLayout,
        cx: &mut Context<Self>,
    ) -> impl IntoElement {
        let compact = layout.compact;
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
                    .children((!layout.minimum).then(|| {
                        accessible_icon_button(
                            "sidebar",
                            if self.sidebar_collapsed {
                                "展開側欄"
                            } else {
                                "收合側欄"
                            },
                            Icon::new(if self.sidebar_collapsed {
                                IconName::PanelLeftOpen
                            } else {
                                IconName::PanelLeftClose
                            })
                            .text_color(theme.primary_text),
                        )
                        .outline()
                        .on_click(cx.listener(|this, _, _, cx| {
                            this.sidebar_collapsed = !this.sidebar_collapsed;
                            this.persist_sidebar();
                            this.sync_gallery_list();
                            this.request_thumbs(cx);
                            cx.notify();
                        }))
                    }))
                    .child(
                        accessible_icon_button(
                            "back",
                            "上一頁",
                            Icon::new(IconName::ArrowLeft).text_color(if self.history.can_back() {
                                theme.primary_text
                            } else {
                                theme.muted_text
                            }),
                        )
                        .ghost()
                        .tooltip("上一頁")
                        .disabled(!self.history.can_back())
                        .on_click(cx.listener(|this, _, _, cx| this.navigate_history(true, cx))),
                    )
                    .child(
                        accessible_icon_button(
                            "forward",
                            "下一頁",
                            Icon::new(IconName::ArrowRight).text_color(
                                if self.history.can_forward() {
                                    theme.primary_text
                                } else {
                                    theme.muted_text
                                },
                            ),
                        )
                        .ghost()
                        .tooltip("下一頁")
                        .disabled(!self.history.can_forward())
                        .on_click(cx.listener(|this, _, _, cx| this.navigate_history(false, cx))),
                    )
                    .child(
                        accessible_icon_button(
                            "refresh",
                            "重新整理",
                            Icon::new(IconName::Redo).text_color(theme.primary_text),
                        )
                        .ghost()
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

    pub(super) fn render_sidebar(
        &self,
        theme: Theme,
        hidden_for_window: bool,
        cx: &mut Context<Self>,
    ) -> AnyElement {
        if self.sidebar_collapsed || hidden_for_window {
            return div().id("sidebar-off").w(px(0.)).into_any_element();
        }
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
                    ),
            )
            .into_any_element()
    }

    fn render_tree_row(
        &self,
        idx: usize,
        row: TreeRow,
        theme: Theme,
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
        let expand_label = format!(
            "{}資料夾「{}」",
            if expanded { "收合" } else { "展開" },
            name
        );
        let chevron = if self.tree_motion_path.as_deref() == Some(row.path.as_str()) {
            Icon::new(IconName::ChevronRight)
                .text_color(theme.secondary_text)
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
                .text_color(theme.secondary_text)
                .rotate(percentage(if expanded { 0.25 } else { 0.0 }))
                .into_any_element()
        };
        h_flex()
            .id(("tree-row", idx))
            .w_full()
            .pl(px(8.0 + row.depth as f32 * 14.0))
            .gap_1()
            .items_center()
            .children(row.expandable.then(|| {
                accessible_icon_button(("tree-exp", idx), expand_label, chevron)
                    .ghost()
                    .on_click(cx.listener(move |this, _, _, cx| {
                        this.toggle_tree_path(path.clone(), cx);
                    }))
            }))
            .child(
                Button::new(("tree-open", idx))
                    .ghost()
                    .selected(active)
                    .icon(IconName::Folder)
                    .label(name)
                    .on_click(cx.listener(move |this, _, _, cx| {
                        this.open_folder(open_path.clone(), false, false, true, cx);
                    })),
            )
    }

    pub(super) fn render_library_header(
        &self,
        theme: Theme,
        folder_title: String,
        folder_path: String,
        visible_count: usize,
        layout: AdaptiveLayout,
        cx: &mut Context<Self>,
    ) -> impl IntoElement {
        let compact = layout.compact;
        let selected_count = self.selected_images().len();
        let has_selection = selected_count > 0;
        let file_operation_busy = self.file_operation_label.is_some();
        let has_visible_images = self.visible.iter().any(|item| item.as_image().is_some());
        let rename_focus = self.focus_handle.clone();
        let batch_focus = self.focus_handle.clone();
        let sort_focus = self.focus_handle.clone();
        h_flex()
            .w_full()
            .px(if layout.minimum { px(8.0) } else { px(28.0) })
            .pt(if layout.minimum { px(8.0) } else { px(16.0) })
            .pb(if layout.minimum { px(8.0) } else { px(12.0) })
            .gap(if layout.minimum { px(4.0) } else { px(12.0) })
            .items_start()
            .justify_between()
            .when(compact, |this| {
                this.flex_col()
                    .items_stretch()
                    .px(if layout.minimum { px(8.0) } else { px(16.0) })
                    .gap(if layout.minimum { px(4.0) } else { px(12.0) })
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
                    .children((!layout.minimum).then(|| {
                        div()
                            .px_2()
                            .py_1()
                            .rounded_full()
                            .bg(theme.app_background)
                            .border_1()
                            .border_color(theme.line)
                            .text_xs()
                            .text_color(theme.secondary_text)
                            .child(format!("共 {visible_count} 個項目"))
                    }))
                    .children((!layout.minimum).then(|| {
                        div()
                            .text_xs()
                            .text_color(theme.muted_text)
                            .text_ellipsis()
                            .child(folder_path)
                    })),
            )
            .child(
                v_flex()
                    .gap_2()
                    .items_end()
                    .when(compact, |this| this.items_start())
                    .child(
                        h_flex()
                            .gap_1()
                            .flex_wrap()
                            .justify_end()
                            .when(compact, |this| this.justify_start())
                            .children((!compact).then(|| {
                                div()
                                    .min_w(px(48.))
                                    .text_xs()
                                    .text_color(theme.muted_text)
                                    .child("圖庫")
                            }))
                            .child(
                                Button::new("recursive")
                                    .outline()
                                    .selected(self.settings.include_subfolders)
                                    .toggled(self.settings.include_subfolders)
                                    .label(if compact {
                                        "子資料夾"
                                    } else {
                                        "含子資料夾"
                                    })
                                    .on_click(cx.listener(|this, _, _, cx| {
                                        this.toggle_include_subfolders(cx)
                                    })),
                            )
                            .child(
                                Button::new("sort")
                                    .outline()
                                    .label(self.sort_label())
                                    .dropdown_menu(move |menu, _, _| {
                                        menu.action_context(sort_focus.clone())
                                            .menu("名稱遞增", Box::new(SortNameAscending))
                                            .menu("名稱遞減", Box::new(SortNameDescending))
                                            .separator()
                                            .menu("修改時間遞增", Box::new(SortModifiedAscending))
                                            .menu("修改時間遞減", Box::new(SortModifiedDescending))
                                    }),
                            )
                            .child(
                                Button::new("batch-actions")
                                    .outline()
                                    .icon(IconName::Ellipsis)
                                    .label(self.file_operation_label.unwrap_or(if compact {
                                        "批次"
                                    } else {
                                        "批次操作"
                                    }))
                                    .loading(file_operation_busy)
                                    .disabled(file_operation_busy || !has_visible_images)
                                    .dropdown_menu(move |menu, _, _| {
                                        menu.action_context(batch_focus.clone())
                                            .menu("目前結果轉 JPG", Box::new(ConvertJpg))
                                            .menu("目前結果轉 WebP", Box::new(ConvertWebp))
                                            .separator()
                                            .menu(
                                                "清除目前結果的同名格式",
                                                Box::new(CleanupSameBasename),
                                            )
                                    }),
                            )
                            .children(file_operation_busy.then(|| {
                                Button::new("cancel-file-operation")
                                    .danger()
                                    .label("取消操作")
                                    .on_click(cx.listener(|this, _, _, cx| {
                                        this.cancel_file_operation(cx);
                                    }))
                            })),
                    )
                    .when(!layout.minimum || has_selection, |this| {
                        this.child(
                            h_flex()
                                .gap_1()
                                .flex_wrap()
                                .justify_end()
                                .when(compact, |this| this.justify_start())
                                .child(
                                    div()
                                        .id("selection-status")
                                        .role(Role::Status)
                                        .aria_label(self.selection_announcement())
                                        .min_w(px(48.))
                                        .text_xs()
                                        .text_color(if has_selection {
                                            theme.secondary_text
                                        } else {
                                            theme.muted_text
                                        })
                                        .child(format!("選取 {selected_count}")),
                                )
                                .when(!has_selection, |this| {
                                    this.child(
                                        div()
                                            .text_sm()
                                            .text_color(theme.muted_text)
                                            .child("選取圖片後可管理"),
                                    )
                                })
                                .when(has_selection, |this| {
                                    this.child(
                                        Button::new("open-view")
                                            .outline()
                                            .label(if compact { "檢視" } else { "開啟檢視" })
                                            .on_click(cx.listener(|this, _, window, cx| {
                                                if let Some(img) = this.selected_images().first() {
                                                    let path = img.path.clone();
                                                    this.open_viewer(&path, window, cx);
                                                }
                                            })),
                                    )
                                    .child(
                                        Button::new("rename-actions")
                                            .outline()
                                            .label("重新命名")
                                            .disabled(file_operation_busy)
                                            .dropdown_menu(move |menu, _, _| {
                                                menu.action_context(rename_focus.clone())
                                                    .menu_with_disabled(
                                                        "重新命名",
                                                        Box::new(RenameSelection),
                                                        selected_count != 1 || file_operation_busy,
                                                    )
                                                    .menu_with_disabled(
                                                        "依目標重新命名",
                                                        Box::new(DropRenamePlan),
                                                        selected_count < 2 || file_operation_busy,
                                                    )
                                            }),
                                    )
                                    .child(
                                        Button::new("reveal")
                                            .outline()
                                            .label(if compact { "位置" } else { "顯示位置" })
                                            .disabled(selected_count != 1)
                                            .on_click(
                                                cx.listener(|this, _, _, cx| this.reveal_focus(cx)),
                                            ),
                                    )
                                    .child(Button::new("clear-sel").ghost().label("清除").on_click(
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
                                            .disabled(file_operation_busy)
                                            .on_click(cx.listener(|this, _, window, cx| {
                                                this.on_trash(&TrashSelection, window, cx)
                                            })),
                                    )
                                }),
                        )
                    }),
            )
    }

    pub(super) fn render_status_bar(
        &self,
        theme: Theme,
        visible_count: usize,
        selected_count: usize,
        layout: AdaptiveLayout,
        cx: &mut Context<Self>,
    ) -> impl IntoElement {
        let compact = layout.compact;
        h_flex()
            .id("status-bar")
            .w_full()
            .h(if layout.minimum {
                px(36.0)
            } else {
                px(theme::STATUS_BAR_H)
            })
            .px(if layout.minimum { px(8.0) } else { px(20.0) })
            .gap_3()
            .items_center()
            .bg(theme.command_bar)
            .border_t_1()
            .border_color(theme.line)
            .child(
                div()
                    .id("status-message")
                    .role(Role::Status)
                    .aria_label(self.status.clone())
                    .flex_1()
                    .text_sm()
                    .text_color(theme.secondary_text)
                    .overflow_hidden()
                    .whitespace_nowrap()
                    .child(self.status.clone()),
            )
            .child(
                div()
                    .id("version-label")
                    .aria_label(format!("PicLens 版本 {}", env!("CARGO_PKG_VERSION")))
                    .text_xs()
                    .text_color(theme.muted_text)
                    .child(format!("PicLens {}", env!("CARGO_PKG_VERSION"))),
            )
            .child(
                h_flex()
                    .gap_1()
                    .items_center()
                    .child(
                        accessible_icon_button(
                            "thumb-",
                            "縮小縮圖",
                            Icon::new(IconName::Minus).text_color(theme.secondary_text),
                        )
                        .ghost()
                        .tooltip("縮小縮圖")
                        .on_click(cx.listener(|this, _, _, cx| this.adjust_thumb_size(-20, cx))),
                    )
                    .child(
                        div()
                            .text_xs()
                            .text_color(theme.muted_text)
                            .child(format!("縮圖 {}", self.settings.thumbnail_size)),
                    )
                    .child(
                        accessible_icon_button(
                            "thumb+",
                            "放大縮圖",
                            Icon::new(IconName::Plus).text_color(theme.secondary_text),
                        )
                        .ghost()
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
