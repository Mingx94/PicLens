use egui::{Color32, Response, Stroke, Ui};

use crate::theme::{self, metrics, Icon};

#[derive(Clone, Copy, Debug, Default)]
pub enum ButtonVariant {
    #[default]
    Default,
    Secondary,
    Outline,
    Ghost,
    Destructive,
}

#[derive(Clone, Copy, Debug, Default)]
pub enum ButtonSize {
    #[default]
    Default,
    Small,
    Icon,
}

pub struct Button<'a> {
    label: &'a str,
    icon: Option<Icon>,
    variant: ButtonVariant,
    size: ButtonSize,
    enabled: bool,
}

impl<'a> Button<'a> {
    pub fn new(label: &'a str) -> Self {
        Self {
            label,
            icon: None,
            variant: ButtonVariant::Default,
            size: ButtonSize::Default,
            enabled: true,
        }
    }

    pub fn icon(mut self, icon: Icon) -> Self {
        self.icon = Some(icon);
        self
    }
    pub fn variant(mut self, variant: ButtonVariant) -> Self {
        self.variant = variant;
        self
    }
    pub fn size(mut self, size: ButtonSize) -> Self {
        self.size = size;
        self
    }
    pub fn enabled(mut self, enabled: bool) -> Self {
        self.enabled = enabled;
        self
    }

    pub fn show(self, ui: &mut Ui) -> Response {
        let palette = theme::palette(ui.ctx());
        let current = ui.visuals();
        let foreground = current.widgets.inactive.fg_stroke.color;
        let background = current.widgets.inactive.bg_fill;
        let hover = current.widgets.hovered.bg_fill;
        let hover_text = current.widgets.hovered.fg_stroke.color;
        let (fill, text, hover_fill, hover_fg, border) = match self.variant {
            ButtonVariant::Default => (
                palette.primary,
                palette.primary_foreground,
                blend(palette.primary, palette.primary_foreground),
                palette.primary_foreground,
                Color32::TRANSPARENT,
            ),
            ButtonVariant::Secondary => (
                palette.muted,
                foreground,
                hover,
                hover_text,
                Color32::TRANSPARENT,
            ),
            ButtonVariant::Outline => (
                background,
                foreground,
                hover,
                hover_text,
                current.widgets.inactive.bg_stroke.color,
            ),
            ButtonVariant::Ghost => (
                Color32::TRANSPARENT,
                foreground,
                hover,
                hover_text,
                Color32::TRANSPARENT,
            ),
            ButtonVariant::Destructive => (
                palette.destructive,
                palette.destructive_foreground,
                blend(palette.destructive, palette.destructive_foreground),
                palette.destructive_foreground,
                Color32::TRANSPARENT,
            ),
        };
        let response = ui
            .scope(|ui| {
                let height = match self.size {
                    ButtonSize::Small => metrics::COMPACT_HEIGHT,
                    _ => metrics::CONTROL_HEIGHT,
                };
                ui.spacing_mut().interact_size.y = height;
                ui.spacing_mut().button_padding = egui::vec2(12.0, 4.0);
                let widgets = &mut ui.visuals_mut().widgets;
                for (state, bg, fg) in [
                    (&mut widgets.inactive, fill, text),
                    (&mut widgets.hovered, hover_fill, hover_fg),
                    (&mut widgets.active, hover_fill, hover_fg),
                ] {
                    state.bg_fill = bg;
                    state.weak_bg_fill = bg;
                    state.fg_stroke.color = fg;
                    state.bg_stroke = Stroke::new(metrics::BORDER, border);
                    state.corner_radius = metrics::RADIUS_CONTROL.into();
                    state.expansion = 0.0;
                }
                let mut button = match (self.icon, self.size) {
                    (Some(icon), ButtonSize::Icon) => {
                        egui::Button::image(icon.image(metrics::ICON_SIZE))
                    }
                    (Some(icon), _) => {
                        egui::Button::image_and_text(icon.image(metrics::ICON_SIZE), self.label)
                    }
                    (None, _) => egui::Button::new(self.label),
                }
                .image_tint_follows_text_color(true)
                .frame_when_inactive(!matches!(self.variant, ButtonVariant::Ghost))
                .min_size(egui::vec2(
                    if matches!(self.size, ButtonSize::Icon) {
                        height
                    } else {
                        0.0
                    },
                    height,
                ));
                if matches!(self.size, ButtonSize::Small) {
                    button = button.small();
                }
                ui.add_enabled(self.enabled, button)
            })
            .inner;
        response.widget_info(|| {
            egui::WidgetInfo::labeled(egui::WidgetType::Button, response.enabled(), self.label)
        });
        super::focus_ring(ui, &response);
        if matches!(self.size, ButtonSize::Icon) {
            response.on_hover_text(self.label)
        } else {
            response
        }
    }
}

fn blend(background: Color32, foreground: Color32) -> Color32 {
    let a = background.to_array();
    let b = foreground.to_array();
    let channel = |i: usize| (f32::from(a[i]) * 0.92 + f32::from(b[i]) * 0.08).round() as u8;
    Color32::from_rgb(channel(0), channel(1), channel(2))
}
