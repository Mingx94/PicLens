//! Strict command-line parsing kept dependency-free for fast startup.

#[derive(Debug, Clone, Default, PartialEq, Eq)]
pub struct LaunchArgs {
    pub folder: Option<String>,
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
    Ok(ParseOutcome::Run(launch))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parses_supported_launch_options() {
        let args = vec![
            "piclens-desktop".into(),
            "--folder=photos".into(),
            "--data-root".into(),
            "profile".into(),
            "--smoke-ms".into(),
            "250".into(),
        ];
        let ParseOutcome::Run(parsed) = parse(&args).unwrap() else {
            panic!("expected launch arguments")
        };
        assert_eq!(parsed.folder.as_deref(), Some("photos"));
        assert_eq!(parsed.data_root.as_deref(), Some("profile"));
        assert_eq!(parsed.smoke_ms, Some(250));
    }

    #[test]
    fn rejects_unknown_missing_and_invalid_values() {
        assert!(parse(&["piclens-desktop".into(), "--unknown".into()]).is_err());
        assert!(parse(&["piclens-desktop".into(), "--folder".into()]).is_err());
        assert!(parse(&["piclens-desktop".into(), "--smoke-ms=x".into()]).is_err());
    }
}
