#![cfg_attr(all(windows, not(debug_assertions)), windows_subsystem = "windows")]

use std::path::PathBuf;
use std::time::Duration;

use piclens_desktop::cli::{self, ParseOutcome};
use piclens_desktop::LaunchOptions;
use piclens_infra::{info, init_file_logger};

fn main() -> eframe::Result<()> {
    let raw_args = std::env::args().collect::<Vec<_>>();
    let launch = match cli::parse(&raw_args) {
        Ok(ParseOutcome::Run(launch)) => launch,
        Ok(ParseOutcome::Print(text)) => {
            print!("{text}");
            return Ok(());
        }
        Err(error) => {
            eprintln!("error: {error}\nTry '--help' for usage.");
            std::process::exit(2);
        }
    };

    if let Some(data_root) = &launch.data_root {
        std::env::set_var("PICLENS_DATA_ROOT", data_root);
    }
    let _ = env_logger::Builder::from_env(env_logger::Env::default().default_filter_or("info"))
        .try_init();
    init_file_logger();
    info(format!(
        "PicLens egui starting; build={}; executable={}",
        if cfg!(debug_assertions) {
            "debug"
        } else {
            "release"
        },
        std::env::current_exe()
            .map(|path| path.display().to_string())
            .unwrap_or_default()
    ));

    piclens_desktop::run(LaunchOptions {
        initial_folder: launch.folder.map(PathBuf::from),
        smoke_after: launch.smoke_ms.map(Duration::from_millis),
        ..Default::default()
    })
}
