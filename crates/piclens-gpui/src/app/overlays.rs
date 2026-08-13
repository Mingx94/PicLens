//! Viewer, rename dialog, and drop-rename preview.

use std::path::PathBuf;

use gpui::*;
use gpui_component::button::{Button, ButtonVariants as _};
use gpui_component::input::Input;
use gpui_component::{h_flex, v_flex, ActiveTheme};
use piclens_domain::{clamp_zoom, reset_zoom_state};

use super::PicLensApp;
use crate::theme::Theme;

impl PicLensApp {
    pub(super) fn render_viewer(&self, theme: Theme, cx: &mut Context<Self>) -> Option<impl IntoElement> {
        let viewer = self.viewer.as_ref()?;
        let idx = viewer.sequence.current_index as usize;
        let name = viewer
            .sequence
            .images
            .get(idx)
            .map(|i| i.name.clone())
            .unwrap_or_default();
        let zoom = viewer.zoom.zoom;
        let message = viewer.message.clone();
        let display = viewer.display_path.clone();
        let pos = format!(
            "{}/{}",
            idx.saturating_add(1),
            viewer.sequence.images.len().max(1)
        );

        Some(
            div()
                .id("viewer")
                .absolute()
                .inset_0()
                .flex()
                .flex_col()
                .bg(theme.viewer_canvas)
                .child(
                    h_flex()
                        .w_full()
                        .h(px(48.))
                        .px_3()
                        .gap_2()
                        .items_center()
                        .bg(theme.viewer_bar)
                        .border_b_1()
                        .border_color(theme.viewer_bar_line)
                        .child(
                            Button::new("v-close")
                                .ghost()
                                .icon(gpui_component::IconName::ArrowLeft)
                                .label("返回")
                                .on_click(cx.listener(|this, _, window, cx| {
                                    this.close_viewer(cx);
                                    this.focus_handle.focus(window, cx);
                                })),
                        )
                        .child(
                            Button::new("v-prev")
                                .ghost()
                                .icon(gpui_component::IconName::ChevronLeft)
                                .tooltip("上一張")
                                .on_click(cx.listener(|this, _, _, cx| this.viewer_step(-1, cx))),
                        )
                        .child(
                            Button::new("v-next")
                                .ghost()
                                .icon(gpui_component::IconName::ChevronRight)
                                .tooltip("下一張")
                                .on_click(cx.listener(|this, _, _, cx| this.viewer_step(1, cx))),
                        )
                        .child(
                            Button::new("v-zin")
                                .ghost()
                                .icon(gpui_component::IconName::Plus)
                                .tooltip("放大")
                                .on_click(cx.listener(|this, _, _, cx| {
                                    if let Some(v) = this.viewer.as_mut() {
                                        v.zoom.zoom = clamp_zoom(v.zoom.zoom * 1.2);
                                        cx.notify();
                                    }
                                })),
                        )
                        .child(
                            Button::new("v-zout")
                                .ghost()
                                .icon(gpui_component::IconName::Minus)
                                .tooltip("縮小")
                                .on_click(cx.listener(|this, _, _, cx| {
                                    if let Some(v) = this.viewer.as_mut() {
                                        v.zoom.zoom = clamp_zoom(v.zoom.zoom / 1.2);
                                        cx.notify();
                                    }
                                })),
                        )
                        .child(
                            Button::new("v-zreset")
                                .ghost()
                                .label(format!("{:.0}%", zoom * 100.0))
                                .tooltip("重設縮放")
                                .on_click(cx.listener(|this, _, _, cx| {
                                    if let Some(v) = this.viewer.as_mut() {
                                        v.zoom = reset_zoom_state();
                                        cx.notify();
                                    }
                                })),
                        )
                        .child(
                            Button::new("v-reveal")
                                .ghost()
                                .icon(gpui_component::IconName::ExternalLink)
                                .tooltip("在檔案管理器顯示")
                                .on_click(cx.listener(|this, _, _, cx| this.reveal_focus(cx))),
                        )
                        .child(div().flex_1())
                        .child(
                            v_flex()
                                .items_end()
                                .child(
                                    div()
                                        .text_sm()
                                        .font_weight(FontWeight::SEMIBOLD)
                                        .text_color(theme.viewer_text)
                                        .child(name),
                                )
                                .child(
                                    div()
                                        .text_xs()
                                        .text_color(theme.viewer_muted)
                                        .child(pos),
                                ),
                        ),
                )
                .child(
                    div()
                        .flex_1()
                        .flex()
                        .items_center()
                        .justify_center()
                        .overflow_hidden()
                        .p_4()
                        .child(if let Some(msg) = message {
                            div()
                                .px_4()
                                .py_3()
                                .rounded(px(8.))
                                .bg(rgb(0x1f2937))
                                .text_color(theme.danger_text)
                                .child(msg)
                                .into_any_element()
                        } else if let Some(display_path) = display {
                            let base = 720.0 * zoom as f32;
                            img(display_path)
                                .object_fit(ObjectFit::Contain)
                                .w(px(base))
                                .h(px(base))
                                .into_any_element()
                        } else {
                            div()
                                .text_sm()
                                .text_color(theme.viewer_muted)
                                .child("載入中…")
                                .into_any_element()
                        }),
                ),
        )
    }

