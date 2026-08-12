mod actions;
mod app;
mod history;

use std::time::Duration;

use app::PicLensApp;
use gpui::*;
use gpui_component::{Root, TitleBar};
use piclens_infra::{info, init_file_logger};

struct LaunchArgs {
    folder: Option<String>,
    /// When set, quit the app after this many milliseconds (CI / smoke).
    smoke_ms: Option<u64>,
}

fn parse_args(args: &[String]) -> LaunchArgs {
    let mut folder = None;
    let mut smoke_ms = None;
    let mut iter = args.iter().skip(1);
    while let Some(arg) = iter.next() {
        if arg == "--folder" {
            folder = iter.next().cloned();
        } else if let Some(path) = arg.strip_prefix("--folder=") {
            folder = Some(path.to_string());
        } else if arg == "--smoke-ms" {
            smoke_ms = iter.next().and_then(|v| v.parse().ok());
        } else if let Some(v) = arg.strip_prefix("--smoke-ms=") {
            smoke_ms = v.parse().ok();
        }
    }
    LaunchArgs { folder, smoke_ms }
}

fn main() {
    env_logger::Builder::from_env(env_logger::Env::default().default_filter_or("info")).init();
    init_file_logger();
    info("PicLens GPUI starting");

    let launch = parse_args(&std::env::args().collect::<Vec<_>>());
    let initial_folder = launch.folder;
    let smoke_ms = launch.smoke_ms;

    let app = gpui_platform::application().with_assets(gpui_component_assets::Assets);
    app.run(move |cx| {
        gpui_component::init(cx);
        actions::init(cx);
        cx.activate(true);

        let mut window_size = size(px(1280.), px(800.));
        if let Some(display) = cx.primary_display() {
            let display_size = display.bounds().size;
            window_size.width = window_size.width.min(display_size.width * 0.85);
            window_size.height = window_size.height.min(display_size.height * 0.85);
        }
        let window_bounds = Bounds::centered(None, window_size, cx);

        if let Some(ms) = smoke_ms {
            info(format!("smoke mode: quit after {ms}ms"));
            cx.spawn(async move |cx| {
                cx.background_executor()
                    .timer(Duration::from_millis(ms))
                    .await;
                info("smoke timer elapsed; quitting");
                cx.update(|cx| {
                    cx.quit();
                });
            })
            .detach();
        }

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
