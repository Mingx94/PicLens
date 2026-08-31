#![cfg_attr(all(windows, not(debug_assertions)), windows_subsystem = "windows")]

mod actions;
mod app;
mod assets;
mod cli;
mod diagnostics;
mod drag_rename;
mod folder_tree;
mod history;
mod interaction;
mod scan_apply;
mod screenshot;
mod theme;
mod thumbs;

use std::sync::Arc;
use std::time::Duration;

use app::{LaunchOptions, PicLensApp};
use cli::{LaunchArgs, ParseOutcome};
use gpui::*;
use gpui_component::Root;
use piclens_domain::{normalize_window_size, MIN_WINDOW_HEIGHT, MIN_WINDOW_WIDTH};
use piclens_infra::{ensure_thumbnail, info, init_file_logger, JsonSettingsStore};

fn main() {
    let raw_args = std::env::args().collect::<Vec<_>>();
    if raw_args.get(1).map(String::as_str) == Some("--thumbnail-worker") {
        let result = raw_args
            .get(2)
            .ok_or_else(|| "thumbnail worker source path is missing".to_string())
            .and_then(|source| {
                raw_args
                    .get(3)
                    .ok_or_else(|| "thumbnail worker size is missing".to_string())
                    .and_then(|size| {
                        size.parse::<u32>()
                            .map_err(|_| format!("invalid thumbnail worker size: {size}"))
                    })
                    .and_then(|size| ensure_thumbnail(source, size).map(|_| ()))
            });
        if let Err(err) = result {
            eprintln!("{err}");
            std::process::exit(1);
        }
        return;
    }
    let launch = match cli::parse(&raw_args) {
        Ok(ParseOutcome::Run(launch)) => launch,
        Ok(ParseOutcome::Print(text)) => {
            print!("{text}");
            return;
        }
        Err(err) => {
            eprintln!("error: {err}\nTry '--help' for usage.");
            std::process::exit(2);
        }
    };
    if let Some(data_root) = &launch.data_root {
        std::env::set_var("PICLENS_DATA_ROOT", data_root);
    }
    env_logger::Builder::from_env(env_logger::Env::default().default_filter_or("info")).init();
    init_file_logger();
    info(format!(
        "PicLens GPUI starting; build={}; executable={}",
        if cfg!(debug_assertions) {
            "debug"
        } else {
            "release"
        },
        std::env::current_exe()
            .map(|path| path.display().to_string())
            .unwrap_or_default()
    ));

    let LaunchArgs {
        folder: initial_folder,
        smoke_ms,
        include_subfolders,
        search,
        list_view,
        sidebar_closed,
        metrics,
        performance_scroll,
        performance_viewer,
        viewer,
        screenshot,
        ..
    } = launch;
    let runtime_metrics = metrics.map(|path| Arc::new(diagnostics::RuntimeMetrics::new(path)));
    let launch_options = LaunchOptions {
        include_subfolders,
        search,
        list_view,
        sidebar_closed,
        viewer,
        performance_scroll,
        performance_viewer,
        metrics: runtime_metrics.clone(),
    };
    let screenshot_path = screenshot.map(std::path::PathBuf::from);

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

        let stored = JsonSettingsStore::new().load();
        let mut window_size = match (stored.window_width, stored.window_height) {
            (Some(w), Some(h)) => {
                let (w, h) = normalize_window_size(w, h);
                size(px(w as f32), px(h as f32))
            }
            _ => size(px(1280.), px(800.)),
        };
        if let Some(display) = cx.primary_display() {
            let display_size = display.bounds().size;
            window_size.width = window_size.width.min(display_size.width * 0.85);
            window_size.height = window_size.height.min(display_size.height * 0.85);
        }
        let window_bounds = Bounds::centered(None, window_size, cx);

        if let Some(metrics) = runtime_metrics.clone() {
            let sample_count = smoke_ms.unwrap_or(60_000).div_ceil(100).max(1);
            let executor = cx.background_executor().clone();
            executor
                .clone()
                .spawn(async move {
                    for _ in 0..sample_count {
                        metrics.sample_process();
                        executor.timer(Duration::from_millis(100)).await;
                    }
                })
                .detach();
        }

        cx.spawn(async move |cx| {
            // Use the system title bar so Windows shows minimize / maximize / close.
            // TitleBar::window_options() is for client-drawn bars and hides those buttons.
            let options = WindowOptions {
                window_bounds: Some(WindowBounds::Windowed(window_bounds)),
                window_min_size: Some(size(
                    px(MIN_WINDOW_WIDTH as f32),
                    px(MIN_WINDOW_HEIGHT as f32),
                )),
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
                    let view = cx.new(|cx| {
                        PicLensApp::new(window, cx, initial_folder.clone(), launch_options.clone())
                    });
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
                        cx.spawn(async move |cx| {
                            cx.background_executor()
                                .timer(Duration::from_millis(100))
                                .await;
                            cx.update(|cx| cx.quit());
                        })
                        .detach();
                    }
                })
                .detach();
            });
            if let Some(ms) = smoke_ms {
                let runtime_metrics = runtime_metrics.clone();
                let smoke_window = window;
                info(format!("smoke mode: close after {ms}ms"));
                cx.update(|cx| {
                    cx.spawn(async move |cx| {
                        cx.background_executor()
                            .timer(Duration::from_millis(ms))
                            .await;
                        info("smoke timer elapsed; closing main window");
                        if let Some(metrics) = runtime_metrics {
                            if let Err(err) = metrics.write_snapshot() {
                                log::error!("failed to write metrics: {err}");
                            }
                        }
                        let _ = smoke_window.update(cx, |_root, window, cx| {
                            window.dispatch_action(Box::new(actions::PrepareShutdown), cx);
                        });
                        cx.background_executor()
                            .timer(Duration::from_millis(100))
                            .await;
                        let _ = smoke_window.update(cx, |_root, window, _cx| {
                            window.remove_window();
                        });
                    })
                    .detach();
                });
            }
            if let Some(path) = screenshot_path {
                cx.background_executor()
                    .timer(Duration::from_millis(750))
                    .await;
                let area = window
                    .update(cx, |_root, window, _cx| {
                        window.activate_window();
                        let bounds = window.bounds();
                        let scale = f64::from(window.scale_factor());
                        (
                            f64::from(bounds.origin.x) * scale,
                            f64::from(bounds.origin.y) * scale,
                            f64::from(bounds.size.width) * scale,
                            f64::from(bounds.size.height) * scale,
                        )
                    })
                    .expect("PicLens window is available for screenshot setup");
                match cx
                    .background_executor()
                    .spawn(async move { screenshot::capture(&path, area).map(|_| path) })
                    .await
                {
                    Ok(path) => info(format!("screenshot saved: {}", path.display())),
                    Err(err) => log::error!("screenshot failed: {err}"),
                }
            }
        })
        .detach();
    });
}
