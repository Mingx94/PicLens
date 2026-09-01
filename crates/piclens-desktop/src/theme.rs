//! PicLens fonts and initial light visual style.

use std::sync::Arc;

use egui::{Color32, FontData, FontDefinitions, FontFamily, Visuals};

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
        style.visuals.panel_fill = Color32::from_rgb(248, 249, 251);
        style.visuals.window_fill = Color32::WHITE;
        style.visuals.selection.bg_fill = Color32::from_rgb(31, 111, 235);
        style.spacing.item_spacing = egui::vec2(8.0, 8.0);
    });
}
