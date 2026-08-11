mod app;
mod history;

use app::PicLensApp;
use gpui::*;
use gpui_component::{Root, TitleBar};
use piclens_infra::{info, init_file_logger};

fn parse_folder_arg(args: &[String]) -> Option<String> {
    let mut iter = args.iter().skip(1);
    while let Some(arg) = iter.next() {
        if arg == "--folder" {
            return iter.next().cloned();
        }
        if let Some(path) = arg.strip_prefix("--folder=") {
            return Some(path.to_string());
        }
    }
    None
}

fn main() {
    env_logger::Builder::from_env(env_logger::Env::default().default_filter_or("info")).init();
    init_file_logger();
    info("PicLens GPUI starting");

    let initial_folder = parse_folder_arg(&std::env::args().collect::<Vec<_>>());

    let app = gpui_platform::application().with_assets(gpui_component_assets::Assets);
    app.run(move |cx| {
        gpui_component::init(cx);
        cx.activate(true);

        let mut window_size = size(px(1280.), px(800.));
        if let Some(display) = cx.primary_display() {
            let display_size = display.bounds().size;
            window_size.width = window_size.width.min(display_size.width * 0.85);
            window_size.height = window_size.height.min(display_size.height * 0.85);
        }
        let window_bounds = Bounds::centered(None, window_size, cx);

        cx.spawn(async move |cx| {
            let options = WindowOptions {
                window_bounds: Some(WindowBounds::Windowed(window_bounds)),
                window_min_size: Some(size(px(480.), px(320.))),
                kind: WindowKind::Normal,
                ..TitleBar::window_options()
            };

            cx.open_window(options, move |window, cx| {
                let view = cx.new(|cx| PicLensApp::new(window, cx, initial_folder.clone()));
                cx.new(|cx| Root::new(view, window, cx))
            })
            .expect("Failed to open PicLens window");
        })
        .detach();
    });
}
