//! Bundled fonts and the application icon.

use std::borrow::Cow;
use std::sync::Arc;

use anyhow::Result;
use gpui::{App, Image, ImageFormat};

const TEXT_FONTS: &[&[u8]] = &[
    include_bytes!("../../../assets/Fonts/NotoSansCJKtc-Regular.otf"),
    include_bytes!("../../../assets/Fonts/NotoSansCJKtc-Medium.otf"),
    include_bytes!("../../../assets/Fonts/NotoSansCJKtc-Bold.otf"),
];

const APP_ICON_PNG: &[u8] =
    include_bytes!("../../../assets/Square150x150Logo.scale-200.png");

/// In-app brand mark (same composition as `assets/AppIcon.ico`).
pub fn app_icon() -> Arc<Image> {
    Arc::new(Image::from_bytes(ImageFormat::Png, APP_ICON_PNG.to_vec()))
}

pub fn register_fonts(cx: &App) -> Result<()> {
    cx.text_system().add_fonts(
        TEXT_FONTS
            .iter()
            .map(|font| Cow::Borrowed(*font))
            .collect::<Vec<_>>(),
    )?;
    Ok(())
}
