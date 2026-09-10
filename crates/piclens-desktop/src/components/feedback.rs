use egui::{Context, Id, Response, RichText, Ui};

use super::{surface_frame, Button, ButtonSize, ButtonVariant, Surface};
use crate::theme;

pub struct Toast<'a> {
    id: Id,
    message: &'a str,
    error: bool,
    details: bool,
}

#[derive(Default)]
pub struct ToastResponse {
    pub details_clicked: bool,
    pub dismiss_clicked: bool,
}

impl<'a> Toast<'a> {
    pub fn new(id: Id, message: &'a str) -> Self {
        Self {
            id,
            message,
            error: false,
            details: false,
        }
    }
    pub fn error(mut self, error: bool) -> Self {
        self.error = error;
        self
    }
    pub fn details(mut self, details: bool) -> Self {
        self.details = details;
        self
    }

    pub fn show(self, ctx: &Context) -> ToastResponse {
        let palette = theme::palette(ctx);
        let mut response = ToastResponse::default();
        egui::Area::new(self.id)
            .order(egui::Order::Foreground)
            .anchor(egui::Align2::RIGHT_BOTTOM, egui::vec2(-20.0, -68.0))
            .movable(false)
            .show(ctx, |ui| {
                surface_frame(ctx, Surface::Popover).show(ui, |ui| {
                    ui.set_max_width(360.0);
                    let message = ui.label(RichText::new(self.message).color(if self.error {
                        palette.destructive
                    } else {
                        palette.foreground
                    }));
                    ctx.accesskit_node_builder(message.id, |node| {
                        node.set_live(if self.error {
                            egui::accesskit::Live::Assertive
                        } else {
                            egui::accesskit::Live::Polite
                        })
                    });
                    ui.horizontal(|ui| {
                        if self.details {
                            response.details_clicked = Button::new("詳細")
                                .variant(ButtonVariant::Outline)
                                .size(ButtonSize::Small)
                                .show(ui)
                                .clicked();
                        }
                        response.dismiss_clicked = Button::new("關閉通知")
                            .variant(ButtonVariant::Ghost)
                            .size(ButtonSize::Small)
                            .show(ui)
                            .clicked();
                    });
                });
            });
        response
    }
}

pub fn alert(ui: &mut Ui, message: &str, error: bool) -> Response {
    let palette = theme::palette(ui.ctx());
    surface_frame(ui.ctx(), Surface::Card)
        .show(ui, |ui| {
            ui.label(RichText::new(message).color(if error {
                palette.destructive
            } else {
                palette.foreground
            }))
        })
        .inner
}
