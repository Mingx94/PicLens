//! PicLens light workbench palette (from docs/design/system.md).

use gpui::{Hsla, rgb};

/// Main shell background `#F5F6F8`
pub fn app_background() -> Hsla {
    rgb(0xf5f6f8).into()
}

/// Command / status bars `#FCFCFD`
pub fn command_bar() -> Hsla {
    rgb(0xfcfcfd).into()
}

/// Sidebar `#F8F9FB`
pub fn sidebar() -> Hsla {
    rgb(0xf8f9fb).into()
}

/// Raised library surface `#FFFFFF`
pub fn surface() -> Hsla {
    rgb(0xffffff).into()
}

/// Tile frame `#F2F3F5`
pub fn tile_frame() -> Hsla {
    rgb(0xf2f3f5).into()
}

/// Default line `#E1E4E9`
pub fn line() -> Hsla {
    rgb(0xe1e4e9).into()
}

/// Strong line `#CBD0D8`
pub fn strong_line() -> Hsla {
    rgb(0xcbd0d8).into()
}

/// Primary text `#1D2026`
pub fn primary_text() -> Hsla {
    rgb(0x1d2026).into()
}

/// Secondary text `#626975`
pub fn secondary_text() -> Hsla {
    rgb(0x626975).into()
}

/// Muted text `#7A828F`
pub fn muted_text() -> Hsla {
    rgb(0x7a828f).into()
}

/// Hover `#EEF1F5`
pub fn hover() -> Hsla {
    rgb(0xeef1f5).into()
}

/// Selected library item `#E8EEFF`
pub fn selected() -> Hsla {
    rgb(0xe8eeff).into()
}

/// Cobalt accent `#4968E8`
pub fn accent() -> Hsla {
    rgb(0x4968e8).into()
}

/// Soft accent `#EEF2FF`
pub fn accent_soft() -> Hsla {
    rgb(0xeef2ff).into()
}

/// Viewer canvas `#11141A`
pub fn viewer_canvas() -> Hsla {
    rgb(0x11141a).into()
}

pub const COMMAND_BAR_H: f32 = 56.0;
pub const STATUS_BAR_H: f32 = 44.0;
pub const SIDEBAR_W: f32 = 240.0;
