//! Framework-light interface state for the egui frontend.

use std::path::PathBuf;

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum Action {
    ChooseFolder,
    RetryBackendProbe,
    DismissStatus,
}

#[derive(Debug, Clone, Default, PartialEq, Eq)]
pub enum Loadable<T> {
    #[default]
    Idle,
    Loading,
    Ready(T),
    Failed(String),
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum BackendStatus {
    Starting,
    Ready,
    Failed(String),
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct AppModel {
    pub initial_folder: Option<PathBuf>,
    pub backend: BackendStatus,
    pub notice: Option<String>,
}

impl AppModel {
    pub fn new(initial_folder: Option<PathBuf>) -> Self {
        Self {
            initial_folder,
            backend: BackendStatus::Starting,
            notice: None,
        }
    }

    pub fn demo_error(message: impl Into<String>) -> Self {
        Self {
            initial_folder: None,
            backend: BackendStatus::Failed(message.into()),
            notice: None,
        }
    }
}
