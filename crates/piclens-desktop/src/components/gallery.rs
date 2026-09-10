use crate::theme::{self, metrics};
use egui::{AtomExt, Color32, Stroke};

pub enum GalleryTilePreview<'a> {
    Folder {
        color: Color32,
    },
    Image {
        texture: &'a egui::TextureHandle,
        uv: egui::Rect,
    },
    Empty,
}

pub struct GalleryTile<'a> {
    pub id_source: &'a str,
    pub label: &'a str,
    pub accessible_label: &'a str,
    pub hover_text: &'a str,
    pub preview: GalleryTilePreview<'a>,
    pub selected: bool,
    pub stroke: Option<Stroke>,
    pub width: f32,
    pub height: f32,
}

impl GalleryTile<'_> {
    pub fn show(self, ui: &mut egui::Ui) -> egui::Response {
        ui.push_id(self.id_source, |ui| {
            ui.spacing_mut().button_padding.x = metrics::SPACE_2;
            let palette = theme::palette(ui.ctx());
            ui.visuals_mut().widgets.inactive.weak_bg_fill = palette.card;
            let preview_size = egui::Vec2::splat(gallery_thumbnail_size(self.width));
            let content_id = ui.make_persistent_id("gallery-tile-content");
            let content_size = egui::vec2(self.width - 16.0, self.height - 12.0);
            let contents =
                egui::AtomLayout::new((egui::Atom::default().atom_size(preview_size), self.label))
                    .direction(egui::Direction::TopDown)
                    .align2(egui::Align2::LEFT_TOP)
                    .wrap_mode(egui::TextWrapMode::Truncate)
                    .min_size(content_size)
                    .max_size(content_size);
            let mut button = egui::Button::new(
                egui::Atom::layout(contents)
                    .atom_id(content_id)
                    .atom_size(content_size)
                    .atom_shrink(true)
                    .atom_max_size(content_size),
            )
            .selected(self.selected)
            .corner_radius(metrics::RADIUS_SURFACE)
            .frame_when_inactive(true)
            .truncate()
            .min_size(egui::vec2(self.width, self.height));
            if let Some(stroke) = self.stroke {
                button = button.stroke(stroke);
            }
            let layout_response = button.atom_ui(ui);
            if let Some(content_rect) = layout_response.rect(content_id) {
                let preview_rect = egui::Rect::from_min_size(content_rect.min, preview_size);
                match self.preview {
                    GalleryTilePreview::Folder { color } => {
                        theme::Icon::Folder.image(48.0).tint(color).paint_at(
                            ui,
                            egui::Rect::from_center_size(
                                preview_rect.center(),
                                egui::Vec2::splat(48.0),
                            ),
                        );
                    }
                    GalleryTilePreview::Image { texture, uv } => {
                        egui::Image::from_texture(texture)
                            .uv(uv)
                            .corner_radius(4)
                            .paint_at(ui, preview_rect);
                    }
                    GalleryTilePreview::Empty => {}
                }
            }
            let response = layout_response.response.on_hover_text(self.hover_text);
            super::focus_ring(ui, &response);
            response.widget_info(|| {
                egui::WidgetInfo::selected(
                    egui::WidgetType::Button,
                    true,
                    self.selected,
                    self.accessible_label.to_owned(),
                )
            });
            response
        })
        .inner
    }
}

pub fn gallery_tile_height(tile_width: f32) -> f32 {
    gallery_thumbnail_size(tile_width) + 38.0
}

pub fn gallery_thumbnail_size(tile_width: f32) -> f32 {
    (tile_width - 16.0).max(48.0)
}
