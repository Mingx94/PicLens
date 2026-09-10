use egui::{Context, Frame, Margin, Response, RichText, Stroke, Ui};

use crate::theme::{self, metrics};

#[derive(Clone, Copy)]
pub enum Surface {
    Card,
    Popover,
    Dialog,
    Toolbar,
    Sidebar,
}

pub fn surface_frame(ctx: &Context, surface: Surface) -> Frame {
    let palette = theme::palette(ctx);
    let (fill, radius, margin) = match surface {
        Surface::Card => (palette.card, metrics::RADIUS_SURFACE, 16),
        Surface::Popover => (palette.popover, metrics::RADIUS_SURFACE, 12),
        Surface::Dialog => (palette.popover, metrics::RADIUS_SURFACE, 24),
        Surface::Toolbar => (palette.background, 0, 8),
        Surface::Sidebar => (palette.sidebar, 0, 12),
    };
    let mut frame = Frame::new()
        .fill(fill)
        .stroke(Stroke::new(metrics::BORDER, palette.border))
        .corner_radius(radius)
        .inner_margin(Margin::same(margin));
    if matches!(surface, Surface::Popover | Surface::Dialog) {
        frame.shadow = egui::epaint::Shadow {
            offset: [0, 4],
            blur: 16,
            spread: 0,
            color: egui::Color32::from_black_alpha(28),
        };
    }
    frame
}

pub fn input_frame(ctx: &Context, id: egui::Id) -> Frame {
    let palette = theme::palette(ctx);
    let focused = ctx.memory(|memory| memory.has_focus(id));
    Frame::new()
        .fill(palette.background)
        .stroke(Stroke::new(
            metrics::BORDER,
            if focused { palette.ring } else { palette.input },
        ))
        .corner_radius(metrics::RADIUS_CONTROL)
}

pub fn focus_ring(ui: &Ui, response: &Response) {
    if response.enabled() && response.has_focus() {
        ui.painter().rect_stroke(
            response.rect.expand(2.0),
            metrics::RADIUS_CONTROL,
            Stroke::new(2.0, theme::palette(ui.ctx()).ring),
            egui::StrokeKind::Outside,
        );
    }
}

pub fn badge(ui: &mut Ui, text: impl Into<String>) -> Response {
    let palette = theme::palette(ui.ctx());
    Frame::new()
        .fill(palette.muted)
        .corner_radius(metrics::RADIUS_CONTROL)
        .inner_margin(Margin::symmetric(8, 3))
        .show(ui, |ui| {
            ui.label(RichText::new(text).small().color(palette.foreground))
        })
        .inner
}

pub fn dialog<R>(
    ctx: &Context,
    id: egui::Id,
    contents: impl FnOnce(&mut Ui) -> R,
) -> egui::ModalResponse<R> {
    egui::Modal::new(id)
        .frame(surface_frame(ctx, Surface::Dialog))
        .show(ctx, |ui| {
            ui.set_min_width(360.0);
            ui.set_max_width(480.0);
            contents(ui)
        })
}
