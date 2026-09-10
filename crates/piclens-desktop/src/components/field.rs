use egui::{Id, Response, Ui};

use crate::theme::{self, metrics};

pub struct Input<'a> {
    id: Id,
    label: &'a str,
    text: &'a mut String,
    hint: &'a str,
    width: f32,
    framed: bool,
}

impl<'a> Input<'a> {
    pub fn new(id: Id, label: &'a str, text: &'a mut String) -> Self {
        Self {
            id,
            label,
            text,
            hint: "",
            width: 240.0,
            framed: true,
        }
    }
    pub fn hint(mut self, hint: &'a str) -> Self {
        self.hint = hint;
        self
    }
    pub fn width(mut self, width: f32) -> Self {
        self.width = width;
        self
    }
    pub fn frameless(mut self) -> Self {
        self.framed = false;
        self
    }
    pub fn show(self, ui: &mut Ui) -> Response {
        let previous = self.text.clone();
        let frame = if self.framed {
            super::input_frame(ui.ctx(), self.id)
        } else {
            egui::Frame::NONE
        };
        let frame =
            frame.inner_margin(egui::Margin::symmetric(if self.framed { 10 } else { 0 }, 8));
        let response = ui.add(
            egui::TextEdit::singleline(self.text)
                .id(self.id)
                .hint_text(self.hint)
                .desired_width(self.width)
                .frame(frame),
        );
        response.widget_info(|| {
            let mut info = egui::WidgetInfo::text_edit(
                response.enabled(),
                &previous,
                self.text.as_str(),
                self.hint,
            );
            info.label = Some(self.label.into());
            info
        });
        if self.framed {
            super::focus_ring(ui, &response);
        }
        response
    }
}

pub fn checkbox(ui: &mut Ui, checked: &mut bool, label: &str) -> Response {
    let palette = theme::palette(ui.ctx());
    let response = ui
        .scope(|ui| {
            let widgets = &mut ui.visuals_mut().widgets;
            for state in [
                &mut widgets.inactive,
                &mut widgets.hovered,
                &mut widgets.active,
            ] {
                state.corner_radius = 3.into();
                state.bg_stroke = egui::Stroke::new(metrics::BORDER, palette.input);
                if *checked {
                    state.bg_fill = palette.primary;
                    state.weak_bg_fill = palette.primary;
                    state.fg_stroke.color = palette.primary_foreground;
                }
            }
            ui.checkbox(
                checked,
                egui::RichText::new(label).color(palette.foreground),
            )
        })
        .inner;
    super::focus_ring(ui, &response);
    response
}

pub fn select<R>(
    ui: &mut Ui,
    id: impl egui::AsIdSalt,
    label: &str,
    selected: &str,
    contents: impl FnOnce(&mut Ui) -> R,
) -> egui::InnerResponse<Option<R>> {
    let response = egui::ComboBox::from_id_salt(id)
        .selected_text(selected)
        .show_ui(ui, contents);
    response.response.widget_info(|| {
        egui::WidgetInfo::labeled(
            egui::WidgetType::ComboBox,
            response.response.enabled(),
            label,
        )
    });
    super::focus_ring(ui, &response.response);
    response
}

pub fn slider(
    ui: &mut Ui,
    label: &str,
    value: &mut i32,
    range: std::ops::RangeInclusive<i32>,
    step: f64,
) -> Response {
    let palette = theme::palette(ui.ctx());
    let response = ui
        .scope(|ui| {
            ui.spacing_mut().interact_size.y = 24.0;
            ui.spacing_mut().slider_rail_height = 4.0;
            ui.visuals_mut().widgets.inactive.bg_fill = palette.muted;
            ui.visuals_mut().widgets.inactive.fg_stroke = egui::Stroke::new(1.0, palette.primary);
            ui.visuals_mut().selection.bg_fill = palette.primary;
            ui.add(
                egui::Slider::new(&mut *value, range)
                    .step_by(step)
                    .trailing_fill(true)
                    .handle_shape(egui::style::HandleShape::Circle)
                    .show_value(true),
            )
        })
        .inner;
    response.widget_info(|| egui::WidgetInfo::slider(response.enabled(), f64::from(*value), label));
    super::focus_ring(ui, &response);
    response
}
