//! Compose the PicLens window from shell, gallery, and overlay pieces.

use gpui::*;
use gpui_component::{h_flex, v_flex, ActiveTheme, Root};

use super::PicLensApp;
use crate::actions::CONTEXT;
use crate::theme::{self, Theme};

impl Focusable for PicLensApp {
    fn focus_handle(&self, _: &App) -> FocusHandle {
        self.focus_handle.clone()
    }
}

impl Render for PicLensApp {
    fn render(&mut self, window: &mut Window, cx: &mut Context<Self>) -> impl IntoElement {
        let theme = Theme::current(cx);
        let folder_title = self.folder_title();
        let folder_path = self.folder_path_label();
        let visible_count = self.visible.len();
        let selected_count = self.selected.len();

        let gallery_body = self.render_gallery(theme, cx);
        let sidebar = self.render_sidebar(theme, cx);
        let viewer_layer = self.render_viewer(theme, cx);
        let rename_layer = self.render_rename(theme, cx);
        let drop_layer = self.render_drop_rename(theme, cx);

        div()
            .id("piclens-root")
            .key_context(CONTEXT)
            .track_focus(&self.focus_handle)
            .size_full()
            .flex()
            .flex_col()
            .font_family(theme::UI_FONT_FAMILY)
            .bg(theme.app_background)
            .text_color(theme.primary_text)
            .on_action(cx.listener(Self::on_open_folder))
            .on_action(cx.listener(Self::on_refresh))
            .on_action(cx.listener(Self::on_history_back))
            .on_action(cx.listener(Self::on_history_forward))
            .on_action(cx.listener(Self::on_toggle_sidebar))
            .on_action(cx.listener(Self::on_toggle_gallery_mode))
            .on_action(cx.listener(Self::on_cycle_sort))
            .on_action(cx.listener(Self::on_toggle_include_subfolders))
            .on_action(cx.listener(Self::on_focus_search))
            .on_action(cx.listener(Self::on_close_overlay))
            .on_action(cx.listener(Self::on_clear_selection))
            .on_action(cx.listener(Self::on_select_all))
            .on_action(cx.listener(Self::on_open_viewer))
            .on_action(cx.listener(Self::on_viewer_prev))
            .on_action(cx.listener(Self::on_viewer_next))
            .on_action(cx.listener(Self::on_zoom_in))
            .on_action(cx.listener(Self::on_zoom_out))
            .on_action(cx.listener(Self::on_zoom_reset))
            .on_action(cx.listener(Self::on_trash))
            .on_action(cx.listener(Self::on_rename))
            .on_action(cx.listener(Self::on_drop_rename))
            .on_action(cx.listener(Self::on_convert_jpg))
            .on_action(cx.listener(Self::on_convert_webp))
            .on_action(cx.listener(Self::on_cleanup))
            .on_action(cx.listener(Self::on_reveal))
            .on_action(cx.listener(Self::on_move_up))
            .on_action(cx.listener(Self::on_move_down))
            .on_action(cx.listener(Self::on_move_left))
            .on_action(cx.listener(Self::on_move_right))
            .child(self.render_command_bar(theme, cx))
            .child(
                h_flex()
                    .id("body")
                    .flex_1()
                    .w_full()
                    .overflow_hidden()
                    .child(sidebar)
                    .child(
                        v_flex()
                            .id("library")
                            .flex_1()
                            .h_full()
                            .m_3()
                            .rounded(cx.theme().radius_lg)
                            .bg(theme.surface)
                            .border_1()
                            .border_color(theme.line)
                            .overflow_hidden()
                            .child(self.render_library_header(
                                theme,
                                folder_title,
                                folder_path,
                                visible_count,
                                cx,
                            ))
                            .child(
                                div()
                                    .id("gallery")
                                    .flex_1()
                                    .w_full()
                                    .px_5()
                                    .pb_4()
                                    .overflow_y_scroll()
                                    .child(gallery_body),
                            ),
                    ),
            )
            .child(self.render_status_bar(theme, visible_count, selected_count, cx))
            .children(viewer_layer)
            .children(rename_layer)
            .children(drop_layer)
            .children(Root::render_dialog_layer(window, cx))
            .children(Root::render_sheet_layer(window, cx))
            .children(Root::render_notification_layer(window, cx))
    }
}
