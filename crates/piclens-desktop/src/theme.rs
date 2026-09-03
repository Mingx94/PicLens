//! PicLens fonts and initial light visual style.

use std::sync::Arc;

use egui::{Color32, FontData, FontDefinitions, FontFamily, FontId, TextStyle, Visuals};

const REGULAR_NAME: &str = "Noto Sans CJK TC Regular";
const MEDIUM_NAME: &str = "Noto Sans CJK TC Medium";
const BOLD_NAME: &str = "Noto Sans CJK TC Bold";

const REGULAR: &[u8] = include_bytes!("../../../assets/Fonts/NotoSansCJKtc-Regular.otf");
const MEDIUM: &[u8] = include_bytes!("../../../assets/Fonts/NotoSansCJKtc-Medium.otf");
const BOLD: &[u8] = include_bytes!("../../../assets/Fonts/NotoSansCJKtc-Bold.otf");

pub fn install(ctx: &egui::Context) {
    let mut fonts = FontDefinitions::default();
    fonts.font_data.insert(
        REGULAR_NAME.into(),
        Arc::new(FontData::from_static(REGULAR)),
    );
    fonts
        .font_data
        .insert(MEDIUM_NAME.into(), Arc::new(FontData::from_static(MEDIUM)));
    fonts
        .font_data
        .insert(BOLD_NAME.into(), Arc::new(FontData::from_static(BOLD)));
    if let Some(family) = fonts.families.get_mut(&FontFamily::Proportional) {
        family.insert(0, REGULAR_NAME.into());
    }
    ctx.set_fonts(fonts);

    ctx.all_styles_mut(|style| {
        style.visuals = Visuals::light();
        let app_background = Color32::from_rgb(245, 246, 248);
        let content = Color32::WHITE;
        let tile = Color32::from_rgb(242, 243, 245);
        let border = Color32::from_rgb(225, 228, 233);
        let primary = Color32::from_rgb(29, 32, 38);
        let accent = Color32::from_rgb(73, 104, 232);
        let selected = Color32::from_rgb(232, 238, 255);

        style.visuals.panel_fill = app_background;
        style.visuals.window_fill = Color32::WHITE;
        style.visuals.faint_bg_color = tile;
        style.visuals.extreme_bg_color = content;
        style.visuals.selection.bg_fill = selected;
        style.visuals.selection.stroke = egui::Stroke::new(1.0, accent);
        style.visuals.hyperlink_color = accent;
        style.visuals.widgets.noninteractive.fg_stroke.color = primary;
        style.visuals.widgets.noninteractive.bg_stroke.color = border;
        style.visuals.widgets.inactive.fg_stroke.color = primary;
        style.visuals.widgets.inactive.bg_stroke.color = border;
        style.visuals.widgets.hovered.fg_stroke.color = primary;
        style.visuals.widgets.hovered.bg_stroke.color = accent;
        style.visuals.widgets.active.fg_stroke.color = primary;
        style.visuals.widgets.active.bg_stroke.color = accent;
        style.spacing.item_spacing = egui::vec2(8.0, 8.0);
        style.spacing.button_padding = egui::vec2(12.0, 6.0);
        style.spacing.interact_size.y = 32.0;
        style.text_styles.insert(
            TextStyle::Heading,
            FontId::new(24.0, FontFamily::Proportional),
        );
        style
            .text_styles
            .insert(TextStyle::Body, FontId::new(15.0, FontFamily::Proportional));
        style.text_styles.insert(
            TextStyle::Button,
            FontId::new(15.0, FontFamily::Proportional),
        );
        style.text_styles.insert(
            TextStyle::Small,
            FontId::new(12.5, FontFamily::Proportional),
        );
    });
}
