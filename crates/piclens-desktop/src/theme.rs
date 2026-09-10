//! PicLens fonts and semantic visual styles.

use std::{
    sync::Arc,
    time::{Duration, Instant},
};

use egui::{Color32, FontData, FontDefinitions, FontFamily, FontId, TextStyle};

const REGULAR_NAME: &str = "Noto Sans CJK TC Regular";
const MEDIUM_NAME: &str = "Noto Sans CJK TC Medium";
const BOLD_NAME: &str = "Noto Sans CJK TC Bold";
const MEDIUM_FAMILY: &str = "PicLens Medium";
const BOLD_FAMILY: &str = "PicLens Bold";
const BRAND_MARK_ID: &str = "piclens-brand-mark";

const REGULAR: &[u8] = include_bytes!("../../../assets/Fonts/NotoSansCJKtc-Regular.otf");
const MEDIUM: &[u8] = include_bytes!("../../../assets/Fonts/NotoSansCJKtc-Medium.otf");
const BOLD: &[u8] = include_bytes!("../../../assets/Fonts/NotoSansCJKtc-Bold.otf");
const BRAND_MARK: &[u8] =
    include_bytes!("../../../assets/Square44x44Logo.targetsize-48_altform-lightunplated.png");

const THEME_STATE_ID: &str = "piclens-theme-state";
const SYSTEM_THEME_POLL_INTERVAL: Duration = Duration::from_secs(1);

macro_rules! icons {
    ($($variant:ident => $file:literal),* $(,)?) => {
        &[$((
            Icon::$variant,
            concat!("bytes://piclens-icon-", $file, ".svg"),
            include_bytes!(concat!("../../../assets/Icons/Lucide/", $file, ".svg")).as_slice(),
        )),*]
    };
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Hash)]
pub enum Icon {
    ArrowLeft,
    ArrowRight,
    ChevronLeft,
    ChevronRight,
    Folder,
    FolderOpen,
    Image,
    Images,
    Minus,
    PanelLeft,
    Pencil,
    Plus,
    Refresh,
    Search,
    Trash,
}

const ICONS: &[(Icon, &str, &[u8])] = icons! {
    ArrowLeft => "arrow-left",
    ArrowRight => "arrow-right",
    ChevronLeft => "chevron-left",
    ChevronRight => "chevron-right",
    Folder => "folder",
    FolderOpen => "folder-open",
    Image => "image",
    Images => "images",
    Minus => "minus",
    PanelLeft => "panel-left",
    Pencil => "pencil",
    Plus => "plus",
    Refresh => "refresh-cw",
    Search => "search",
    Trash => "trash-2",
};

impl Icon {
    fn uri(self) -> &'static str {
        ICONS
            .iter()
            .find(|(icon, _, _)| *icon == self)
            .map_or("", |(_, uri, _)| *uri)
    }

    pub fn image(self, size: f32) -> egui::Image<'static> {
        egui::Image::new(self.uri()).fit_to_exact_size(egui::Vec2::splat(size))
    }
}

