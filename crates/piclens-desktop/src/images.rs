//! Thumbnail identities, decoded pixels, and egui texture lifetime.

use std::collections::{HashMap, HashSet};
use std::path::{Path, PathBuf};
use std::time::UNIX_EPOCH;

use piclens_domain::ImageListItem;

#[derive(Debug, Clone, PartialEq, Eq, Hash)]
pub struct ThumbnailKey {
    pub source: PathBuf,
    pub modified_unix_ms: Option<i64>,
    pub file_size: u64,
    pub longest_edge: u32,
}

impl ThumbnailKey {
    pub fn from_image(image: &ImageListItem, longest_edge: u32) -> Self {
        Self {
            source: image.path.clone().into(),
            modified_unix_ms: image.modified_at_ms,
            file_size: image.size_bytes,
            longest_edge: longest_edge.max(16),
        }
    }

    pub fn source_matches_disk(&self) -> bool {
        let Ok(metadata) = std::fs::metadata(&self.source) else {
            return false;
        };
        if metadata.len() != self.file_size {
            return false;
        }
        let Some(expected_modified) = self.modified_unix_ms else {
            return true;
        };
        metadata
            .modified()
            .ok()
            .and_then(|modified| modified.duration_since(UNIX_EPOCH).ok())
            .map(|duration| duration.as_millis() as i64)
            == Some(expected_modified)
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub struct ThumbnailRequestIdentity {
    pub generation: u64,
    pub request_id: u64,
}

#[derive(Debug, Clone, PartialEq, Eq, Hash)]
pub struct ThumbnailRequest {
    pub identity: ThumbnailRequestIdentity,
    pub key: ThumbnailKey,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct DecodedThumbnail {
    pub width: u32,
    pub height: u32,
    pub rgba: Vec<u8>,
}

impl DecodedThumbnail {
    fn color_image(&self) -> Result<egui::ColorImage, String> {
        let expected_len = self.width as usize * self.height as usize * 4;
        if self.width == 0 || self.height == 0 || self.rgba.len() != expected_len {
            return Err("thumbnail decoder returned invalid RGBA dimensions".into());
        }
        Ok(egui::ColorImage::from_rgba_unmultiplied(
            [self.width as usize, self.height as usize],
            &self.rgba,
        ))
    }
}

enum ThumbnailEntry {
    Pending(ThumbnailRequest),
    Ready(egui::TextureHandle),
    Failed { generation: u64, message: String },
}

pub struct ThumbnailLoader {
    entries: HashMap<ThumbnailKey, ThumbnailEntry>,
    last_synced_requests: Vec<ThumbnailRequest>,
    next_request_id: u64,
}

impl Default for ThumbnailLoader {
    fn default() -> Self {
        Self {
            entries: HashMap::new(),
            last_synced_requests: Vec::new(),
            next_request_id: 1,
        }
    }
}

impl ThumbnailLoader {
    pub fn texture(&self, key: &ThumbnailKey) -> Option<&egui::TextureHandle> {
        match self.entries.get(key) {
            Some(ThumbnailEntry::Ready(texture)) => Some(texture),
            _ => None,
        }
    }

    pub fn failure(&self, key: &ThumbnailKey) -> Option<&str> {
        match self.entries.get(key) {
            Some(ThumbnailEntry::Failed { message, .. }) => Some(message),
            _ => None,
        }
    }

    pub fn sync_materialized(
        &mut self,
        keys: Vec<ThumbnailKey>,
        generation: u64,
    ) -> Option<Vec<ThumbnailRequest>> {
        let mut seen = HashSet::with_capacity(keys.len());
        let keys = keys
            .into_iter()
            .filter(|key| seen.insert(key.clone()))
            .collect::<Vec<_>>();
        self.entries.retain(|key, _| seen.contains(key));

        for key in &keys {
            let must_start = match self.entries.get(key) {
                None => true,
                Some(ThumbnailEntry::Pending(request)) => request.identity.generation != generation,
                Some(ThumbnailEntry::Failed {
                    generation: failed_generation,
                    ..
                }) => *failed_generation != generation,
                Some(ThumbnailEntry::Ready(_)) => false,
            };
            if must_start {
                let request = ThumbnailRequest {
                    identity: ThumbnailRequestIdentity {
                        generation,
                        request_id: self.next_request_id,
                    },
                    key: key.clone(),
                };
                self.next_request_id = self.next_request_id.wrapping_add(1).max(1);
                self.entries
                    .insert(key.clone(), ThumbnailEntry::Pending(request));
            }
        }

        let requests = keys
            .iter()
            .filter_map(|key| match self.entries.get(key) {
                Some(ThumbnailEntry::Pending(request)) => Some(request.clone()),
                _ => None,
            })
            .collect::<Vec<_>>();
        if requests == self.last_synced_requests {
            return None;
        }
        self.last_synced_requests.clone_from(&requests);
        Some(requests)
    }

    pub fn handle_result(
        &mut self,
        request: &ThumbnailRequest,
        result: Result<DecodedThumbnail, String>,
        ctx: &egui::Context,
    ) -> bool {
        let Some(ThumbnailEntry::Pending(pending)) = self.entries.get(&request.key) else {
            return false;
        };
        if pending.identity != request.identity {
            return false;
        }

        let entry = match result.and_then(|thumbnail| thumbnail.color_image()) {
            Ok(image) => ThumbnailEntry::Ready(ctx.load_texture(
                texture_name(&request.key),
                image,
                egui::TextureOptions::LINEAR,
            )),
            Err(message) => ThumbnailEntry::Failed {
                generation: request.identity.generation,
                message,
            },
        };
        self.entries.insert(request.key.clone(), entry);
        true
    }

    pub fn fail_requests(&mut self, requests: &[ThumbnailRequest], message: &str) {
        for request in requests {
            let Some(ThumbnailEntry::Pending(pending)) = self.entries.get(&request.key) else {
                continue;
            };
            if pending.identity == request.identity {
                self.entries.insert(
                    request.key.clone(),
                    ThumbnailEntry::Failed {
                        generation: request.identity.generation,
                        message: message.into(),
                    },
                );
            }
        }
    }
}

fn texture_name(key: &ThumbnailKey) -> String {
    format!(
        "thumbnail:{}:{:?}:{}:{}",
        key.source.display(),
        key.modified_unix_ms,
        key.file_size,
        key.longest_edge
    )
}

pub fn decode_cached_thumbnail(path: &Path) -> Result<DecodedThumbnail, String> {
    let image = image::open(path)
        .map_err(|error| error.to_string())?
        .into_rgba8();
    let (width, height) = image.dimensions();
    Ok(DecodedThumbnail {
        width,
        height,
        rgba: image.into_raw(),
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    fn image() -> ImageListItem {
        ImageListItem {
            path: "C:/gallery/image.png".into(),
            name: "image.png".into(),
            extension: "png".into(),
            modified_at_ms: Some(42),
            size_bytes: 99,
            is_animated: false,
        }
    }

    #[test]
    fn thumbnail_key_contains_source_identity_and_size() {
        let base = ThumbnailKey::from_image(&image(), 160);
        let mut changed = image();
        changed.modified_at_ms = Some(43);
        assert_ne!(base, ThumbnailKey::from_image(&changed, 160));
        changed = image();
        changed.size_bytes = 100;
        assert_ne!(base, ThumbnailKey::from_image(&changed, 160));
        assert_ne!(base, ThumbnailKey::from_image(&image(), 240));
        assert_eq!(ThumbnailKey::from_image(&image(), 1).longest_edge, 16);
    }

    #[test]
    fn materialized_sync_replaces_pending_generation_and_ignores_stale_result() {
        let key = ThumbnailKey::from_image(&image(), 160);
        let mut loader = ThumbnailLoader::default();
        let first = loader
            .sync_materialized(vec![key.clone()], 1)
            .unwrap()
            .remove(0);
        let second = loader
            .sync_materialized(vec![key.clone()], 2)
            .unwrap()
            .remove(0);
        assert_ne!(first.identity, second.identity);
        assert!(!loader.handle_result(&first, Err("stale".into()), &egui::Context::default()));
        assert!(loader.handle_result(&second, Err("current".into()), &egui::Context::default()));
        assert_eq!(loader.failure(&key), Some("current"));
    }

    #[test]
    fn unloaded_entries_cancel_pending_snapshot() {
        let key = ThumbnailKey::from_image(&image(), 160);
        let mut loader = ThumbnailLoader::default();
        assert_eq!(loader.sync_materialized(vec![key], 1).unwrap().len(), 1);
        assert!(loader.sync_materialized(Vec::new(), 1).unwrap().is_empty());
        assert!(loader.sync_materialized(Vec::new(), 1).is_none());
    }

    #[test]
    fn viewer_materialization_pauses_gallery_and_gallery_resumes_after_close() {
        let gallery = ThumbnailKey::from_image(&image(), 160);
        let viewer = ThumbnailKey::from_image(&image(), 1024);
        let mut loader = ThumbnailLoader::default();

        assert_eq!(
            loader.sync_materialized(vec![gallery.clone()], 1),
            Some(vec![ThumbnailRequest {
                identity: ThumbnailRequestIdentity {
                    generation: 1,
                    request_id: 1,
                },
                key: gallery.clone(),
            }])
        );
        assert_eq!(
            loader.sync_materialized(vec![viewer.clone()], 1),
            Some(vec![ThumbnailRequest {
                identity: ThumbnailRequestIdentity {
                    generation: 1,
                    request_id: 2,
                },
                key: viewer,
            }])
        );
        assert_eq!(
            loader.sync_materialized(vec![gallery.clone()], 1),
            Some(vec![ThumbnailRequest {
                identity: ThumbnailRequestIdentity {
                    generation: 1,
                    request_id: 3,
                },
                key: gallery,
            }])
        );
    }

    #[test]
    fn source_identity_detects_file_size_changes() {
        let root = std::env::temp_dir().join(format!(
            "piclens-egui-thumbnail-identity-{}",
            std::process::id()
        ));
        let _ = std::fs::remove_dir_all(&root);
        std::fs::create_dir_all(&root).unwrap();
        let source = root.join("image.png");
        std::fs::write(&source, b"first").unwrap();
        let metadata = std::fs::metadata(&source).unwrap();
        let modified_unix_ms = metadata
            .modified()
            .ok()
            .and_then(|modified| modified.duration_since(UNIX_EPOCH).ok())
            .map(|duration| duration.as_millis() as i64);
        let key = ThumbnailKey {
            source: source.clone(),
            modified_unix_ms,
            file_size: metadata.len(),
            longest_edge: 160,
        };

        assert!(key.source_matches_disk());
        std::fs::write(&source, b"changed size").unwrap();
        assert!(!key.source_matches_disk());

        std::fs::remove_dir_all(root).unwrap();
    }
}
