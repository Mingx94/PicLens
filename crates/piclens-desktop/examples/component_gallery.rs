//! Interactive component catalog. No library scanning or file-operation commands.
//! cargo run -p piclens-desktop --example component_gallery -- --dark
//! Add --screenshot <path.png> to capture the catalog and exit.

use eframe::egui;
use piclens_desktop::{
    components::{
        self, Button, ButtonSize, ButtonVariant, GalleryTile, GalleryTilePreview, Input, Surface,
    },
    theme::{self, Icon},
};
use std::{
    path::PathBuf,
    time::{Duration, Instant},
};

struct Catalog {
    query: String,
    checked: bool,
    size: i32,
    sort: usize,
    dialog: bool,
    toast_until: Option<Instant>,
    texture: egui::TextureHandle,
    output: Option<PathBuf>,
    capture_at: Option<Instant>,
}

impl Catalog {
    fn content(&mut self, ui: &mut egui::Ui) {
        let palette = theme::palette(ui.ctx());
        ui.heading("PicLens 元件庫");
        ui.label(
            egui::RichText::new("中性灰階 · 清楚的操作層級 · 原生鍵盤操作")
                .color(palette.muted_foreground),
        );
        ui.add_space(16.0);

        components::surface_frame(ui.ctx(), Surface::Card).show(ui, |ui| {
            ui.set_min_width(ui.available_width());
            ui.strong("按鈕與操作層級");
            ui.add_space(8.0);
            ui.horizontal_wrapped(|ui| {
                Button::new("主要操作").icon(Icon::Plus).show(ui);
                Button::new("次要操作")
                    .variant(ButtonVariant::Secondary)
                    .show(ui);
                Button::new("外框按鈕")
                    .variant(ButtonVariant::Outline)
                    .show(ui);
                Button::new("輕量按鈕")
                    .variant(ButtonVariant::Ghost)
                    .show(ui);
                Button::new("移至回收筒")
                    .icon(Icon::Trash)
                    .variant(ButtonVariant::Destructive)
                    .show(ui);
                Button::new("暫時停用").enabled(false).show(ui);
                Button::new("重新整理")
                    .icon(Icon::Refresh)
                    .variant(ButtonVariant::Ghost)
                    .size(ButtonSize::Icon)
                    .show(ui);
            });
        });
        ui.add_space(8.0);
        components::surface_frame(ui.ctx(), Surface::Card).show(ui, |ui| {
            ui.set_min_width(ui.available_width());
            ui.strong("輸入與篩選");
            ui.add_space(8.0);
            ui.horizontal_wrapped(|ui| {
                Input::new(egui::Id::new("catalog-search"), "搜尋圖片", &mut self.query)
                    .hint("搜尋名稱或路徑…")
                    .width(240.0)
                    .show(ui);
                let selected = ["名稱", "修改時間"][self.sort];
                components::select(ui, "catalog-sort", "排序", selected, |ui| {
                    ui.selectable_value(&mut self.sort, 0, "名稱");
                    ui.selectable_value(&mut self.sort, 1, "修改時間");
                });
                components::checkbox(ui, &mut self.checked, "包含子資料夾");
                components::slider(ui, "縮圖大小", &mut self.size, 120..=240, 20.0);
            });
        });
        ui.add_space(8.0);
        components::surface_frame(ui.ctx(), Surface::Card).show(ui, |ui| {
            ui.set_min_width(ui.available_width());
            ui.horizontal(|ui| {
                ui.strong("圖片卡片");
                components::badge(ui, "2 個項目");
            });
            ui.add_space(8.0);
            ui.horizontal_wrapped(|ui| {
                for (index, label) in ["一般卡片.png", "已選取的圖片.png"].into_iter().enumerate()
                {
                    GalleryTile {
                        id_source: label,
                        label,
                        accessible_label: label,
                        hover_text: label,
                        preview: GalleryTilePreview::Image {
                            texture: &self.texture,
                            uv: egui::Rect::from_min_max(egui::Pos2::ZERO, egui::pos2(1.0, 1.0)),
                        },
                        selected: index == 1,
                        stroke: None,
                        width: 144.0,
                        height: components::gallery_tile_height(144.0),
                    }
                    .show(ui);
                }
            });
        });
        ui.add_space(8.0);
        components::surface_frame(ui.ctx(), Surface::Card).show(ui, |ui| {
            ui.set_min_width(ui.available_width());
            ui.strong("通知與確認");
            ui.horizontal_wrapped(|ui| {
                if Button::new("顯示對話框")
                    .variant(ButtonVariant::Outline)
                    .show(ui)
                    .clicked()
                {
                    self.dialog = true;
                }
                if Button::new("顯示 toast")
                    .variant(ButtonVariant::Secondary)
                    .show(ui)
                    .clicked()
                {
                    self.toast_until = Some(Instant::now() + Duration::from_secs(6));
                }
                components::badge(ui, "原檔保留");
            });
            components::alert(ui, "檔案已存在時會略過，不會覆寫原檔。", false);
        });
    }
}