    pub(super) fn render_rename(
        &self,
        theme: Theme,
        cx: &mut Context<Self>,
    ) -> Option<impl IntoElement> {
        let draft = self.rename.as_ref()?;
        Some(
            div()
                .id("rename")
                .absolute()
                .inset_0()
                .flex()
                .items_center()
                .justify_center()
                .bg(black().opacity(0.35))
                .child(
                    v_flex()
                        .w(px(400.))
                        .gap_3()
                        .p_5()
                        .rounded(cx.theme().radius_lg)
                        .bg(theme.surface)
                        .border_1()
                        .border_color(theme.line)
                        .child(
                            div()
                                .text_lg()
                                .font_weight(FontWeight::SEMIBOLD)
                                .text_color(theme.primary_text)
                                .child("重新命名"),
                        )
                        .child(Input::new(&draft.input))
                        .child(
                            h_flex()
                                .gap_2()
                                .justify_end()
                                .child(
                                    Button::new("rn-cancel").outline().label("取消").on_click(
                                        cx.listener(|this, _, window, cx| {
                                            this.rename = None;
                                            this.focus_handle.focus(window, cx);
                                            cx.notify();
                                        }),
                                    ),
                                )
                                .child(
                                    Button::new("rn-ok").primary().label("確定").on_click(
                                        cx.listener(|this, _, _, cx| {
                                            this.commit_rename(cx);
                                        }),
                                    ),
                                ),
                        ),
                ),
        )
    }

    pub(super) fn render_drop_rename(
        &self,
        theme: Theme,
        cx: &mut Context<Self>,
    ) -> Option<impl IntoElement> {
        let plan = self.drop_rename.as_ref()?;
        let radius = cx.theme().radius;
        let lines: Vec<AnyElement> = plan
            .items
            .iter()
            .take(12)
            .map(|item| {
                let src = PathBuf::from(&item.source_path)
                    .file_name()
                    .and_then(|n| n.to_str())
                    .unwrap_or("?")
                    .to_string();
                let dst = PathBuf::from(&item.target_path)
                    .file_name()
                    .and_then(|n| n.to_str())
                    .unwrap_or("?")
                    .to_string();
                let line = if item.should_skip {
                    format!("略過 {src}")
                } else {
                    format!("{src} → {dst}")
                };
                div()
                    .text_sm()
                    .text_color(theme.secondary_text)
                    .child(line)
                    .into_any_element()
            })
            .collect();
        let more = format!("共 {} 項", plan.total);

        Some(
            div()
                .id("drop-rename")
                .absolute()
                .inset_0()
                .flex()
                .items_center()
                .justify_center()
                .bg(black().opacity(0.35))
                .child(
                    v_flex()
                        .w(px(520.))
                        .max_h(px(480.))
                        .gap_3()
                        .p_5()
                        .rounded(cx.theme().radius_lg)
                        .bg(theme.surface)
                        .border_1()
                        .border_color(theme.line)
                        .child(
                            div()
                                .text_lg()
                                .font_weight(FontWeight::SEMIBOLD)
                                .text_color(theme.primary_text)
                                .child("批次重新命名預覽"),
                        )
                        .child(
                            div()
                                .text_xs()
                                .text_color(theme.muted_text)
                                .child("選取順序：來源在前，最後一張為目標。取消不會改檔。"),
                        )
                        .child(
                            div()
                                .id("drop-plan-list")
                                .flex_1()
                                .p_3()
                                .rounded(radius)
                                .bg(theme.tile_frame)
                                .overflow_y_scroll()
                                .child(v_flex().gap_1().children(lines)),
                        )
                        .child(div().text_xs().text_color(theme.muted_text).child(more))
                        .child(
                            h_flex()
                                .gap_2()
                                .justify_end()
                                .child(
                                    Button::new("dr-cancel").outline().label("取消").on_click(
                                        cx.listener(|this, _, _, cx| {
                                            this.drop_rename = None;
                                            cx.notify();
                                        }),
                                    ),
                                )
                                .child(
                                    Button::new("dr-ok")
                                        .primary()
                                        .label("確認重新命名")
                                        .on_click(cx.listener(|this, _, _, cx| {
                                            this.commit_drop_rename(cx);
                                        })),
                                ),
                        ),
                ),
        )
    }
}
