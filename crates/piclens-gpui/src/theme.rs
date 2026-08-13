//! PicLens light workbench palette (from docs/design/system.md).
//!
//! Theme is a GPUI global so every view reads one published palette,
//! matching the pattern used by mature GPUI apps.

use gpui::{App, Global, Hsla, rgb};

/// Match the established Qt shell geometry.
pub const COMMAND_BAR_H: f32 = 64.0;
pub const STATUS_BAR_H: f32 = 48.0;
pub const SIDEBAR_W: f32 = 228.0;

/// Bundled CJK UI face. Registered in [`crate::assets`].
pub const UI_FONT_FAMILY: &str = "Noto Sans CJK TC";

/// Published light palette. PicLens is light-only until a full dark set exists.
#[derive(Clone, Copy)]
pub struct Theme {
    pub app_background: Hsla,
    pub command_bar: Hsla,
    pub sidebar: Hsla,
    pub surface: Hsla,
    pub tile_frame: Hsla,
    pub line: Hsla,
    pub strong_line: Hsla,
    pub primary_text: Hsla,
    pub secondary_text: Hsla,
    pub muted_text: Hsla,
    pub hover: Hsla,
    pub selected: Hsla,
    pub accent: Hsla,
    pub accent_soft: Hsla,
    pub viewer_canvas: Hsla,
    pub viewer_bar: Hsla,
    pub viewer_bar_line: Hsla,
    pub viewer_text: Hsla,
    pub viewer_muted: Hsla,
    pub danger_text: Hsla,
}

impl Theme {
    pub fn light() -> Self {
        Self {
            app_background: rgb(0xf5f6f8).into(),
            command_bar: rgb(0xfcfcfd).into(),
            sidebar: rgb(0xf8f9fb).into(),
            surface: rgb(0xffffff).into(),
            tile_frame: rgb(0xf2f3f5).into(),
            line: rgb(0xe1e4e9).into(),
            strong_line: rgb(0xcbd0d8).into(),
            primary_text: rgb(0x1d2026).into(),
            secondary_text: rgb(0x626975).into(),
            muted_text: rgb(0x7a828f).into(),
            hover: rgb(0xeef1f5).into(),
            selected: rgb(0xe8eeff).into(),
            accent: rgb(0x4968e8).into(),
            accent_soft: rgb(0xeef2ff).into(),
            viewer_canvas: rgb(0x11141a).into(),
            viewer_bar: rgb(0x0c0e12).into(),
            viewer_bar_line: rgb(0x22262e).into(),
            viewer_text: rgb(0xf3f4f6).into(),
            viewer_muted: rgb(0x9ca3af).into(),
            danger_text: rgb(0xfca5a5).into(),
        }
    }

    pub fn current(cx: &App) -> Self {
        if cx.has_global::<ActiveTheme>() {
            cx.global::<ActiveTheme>().0
        } else {
            Self::light()
        }
    }
}

#[derive(Clone, Copy)]
struct ActiveTheme(Theme);

impl Global for ActiveTheme {}

/// Publish the startup palette before any window exists.
pub fn init(cx: &mut App) {
    cx.set_global(ActiveTheme(Theme::light()));
}
