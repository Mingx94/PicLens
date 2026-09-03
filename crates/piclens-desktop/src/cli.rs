//! Strict command-line parsing kept dependency-free for fast startup.

#[derive(Debug, Clone, Default, PartialEq, Eq)]
pub struct LaunchArgs {
    pub folder: Option<String>,
    pub search: Option<String>,
    pub include_subfolders: bool,
    pub sidebar_closed: bool,
    pub viewer: Option<String>,
    pub screenshot: Option<String>,
    pub metrics: Option<String>,
    pub performance_scroll: bool,
    pub performance_viewer: bool,
    pub performance_batch_jpg: bool,
    pub smoke_ms: Option<u64>,
    pub data_root: Option<String>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum ParseOutcome {
    Run(LaunchArgs),
    Print(String),
}

const HELP: &str = "PicLens - desktop image viewer and organizer\n\n\
Usage: piclens-desktop [OPTIONS]\n\n\
Options:\n\
  -f, --folder <PATH>          Open a folder without changing saved startup state\n\
      --search <TEXT>          Apply a temporary search filter\n\
      --include-subfolders     Include child folders for this run\n\
      --sidebar-closed         Start with the sidebar closed for this run\n\
      --viewer <PATH>          Open this image in the viewer after loading\n\
      --screenshot <PATH>      Save an automated window screenshot\n\
      --metrics <PATH>         Write release-run metrics as JSON\n\
      --performance-scroll     Run the continuous gallery scroll workload\n\
      --performance-viewer     Run forward/backward navigation (requires --viewer)\n\
      --performance-batch-jpg  Convert a disposable PNG batch under --data-root\n\
      --data-root <PATH>       Use an isolated PicLens profile\n\
      --smoke-ms <MILLISECONDS> Quit automatically after the given time\n\
  -h, --help                   Print help\n\
  -V, --version                Print version\n";

fn split_value(arg: &str) -> Option<(&str, &str)> {
    arg.split_once('=').filter(|(_, value)| !value.is_empty())
}

fn take_value(args: &[String], index: &mut usize, name: &str) -> Result<String, String> {
    *index += 1;
    args.get(*index)
        .filter(|value| !value.starts_with('-'))
        .cloned()
        .ok_or_else(|| format!("missing value for {name}"))
}

pub fn parse(args: &[String]) -> Result<ParseOutcome, String> {
    let mut launch = LaunchArgs::default();
    let mut index = 1;
    while index < args.len() {
        let arg = &args[index];
        if let Some((name, value)) = split_value(arg) {
            match name {
                "--folder" => launch.folder = Some(value.into()),
                "--search" => launch.search = Some(value.into()),
                "--viewer" => launch.viewer = Some(value.into()),
                "--screenshot" => launch.screenshot = Some(value.into()),
                "--metrics" => launch.metrics = Some(value.into()),
                "--data-root" => launch.data_root = Some(value.into()),
                "--smoke-ms" => {
                    launch.smoke_ms = Some(
                        value
                            .parse()
                            .map_err(|_| format!("invalid value for --smoke-ms: {value}"))?,
                    );
                }
                _ => return Err(format!("unknown option: {name}")),
            }
            index += 1;
            continue;
        }

        match arg.as_str() {
            "-h" | "--help" => return Ok(ParseOutcome::Print(HELP.into())),
            "-V" | "--version" => {
                return Ok(ParseOutcome::Print(format!(
                    "PicLens {}\n",
                    env!("CARGO_PKG_VERSION")
                )));
            }
            "-f" | "--folder" => launch.folder = Some(take_value(args, &mut index, arg)?),
            "--search" => launch.search = Some(take_value(args, &mut index, arg)?),
            "--include-subfolders" => launch.include_subfolders = true,
            "--sidebar-closed" => launch.sidebar_closed = true,
            "--viewer" => launch.viewer = Some(take_value(args, &mut index, arg)?),
            "--screenshot" => launch.screenshot = Some(take_value(args, &mut index, arg)?),
            "--metrics" => launch.metrics = Some(take_value(args, &mut index, arg)?),
            "--performance-scroll" => launch.performance_scroll = true,
            "--performance-viewer" => launch.performance_viewer = true,
            "--performance-batch-jpg" => launch.performance_batch_jpg = true,
            "--data-root" => launch.data_root = Some(take_value(args, &mut index, arg)?),
            "--smoke-ms" => {
                let value = take_value(args, &mut index, arg)?;
                launch.smoke_ms = Some(
                    value
                        .parse()
                        .map_err(|_| format!("invalid value for --smoke-ms: {value}"))?,
                );
            }
            _ => return Err(format!("unknown option: {arg}")),
        }
        index += 1;
    }
    if launch.performance_viewer && launch.viewer.is_none() {
        return Err("--performance-viewer requires --viewer".into());
    }
    if launch.performance_batch_jpg && (launch.folder.is_none() || launch.data_root.is_none()) {
        return Err("--performance-batch-jpg requires --folder and --data-root".into());
    }
    Ok(ParseOutcome::Run(launch))
}

pub fn validate_performance_batch_fixture(launch: &LaunchArgs) -> Result<(), String> {
    if !launch.performance_batch_jpg {
        return Ok(());
    }
    let folder = launch.folder.as_deref().expect("validated by parse");
    let data_root = launch.data_root.as_deref().expect("validated by parse");
    std::fs::create_dir_all(data_root)
        .map_err(|error| format!("cannot create --data-root: {error}"))?;
    let folder = std::fs::canonicalize(folder)
        .map_err(|error| format!("cannot resolve batch fixture folder: {error}"))?;
    let data_root = std::fs::canonicalize(data_root)
        .map_err(|error| format!("cannot resolve --data-root: {error}"))?;
    if folder == data_root || !folder.starts_with(&data_root) {
        return Err(
            "--performance-batch-jpg requires --folder to be a child of --data-root".into(),
        );
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parses_supported_launch_options() {
        let args = vec![
            "piclens-desktop".into(),
            "--folder=photos".into(),
            "--search".into(),
            "jpg".into(),
            "--include-subfolders".into(),
            "--sidebar-closed".into(),
            "--data-root".into(),
            "profile".into(),
            "--viewer=photos/a.jpg".into(),
            "--screenshot".into(),
            "window.png".into(),
            "--metrics".into(),
            "metrics.json".into(),
            "--performance-scroll".into(),
            "--performance-viewer".into(),
            "--performance-batch-jpg".into(),
            "--smoke-ms".into(),
            "250".into(),
        ];
        let ParseOutcome::Run(parsed) = parse(&args).unwrap() else {
            panic!("expected launch arguments")
        };
        assert_eq!(parsed.folder.as_deref(), Some("photos"));
        assert_eq!(parsed.search.as_deref(), Some("jpg"));
        assert!(parsed.include_subfolders);
        assert!(parsed.sidebar_closed);
        assert_eq!(parsed.data_root.as_deref(), Some("profile"));
        assert_eq!(parsed.viewer.as_deref(), Some("photos/a.jpg"));
        assert_eq!(parsed.screenshot.as_deref(), Some("window.png"));
        assert_eq!(parsed.metrics.as_deref(), Some("metrics.json"));
        assert!(parsed.performance_scroll);
        assert!(parsed.performance_viewer);
        assert!(parsed.performance_batch_jpg);
        assert_eq!(parsed.smoke_ms, Some(250));
    }

    #[test]
    fn rejects_unknown_missing_and_invalid_values() {
        assert!(parse(&["piclens-desktop".into(), "--unknown".into()]).is_err());
        assert!(parse(&["piclens-desktop".into(), "--folder".into()]).is_err());
        assert!(parse(&["piclens-desktop".into(), "--smoke-ms=x".into()]).is_err());
        assert!(parse(&["piclens-desktop".into(), "--performance-viewer".into()]).is_err());
        assert!(parse(&["piclens-desktop".into(), "--performance-batch-jpg".into()]).is_err());
    }

    #[test]
    fn performance_batch_fixture_must_be_below_the_data_root() {
        let unique = std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .unwrap()
            .as_nanos();
        let test_root = std::env::temp_dir().join(format!(
            "piclens-batch-path-{}-{unique}",
            std::process::id()
        ));
        let data_root = test_root.join("profile");
        let fixture = data_root.join("fixture");
        let outside = test_root.join("outside");
        std::fs::create_dir_all(&fixture).unwrap();
        std::fs::create_dir_all(&outside).unwrap();
        let mut launch = LaunchArgs {
            folder: Some(fixture.to_string_lossy().into_owned()),
            data_root: Some(data_root.to_string_lossy().into_owned()),
            performance_batch_jpg: true,
            ..Default::default()
        };

        assert!(validate_performance_batch_fixture(&launch).is_ok());
        launch.folder = Some(outside.to_string_lossy().into_owned());
        assert!(validate_performance_batch_fixture(&launch).is_err());

        std::fs::remove_dir_all(test_root).unwrap();
    }
}
