//! Bundled fonts so Traditional Chinese filenames share one UI face.

use std::borrow::Cow;

use anyhow::Result;
use gpui::App;

const TEXT_FONTS: &[&[u8]] = &[
    include_bytes!("../../../assets/Fonts/NotoSansCJKtc-Regular.otf"),
    include_bytes!("../../../assets/Fonts/NotoSansCJKtc-Medium.otf"),
    include_bytes!("../../../assets/Fonts/NotoSansCJKtc-Bold.otf"),
];

pub fn register_fonts(cx: &App) -> Result<()> {
    cx.text_system().add_fonts(
        TEXT_FONTS
            .iter()
            .map(|font| Cow::Borrowed(*font))
            .collect::<Vec<_>>(),
    )?;
    Ok(())
}
