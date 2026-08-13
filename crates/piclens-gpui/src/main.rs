mod actions;
mod app;
mod assets;
mod history;
mod scan_apply;
mod theme;
mod thumbs;

use std::time::Duration;

use app::PicLensApp;
use gpui::*;
use gpui_component::Root;
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
        theme::init(cx);
        if let Err(err) = assets::register_fonts(cx) {
            log::warn!("failed to register bundled fonts: {err}");
        }
        actions::init(cx);
        cx.on_action(|_: &actions::Quit, cx| cx.quit());
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
            // Use the system title bar so Windows shows minimize / maximize / close.
            // TitleBar::window_options() is for client-drawn bars and hides those buttons.
            let options = WindowOptions {
                window_bounds: Some(WindowBounds::Windowed(window_bounds)),
                window_min_size: Some(size(px(480.), px(320.))),
                kind: WindowKind::Normal,
                titlebar: Some(TitlebarOptions {
                    title: Some("PicLens".into()),
                    appears_transparent: false,
                    ..Default::default()
                }),
                ..Default::default()
            };

            let window = cx
                .open_window(options, move |window, cx| {
                    let view = cx.new(|cx| PicLensApp::new(window, cx, initial_folder.clone()));
                    cx.new(|cx| Root::new(view, window, cx))
                })
                .expect("Failed to open PicLens window");

            // When the user closes the last window, quit without leaving
            // background work that would log "window not found".
            let window_id = window.window_id();
            cx.update(|cx| {
                cx.on_window_closed(move |cx, closed_id| {
                    if closed_id == window_id {
                        info("main window closed; quitting");
                        cx.quit();
                    }
                })
                .detach();
            });
        })
        .detach();
    });
}