fn register_icons(ctx: &egui::Context) {
    for (_, uri, bytes) in ICONS {
        ctx.include_bytes(*uri, *bytes);
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct Palette {
    pub background: Color32,
    pub foreground: Color32,
    pub card: Color32,
    pub popover: Color32,
    pub sidebar: Color32,
    pub muted: Color32,
    pub muted_foreground: Color32,
    pub primary: Color32,
    pub primary_foreground: Color32,
    pub accent: Color32,
    pub accent_foreground: Color32,
    pub destructive: Color32,
    pub destructive_foreground: Color32,
    pub border: Color32,
    pub input: Color32,
    pub ring: Color32,
    pub viewer_canvas: Color32,
    pub viewer_text: Color32,
    pub viewer_muted: Color32,
    pub viewer_error: Color32,
    pub viewer_control: Color32,
    pub viewer_control_hover: Color32,
    pub viewer_control_hover_text: Color32,
    pub viewer_control_border: Color32,
    pub drag_target: Color32,
}

#[derive(Clone)]
struct ThemeState {
    high_contrast: Option<Palette>,
    next_refresh: Instant,
}

const LIGHT: Palette = Palette {
    background: Color32::from_rgb(255, 255, 255),
    foreground: Color32::from_rgb(24, 24, 27),
    card: Color32::from_rgb(255, 255, 255),
    popover: Color32::from_rgb(255, 255, 255),
    sidebar: Color32::from_rgb(250, 250, 250),
    muted: Color32::from_rgb(244, 244, 245),
    muted_foreground: Color32::from_rgb(113, 113, 122),
    primary: Color32::from_rgb(24, 24, 27),
    primary_foreground: Color32::from_rgb(250, 250, 250),
    accent: Color32::from_rgb(244, 244, 245),
    accent_foreground: Color32::from_rgb(24, 24, 27),
    destructive: Color32::from_rgb(185, 28, 28),
    destructive_foreground: Color32::from_rgb(255, 255, 255),
    border: Color32::from_rgb(228, 228, 231),
    input: Color32::from_rgb(212, 212, 216),
    ring: Color32::from_rgb(113, 113, 122),
    viewer_canvas: Color32::from_rgb(9, 9, 11),
    viewer_text: Color32::from_rgb(250, 250, 250),
    viewer_muted: Color32::from_rgb(161, 161, 170),
    viewer_error: Color32::from_rgb(252, 165, 165),
    viewer_control: Color32::from_rgb(24, 24, 27),
    viewer_control_hover: Color32::from_rgb(39, 39, 42),
    viewer_control_hover_text: Color32::from_rgb(250, 250, 250),
    viewer_control_border: Color32::from_rgb(63, 63, 70),
    drag_target: Color32::from_rgb(24, 24, 27),
};

const DARK: Palette = Palette {
    background: Color32::from_rgb(9, 9, 11),
    foreground: Color32::from_rgb(250, 250, 250),
    card: Color32::from_rgb(24, 24, 27),
    popover: Color32::from_rgb(24, 24, 27),
    sidebar: Color32::from_rgb(24, 24, 27),
    muted: Color32::from_rgb(39, 39, 42),
    muted_foreground: Color32::from_rgb(161, 161, 170),
    primary: Color32::from_rgb(250, 250, 250),
    primary_foreground: Color32::from_rgb(24, 24, 27),
    accent: Color32::from_rgb(39, 39, 42),
    accent_foreground: Color32::from_rgb(250, 250, 250),
    destructive: Color32::from_rgb(252, 165, 165),
    destructive_foreground: Color32::from_rgb(69, 10, 10),
    border: Color32::from_rgb(63, 63, 70),
    input: Color32::from_rgb(82, 82, 91),
    ring: Color32::from_rgb(161, 161, 170),
    viewer_canvas: Color32::from_rgb(9, 9, 11),
    viewer_text: Color32::from_rgb(250, 250, 250),
    viewer_muted: Color32::from_rgb(161, 161, 170),
    viewer_error: Color32::from_rgb(252, 165, 165),
    viewer_control: Color32::from_rgb(24, 24, 27),
    viewer_control_hover: Color32::from_rgb(39, 39, 42),
    viewer_control_hover_text: Color32::from_rgb(250, 250, 250),
    viewer_control_border: Color32::from_rgb(63, 63, 70),
    drag_target: Color32::from_rgb(250, 250, 250),
};

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
    fonts.families.insert(
        FontFamily::Name(MEDIUM_FAMILY.into()),
        vec![MEDIUM_NAME.into(), REGULAR_NAME.into()],
    );
    fonts.families.insert(
        FontFamily::Name(BOLD_FAMILY.into()),
        vec![BOLD_NAME.into(), REGULAR_NAME.into()],
    );
    ctx.set_fonts(fonts);
    register_icons(ctx);
    egui_extras::install_image_loaders(ctx);

    let mark = image::load_from_memory(BRAND_MARK)
        .expect("embedded PicLens brand mark must be valid PNG")
        .into_rgba8();
    let size = [mark.width() as usize, mark.height() as usize];
    let texture = ctx.load_texture(
        BRAND_MARK_ID,
        egui::ColorImage::from_rgba_unmultiplied(size, mark.as_raw()),
        egui::TextureOptions::LINEAR,
    );
    ctx.data_mut(|data| data.insert_temp(egui::Id::new(BRAND_MARK_ID), texture));

    ctx.set_theme(egui::ThemePreference::System);
    let high_contrast = system_high_contrast_palette();
    apply_styles(ctx, high_contrast);
    ctx.data_mut(|data| {
        data.insert_temp(
            egui::Id::new(THEME_STATE_ID),
            ThemeState {
                high_contrast,
                next_refresh: Instant::now() + SYSTEM_THEME_POLL_INTERVAL,
            },
        );
    });
}

pub(crate) fn sync_system_accessibility(ctx: &egui::Context) {
    let now = Instant::now();
    let id = egui::Id::new(THEME_STATE_ID);
    let previous = ctx.data_mut(|data| data.get_temp::<ThemeState>(id));
    if previous
        .as_ref()
        .is_some_and(|state| now < state.next_refresh)
    {
        if let Some(state) = previous {
            ctx.request_repaint_after(state.next_refresh.saturating_duration_since(now));
        }
        return;
    }

    let high_contrast = system_high_contrast_palette();
    if previous.as_ref().map(|state| state.high_contrast) != Some(high_contrast) {
        apply_styles(ctx, high_contrast);
        ctx.request_repaint();
    }
    ctx.data_mut(|data| {
        data.insert_temp(
            id,
            ThemeState {
                high_contrast,
                next_refresh: now + SYSTEM_THEME_POLL_INTERVAL,
            },
        );
    });
    ctx.request_repaint_after(SYSTEM_THEME_POLL_INTERVAL);
}

pub fn palette(ctx: &egui::Context) -> Palette {
    let high_contrast = ctx.data_mut(|data| {
        data.get_temp::<ThemeState>(egui::Id::new(THEME_STATE_ID))
            .and_then(|state| state.high_contrast)
    });
    high_contrast.unwrap_or_else(|| match ctx.theme() {
        egui::Theme::Dark => DARK,
        egui::Theme::Light => LIGHT,
    })
}

pub(crate) fn brand_mark(ctx: &egui::Context) -> Option<egui::TextureHandle> {
    ctx.data_mut(|data| data.get_temp(egui::Id::new(BRAND_MARK_ID)))
}

pub(crate) fn medium_font(size: f32) -> FontId {
    FontId::new(size, FontFamily::Name(MEDIUM_FAMILY.into()))
}

fn bold_font(size: f32) -> FontId {
    FontId::new(size, FontFamily::Name(BOLD_FAMILY.into()))
}

fn apply_styles(ctx: &egui::Context, high_contrast: Option<Palette>) {
    ctx.all_styles_mut(|style| {
        let palette = high_contrast.unwrap_or(if style.visuals.dark_mode { DARK } else { LIGHT });

        style.visuals.panel_fill = palette.background;
        style.visuals.window_fill = palette.card;
        style.visuals.faint_bg_color = palette.muted;
        style.visuals.extreme_bg_color = palette.background;
        style.visuals.error_fg_color = palette.destructive;
        style.visuals.selection.bg_fill = palette.accent;
        style.visuals.selection.stroke = egui::Stroke::new(1.0, palette.accent_foreground);
        style.visuals.hyperlink_color = palette.primary;
        style.visuals.widgets.noninteractive.fg_stroke.color = palette.foreground;
        style.visuals.widgets.noninteractive.bg_stroke.color = palette.border;
        style.visuals.widgets.inactive.fg_stroke.color = palette.foreground;
        style.visuals.widgets.inactive.bg_fill = palette.background;
        style.visuals.widgets.inactive.weak_bg_fill = palette.background;
        style.visuals.widgets.inactive.bg_stroke =
            egui::Stroke::new(metrics::BORDER, palette.border);
        style.visuals.widgets.hovered.fg_stroke.color = palette.accent_foreground;
        style.visuals.widgets.hovered.bg_fill = palette.accent;
        style.visuals.widgets.hovered.weak_bg_fill = palette.accent;
        style.visuals.widgets.hovered.bg_stroke.color = palette.primary;
        style.visuals.widgets.active.fg_stroke.color = palette.accent_foreground;
        style.visuals.widgets.active.bg_fill = palette.accent;
        style.visuals.widgets.active.weak_bg_fill = palette.accent;
        style.visuals.widgets.active.bg_stroke.color = palette.primary;
        style.visuals.widgets.open = style.visuals.widgets.hovered;
        style.visuals.widgets.hovered.expansion = 0.0;
        style.visuals.widgets.active.expansion = 0.0;
        style.visuals.widgets.open.expansion = 0.0;
        let control_radius = egui::CornerRadius::same(metrics::RADIUS_CONTROL);
        style.visuals.widgets.noninteractive.corner_radius = control_radius;
        style.visuals.widgets.inactive.corner_radius = control_radius;
        style.visuals.widgets.hovered.corner_radius = control_radius;
        style.visuals.widgets.active.corner_radius = control_radius;
        style.visuals.widgets.open.corner_radius = control_radius;
        style.visuals.window_corner_radius = egui::CornerRadius::same(metrics::RADIUS_SURFACE);
        style.visuals.menu_corner_radius = egui::CornerRadius::same(metrics::RADIUS_SURFACE);
        style.spacing.item_spacing = egui::Vec2::splat(metrics::SPACE_2);
        style.spacing.button_padding = egui::vec2(12.0, 6.0);
        style.spacing.interact_size.y = metrics::CONTROL_HEIGHT;
        style
            .text_styles
            .insert(TextStyle::Heading, bold_font(24.0));
        style
            .text_styles
            .insert(TextStyle::Body, FontId::new(14.0, FontFamily::Proportional));
        style
            .text_styles
            .insert(TextStyle::Button, medium_font(14.0));
        style.text_styles.insert(
            TextStyle::Small,
            FontId::new(12.0, FontFamily::Proportional),
        );
    });
}

#[cfg(not(windows))]
fn system_high_contrast_palette() -> Option<Palette> {
    None
}

#[cfg(windows)]
fn system_high_contrast_palette() -> Option<Palette> {
    use std::ffi::c_void;

    use windows::Win32::{
        Graphics::Gdi::{
            GetSysColor, COLOR_BTNFACE, COLOR_HIGHLIGHT, COLOR_HIGHLIGHTTEXT, COLOR_WINDOW,
            COLOR_WINDOWFRAME, COLOR_WINDOWTEXT, SYS_COLOR_INDEX,
        },
        UI::{
            Accessibility::{HCF_HIGHCONTRASTON, HIGHCONTRASTW},
            WindowsAndMessaging::{
                SystemParametersInfoW, SPI_GETHIGHCONTRAST, SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS,
            },
        },
    };

    let mut contrast = HIGHCONTRASTW {
        cbSize: std::mem::size_of::<HIGHCONTRASTW>() as u32,
        ..Default::default()
    };
    // SAFETY: `contrast` is a correctly sized writable HIGHCONTRASTW for this synchronous call.
    unsafe {
        SystemParametersInfoW(
            SPI_GETHIGHCONTRAST,
            contrast.cbSize,
            Some(std::ptr::from_mut(&mut contrast).cast::<c_void>()),
            SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS(0),
        )
        .ok()?;
    }
    if !contrast.dwFlags.contains(HCF_HIGHCONTRASTON) {
        return None;
    }

    let color = |index: SYS_COLOR_INDEX| {
        // SAFETY: GetSysColor accepts the documented system-color indices used below.
        let value = unsafe { GetSysColor(index) };
        Color32::from_rgb(
            (value & 0xff) as u8,
            ((value >> 8) & 0xff) as u8,
            ((value >> 16) & 0xff) as u8,
        )
    };
    let window = color(COLOR_WINDOW);
    let text = color(COLOR_WINDOWTEXT);
    let highlight = color(COLOR_HIGHLIGHT);
    let highlight_text = color(COLOR_HIGHLIGHTTEXT);
    let button = color(COLOR_BTNFACE);
    Some(Palette {
        background: window,
        foreground: text,
        card: window,
        popover: window,
        sidebar: window,
        muted: button,
        muted_foreground: text,
        primary: highlight,
        primary_foreground: highlight_text,
        accent: highlight,
        accent_foreground: highlight_text,
        destructive: highlight,
        destructive_foreground: highlight_text,
        border: color(COLOR_WINDOWFRAME),
        input: color(COLOR_WINDOWFRAME),
        ring: highlight,
        viewer_canvas: window,
        viewer_text: text,
        viewer_muted: text,
        viewer_error: text,
        viewer_control: button,
        viewer_control_hover: highlight,
        viewer_control_hover_text: highlight_text,
        viewer_control_border: color(COLOR_WINDOWFRAME),
        drag_target: highlight_text,
    })
}

/// Shared logical-point measurements for PicLens components.
pub mod metrics {
    pub const SPACE_1: f32 = 4.0;
    pub const SPACE_2: f32 = 8.0;
    pub const SPACE_3: f32 = 12.0;
    pub const SPACE_4: f32 = 16.0;
    pub const SPACE_6: f32 = 24.0;
    pub const CONTROL_HEIGHT: f32 = 36.0;
    pub const COMPACT_HEIGHT: f32 = 28.0;
    pub const ICON_SIZE: f32 = 16.0;
    pub const RADIUS_CONTROL: u8 = 6;
    pub const RADIUS_SURFACE: u8 = 10;
    pub const BORDER: f32 = 1.0;
}
