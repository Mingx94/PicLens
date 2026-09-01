//! Image-loader identities shared by the gallery and viewer pipelines.

use std::path::PathBuf;

#[derive(Debug, Clone, PartialEq, Eq, Hash)]
pub struct ThumbnailKey {
    pub source: PathBuf,
    pub size: u32,
    pub generation: u64,
}

#[derive(Debug, Clone, PartialEq, Eq, Hash)]
pub struct PreviewKey {
    pub source: PathBuf,
    pub modified_unix_ms: u128,
    pub file_size: u64,
    pub longest_edge: u32,
}
