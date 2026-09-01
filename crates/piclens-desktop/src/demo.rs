//! Deterministic interface states for headless render and screenshot tests.

use crate::model::{AppModel, BackendStatus};

pub fn empty_library() -> AppModel {
    let mut model = AppModel::new(None);
    model.backend = BackendStatus::Ready;
    model
}

pub fn startup_error(message: impl Into<String>) -> AppModel {
    AppModel::demo_error(message)
}
