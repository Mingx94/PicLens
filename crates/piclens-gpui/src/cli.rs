//! Strict command-line parsing kept dependency-free for fast startup.

#[derive(Debug, Clone, Default, PartialEq, Eq)]
pub struct LaunchArgs {
    pub folder: Option<String>,
    pub smoke_ms: Option<u64>,
    pub data_root: Option<String>,
    pub screenshot: Option<String>,
    pub viewer: Option<String>,
    pub metrics: Option<String>,
    pub performance_scroll: bool,
    pub include_subfolders: bool,
    pub search: Option<String>,
    pub list_view: bool,
    pub sidebar_closed: bool,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum ParseOutcome {
    Run(LaunchArgs),
    Print(String),
}

const HELP: &str = "PicLens - desktop image viewer and organizer\n\n\
Usage: piclens-gpui [OPTIONS]\n\n\
Options:\n\
  -f, --folder <PATH>          Open a folder without changing saved startup state\n\
      --data-root <PATH>       Use an isolated PicLens profile\n\
      --viewer <PATH>          Open this image in the viewer after loading\n\
      --search <TEXT>          Apply a temporary search filter\n\
      --include-subfolders     Include child folders for this run\n\
      --list-view              Start in list view for this run\n\
      --sidebar-closed         Start with the sidebar closed for this run\n\
      --screenshot <PATH>      Save an automated window screenshot\n\
      --metrics <PATH>         Write release-run metrics as JSON\n\
      --performance-scroll     Run the deterministic scroll workload\n\
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
                "--smoke-ms" => {
                    launch.smoke_ms = Some(
                        value
                            .parse()
                            .map_err(|_| format!("invalid value for --smoke-ms: {value}"))?,
                    )
                }
                "--data-root" => launch.data_root = Some(value.into()),
                "--screenshot" => launch.screenshot = Some(value.into()),
                "--viewer" => launch.viewer = Some(value.into()),
                "--metrics" => launch.metrics = Some(value.into()),
                "--search" => launch.search = Some(value.into()),
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
                )))
            }
            "-f" | "--folder" => launch.folder = Some(take_value(args, &mut index, arg)?),
            "--smoke-ms" => {
                let value = take_value(args, &mut index, arg)?;
                launch.smoke_ms = Some(
                    value
                        .parse()
                        .map_err(|_| format!("invalid value for --smoke-ms: {value}"))?,
                );
            }
            "--data-root" => launch.data_root = Some(take_value(args, &mut index, arg)?),
            "--screenshot" => launch.screenshot = Some(take_value(args, &mut index, arg)?),
            "--viewer" => launch.viewer = Some(take_value(args, &mut index, arg)?),
            "--metrics" => launch.metrics = Some(take_value(args, &mut index, arg)?),
            "--search" => launch.search = Some(take_value(args, &mut index, arg)?),
            "--performance-scroll" => launch.performance_scroll = true,
            "--include-subfolders" => launch.include_subfolders = true,
            "--list-view" => launch.list_view = true,
            "--sidebar-closed" => launch.sidebar_closed = true,
            _ => return Err(format!("unknown option: {arg}")),
        }
        index += 1;
    }
    Ok(ParseOutcome::Run(launch))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parses_supported_overrides() {
        let args = vec![
            "piclens".into(),
            "-f".into(),
            "photos".into(),
            "--data-root=profile".into(),
            "--include-subfolders".into(),
            "--search".into(),
            "cat".into(),
            "--list-view".into(),
            "--sidebar-closed".into(),
        ];
        let ParseOutcome::Run(parsed) = parse(&args).unwrap() else {
            panic!()
        };
        assert_eq!(parsed.folder.as_deref(), Some("photos"));
        assert_eq!(parsed.data_root.as_deref(), Some("profile"));
        assert_eq!(parsed.search.as_deref(), Some("cat"));
        assert!(parsed.include_subfolders && parsed.list_view && parsed.sidebar_closed);
    }

    #[test]
    fn rejects_unknown_missing_and_invalid_values() {
        assert!(parse(&["piclens".into(), "--wat".into()]).is_err());
        assert!(parse(&["piclens".into(), "--folder".into()]).is_err());
        assert!(parse(&["piclens".into(), "--smoke-ms=x".into()]).is_err());
    }
}
