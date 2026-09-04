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
pub(crate) enum Icon {
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

    pub(crate) fn image(self, size: f32) -> egui::Image<'static> {
        egui::Image::new(self.uri()).fit_to_exact_size(egui::Vec2::splat(size))
    }
}

fn register_icons(ctx: &egui::Context) {
    for (_, uri, bytes) in ICONS {
        ctx.include_bytes(*uri, *bytes);
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub(crate) struct Palette {
    pub app_background: Color32,
    pub command_surface: Color32,
    pub sidebar: Color32,
    pub content: Color32,
    pub tile: Color32,
    pub border: Color32,
    pub primary: Color32,
    pub secondary: Color32,
    pub accent: Color32,
    pub selected: Color32,
    pub danger: Color32,
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
    app_background: Color32::from_rgb(245, 246, 248),
    command_surface: Color32::from_rgb(252, 252, 253),
    sidebar: Color32::from_rgb(248, 249, 251),
    content: Color32::WHITE,
    tile: Color32::from_rgb(242, 243, 245),
    border: Color32::from_rgb(225, 228, 233),
    primary: Color32::from_rgb(29, 32, 38),
    secondary: Color32::from_rgb(98, 105, 117),
    accent: Color32::from_rgb(73, 104, 232),
    selected: Color32::from_rgb(232, 238, 255),
    danger: Color32::from_rgb(183, 35, 35),
    viewer_canvas: Color32::from_rgb(17, 20, 26),
    viewer_text: Color32::WHITE,
    viewer_muted: Color32::from_rgb(174, 182, 194),
    viewer_error: Color32::from_rgb(255, 150, 150),
    viewer_control: Color32::from_rgb(37, 42, 51),
    viewer_control_hover: Color32::from_rgb(52, 59, 71),
    viewer_control_hover_text: Color32::WHITE,
    viewer_control_border: Color32::from_rgb(65, 73, 87),
    drag_target: Color32::from_rgb(35, 110, 210),
};

const DARK: Palette = Palette {
    app_background: Color32::from_rgb(21, 24, 29),
    command_surface: Color32::from_rgb(27, 31, 38),
    sidebar: Color32::from_rgb(24, 28, 34),
    content: Color32::from_rgb(18, 21, 26),
    tile: Color32::from_rgb(32, 36, 43),
    border: Color32::from_rgb(61, 67, 77),
    primary: Color32::from_rgb(232, 235, 240),
    secondary: Color32::from_rgb(170, 177, 188),
    accent: Color32::from_rgb(129, 156, 255),
    selected: Color32::from_rgb(49, 64, 104),
    danger: Color32::from_rgb(255, 124, 124),
    viewer_canvas: Color32::from_rgb(12, 14, 18),
    viewer_text: Color32::WHITE,
    viewer_muted: Color32::from_rgb(174, 182, 194),
    viewer_error: Color32::from_rgb(255, 150, 150),
    viewer_control: Color32::from_rgb(37, 42, 51),
    viewer_control_hover: Color32::from_rgb(52, 59, 71),
    viewer_control_hover_text: Color32::WHITE,
    viewer_control_border: Color32::from_rgb(65, 73, 87),
    drag_target: Color32::from_rgb(112, 166, 255),
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

pub(crate) fn palette(ctx: &egui::Context) -> Palette {
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

        style.visuals.panel_fill = palette.app_background;
        style.visuals.window_fill = palette.command_surface;
        style.visuals.faint_bg_color = palette.tile;
        style.visuals.extreme_bg_color = palette.content;
        style.visuals.error_fg_color = palette.danger;
        style.visuals.selection.bg_fill = palette.selected;
        style.visuals.selection.stroke = egui::Stroke::new(1.0, palette.accent);
        style.visuals.hyperlink_color = palette.accent;
        style.visuals.widgets.noninteractive.fg_stroke.color = palette.primary;
        style.visuals.widgets.noninteractive.bg_stroke.color = palette.border;
        style.visuals.widgets.inactive.fg_stroke.color = palette.primary;
        style.visuals.widgets.inactive.bg_fill = palette.tile;
        style.visuals.widgets.inactive.weak_bg_fill = palette.tile;
        style.visuals.widgets.inactive.bg_stroke.color = palette.border;
        style.visuals.widgets.hovered.fg_stroke.color = palette.primary;
        style.visuals.widgets.hovered.bg_fill = palette.selected;
        style.visuals.widgets.hovered.weak_bg_fill = palette.selected;
        style.visuals.widgets.hovered.bg_stroke.color = palette.accent;
        style.visuals.widgets.active.fg_stroke.color = palette.primary;
        style.visuals.widgets.active.bg_fill = palette.selected;
        style.visuals.widgets.active.weak_bg_fill = palette.selected;
        style.visuals.widgets.active.bg_stroke.color = palette.accent;
        let control_radius = egui::CornerRadius::same(5);
        style.visuals.widgets.noninteractive.corner_radius = control_radius;
        style.visuals.widgets.inactive.corner_radius = control_radius;
        style.visuals.widgets.hovered.corner_radius = control_radius;
        style.visuals.widgets.active.corner_radius = control_radius;
        style.visuals.widgets.open.corner_radius = control_radius;
        style.visuals.window_corner_radius = egui::CornerRadius::same(6);
        style.visuals.menu_corner_radius = egui::CornerRadius::same(6);
        style.spacing.item_spacing = egui::vec2(8.0, 8.0);
        style.spacing.button_padding = egui::vec2(12.0, 6.0);
        style.spacing.interact_size.y = 32.0;
        style
            .text_styles
            .insert(TextStyle::Heading, bold_font(24.0));
        style
            .text_styles
            .insert(TextStyle::Body, FontId::new(15.0, FontFamily::Proportional));
        style
            .text_styles
            .insert(TextStyle::Button, medium_font(15.0));
        style.text_styles.insert(
            TextStyle::Small,
            FontId::new(12.5, FontFamily::Proportional),
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
        app_background: window,
        command_surface: window,
        sidebar: window,
        content: window,
        tile: button,
        border: color(COLOR_WINDOWFRAME),
        primary: text,
        secondary: text,
        accent: highlight,
        selected: highlight,
        danger: text,
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