impl eframe::App for Catalog {
    fn raw_input_hook(&mut self, ctx: &egui::Context, input: &mut egui::RawInput) {
        if let Some(path) = &self.output {
            let screenshot = input.events.iter().find_map(|event| {
                if let egui::Event::Screenshot { image, .. } = event {
                    Some(image.clone())
                } else {
                    None
                }
            });
            if let Some(screenshot) = screenshot {
                let rgba: Vec<u8> = screenshot
                    .pixels
                    .iter()
                    .flat_map(|color| color.to_array())
                    .collect();
                image::save_buffer(
                    path,
                    &rgba,
                    screenshot.width() as u32,
                    screenshot.height() as u32,
                    image::ColorType::Rgba8,
                )
                .expect("save component screenshot");
                self.output = None;
                ctx.send_viewport_cmd(egui::ViewportCommand::Close);
            }
        }
    }

    fn ui(&mut self, ui: &mut egui::Ui, _frame: &mut eframe::Frame) {
        let ctx = ui.ctx().clone();
        if self.output.is_some() {
            ctx.request_repaint_after(Duration::from_millis(50));
        }
        egui::CentralPanel::default()
            .frame(
                egui::Frame::new()
                    .fill(theme::palette(&ctx).background)
                    .inner_margin(24),
            )
            .show(ui, |ui| {
                egui::ScrollArea::vertical().show(ui, |ui| self.content(ui));
            });
        if self.dialog {
            let response = components::dialog(&ctx, egui::Id::new("catalog-dialog"), |ui| {
                ui.heading("確認重新命名");
                ui.label("這是元件展示，不會修改任何檔案。");
                ui.add_space(16.0);
                ui.horizontal(|ui| {
                    if Button::new("確認").show(ui).clicked() {
                        self.dialog = false;
                    }
                    if Button::new("取消")
                        .variant(ButtonVariant::Outline)
                        .show(ui)
                        .clicked()
                    {
                        self.dialog = false;
                    }
                });
            });
            if response.should_close() {
                self.dialog = false;
            }
        } else if let Some(deadline) = self.toast_until {
            if Instant::now() < deadline {
                let response = components::Toast::new(
                    egui::Id::new("catalog-toast"),
                    "重新命名完成：成功 2、略過 0、取消 0、失敗 0",
                )
                .details(true)
                .show(&ctx);
                ctx.request_repaint_after(deadline.saturating_duration_since(Instant::now()));
                if response.dismiss_clicked {
                    self.toast_until = None;
                }
                if response.details_clicked {
                    self.dialog = true;
                }
            } else {
                self.toast_until = None;
            }
        }
        if let Some(deadline) = self.capture_at {
            if Instant::now() >= deadline {
                ctx.send_viewport_cmd(egui::ViewportCommand::Screenshot(egui::UserData::default()));
                ctx.request_repaint();
                self.capture_at = None;
            } else {
                ctx.request_repaint_after(deadline.saturating_duration_since(Instant::now()));
            }
        }
    }
}

fn main() -> eframe::Result<()> {
    let args: Vec<String> = std::env::args().collect();
    let dark = args.iter().any(|arg| arg == "--dark");
    let compact = args.iter().any(|arg| arg == "--compact");
    let dialog = args.iter().any(|arg| arg == "--dialog");
    let output = args
        .windows(2)
        .find(|pair| pair[0] == "--screenshot")
        .map(|pair| PathBuf::from(&pair[1]));
    eframe::run_native(
        "PicLens 元件庫",
        eframe::NativeOptions {
            viewport: egui::ViewportBuilder::default().with_inner_size(if compact {
                [800.0, 800.0]
            } else {
                [1200.0, 900.0]
            }),
            persist_window: false,
            ..Default::default()
        },
        Box::new(move |creation| {
            theme::install(&creation.egui_ctx);
            creation.egui_ctx.set_theme(if dark {
                egui::ThemePreference::Dark
            } else {
                egui::ThemePreference::Light
            });
            let mark = image::load_from_memory(include_bytes!(
                "../../../assets/Square150x150Logo.scale-200.png"
            ))
            .unwrap()
            .into_rgba8();
            let texture = creation.egui_ctx.load_texture(
                "catalog-image",
                egui::ColorImage::from_rgba_unmultiplied(
                    [mark.width() as usize, mark.height() as usize],
                    mark.as_raw(),
                ),
                egui::TextureOptions::LINEAR,
            );
            Ok(Box::new(Catalog {
                query: String::new(),
                checked: true,
                size: 160,
                sort: 0,
                dialog,
                toast_until: Some(Instant::now() + Duration::from_secs(6)),
                texture,
                capture_at: output
                    .as_ref()
                    .map(|_| Instant::now() + Duration::from_secs(1)),
                output,
            }))
        }),
    )
}
