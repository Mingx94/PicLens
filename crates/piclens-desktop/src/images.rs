//! Thumbnail identities, decoded pixels, and egui texture lifetime.

use std::collections::{HashMap, HashSet, VecDeque};
use std::path::PathBuf;
use std::time::UNIX_EPOCH;

use piclens_domain::ImageListItem;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub enum ImageResolution {
    Preview(u32),
    Original,
}

#[derive(Debug, Clone, PartialEq, Eq, Hash)]
pub struct ThumbnailKey {
    pub source: PathBuf,
    pub modified_unix_ms: Option<i64>,
    pub file_size: u64,
    pub resolution: ImageResolution,
}

impl ThumbnailKey {
    pub fn from_image(image: &ImageListItem, longest_edge: u32) -> Self {
        Self {
            source: image.path.clone().into(),
            modified_unix_ms: image.modified_at_ms,
            file_size: image.size_bytes,
            resolution: ImageResolution::Preview(longest_edge.max(16)),
        }
    }

    pub fn original(image: &ImageListItem) -> Self {
        Self {
            resolution: ImageResolution::Original,
            ..Self::from_image(image, 1024)
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
    fn color_image(&self, longest_edge: u32) -> Result<egui::ColorImage, String> {
        let expected_len = self.width as usize * self.height as usize * 4;
        if self.width == 0
            || self.height == 0
            || self.width > longest_edge
            || self.height > longest_edge
            || self.rgba.len() != expected_len
        {
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
    Original(OriginalTexture),
    Failed { generation: u64, message: String },
}

pub struct OriginalTexture {
    pub size: egui::Vec2,
    tiles: Vec<(egui::TextureHandle, egui::Rect, egui::Rect)>,
}

#[derive(Debug, Clone, PartialEq)]
pub enum PreparedThumbnail {
    Preview(egui::ColorImage),
    Original(PreparedOriginal),
}

#[derive(Debug, Clone, PartialEq)]
pub struct PreparedOriginal {
    size: egui::Vec2,
    tiles: Vec<(egui::ColorImage, egui::Rect, egui::Rect)>,
}

impl DecodedThumbnail {
    pub fn prepare(
        self,
        resolution: ImageResolution,
        side: usize,
        cancellation: &piclens_infra::CancellationToken,
    ) -> Result<PreparedThumbnail, String> {
        if cancellation.is_canceled() {
            return Err("thumbnail canceled".into());
        }
        match resolution {
            ImageResolution::Preview(edge) => {
                self.color_image(edge).map(PreparedThumbnail::Preview)
            }
            ImageResolution::Original => {
                PreparedOriginal::prepare(self, side, cancellation).map(PreparedThumbnail::Original)
            }
        }
    }
}

impl PreparedOriginal {
    fn prepare(
        decoded: DecodedThumbnail,
        side: usize,
        cancellation: &piclens_infra::CancellationToken,
    ) -> Result<Self, String> {
        let (width, height) = (decoded.width as usize, decoded.height as usize);
        let len = width.checked_mul(height).and_then(|n| n.checked_mul(4));
        if width == 0
            || height == 0
            || len != Some(decoded.rgba.len())
            || decoded.rgba.len() > piclens_infra::MAX_ORIGINAL_RGBA_BYTES
        {
            return Err("原圖解碼尺寸無效或超過像素上限。".into());
        }
        let side = side.max(3);
        let mut tiles = Vec::new();
        // One-pixel gutters let linear filtering sample across tile boundaries.
        for y in (0..height).step_by(side - 2) {
            for x in (0..width).step_by(side - 2) {
                let right = (x + side - 2).min(width);
                let bottom = (y + side - 2).min(height);
                let left = x.saturating_sub(1);
                let top = y.saturating_sub(1);
                let end_x = (right + 1).min(width);
                let end_y = (bottom + 1).min(height);
                let mut pixels = Vec::with_capacity((end_x - left) * (end_y - top));
                for row in top..end_y {
                    if cancellation.is_canceled() {
                        return Err("original canceled".into());
                    }
                    pixels.extend(
                        decoded.rgba[(row * width + left) * 4..(row * width + end_x) * 4]
                            .as_chunks::<4>()
                            .0
                            .iter()
                            .map(|p| egui::Color32::from_rgba_unmultiplied(p[0], p[1], p[2], p[3])),
                    );
                }
                let image = egui::ColorImage::new([end_x - left, end_y - top], pixels);
                let bounds = egui::Rect::from_min_max(
                    egui::pos2(x as f32, y as f32),
                    egui::pos2(right as f32, bottom as f32),
                );
                let uv = egui::Rect::from_min_max(
                    egui::pos2(
                        (x - left) as f32 / (end_x - left) as f32,
                        (y - top) as f32 / (end_y - top) as f32,
                    ),
                    egui::pos2(
                        (right - left) as f32 / (end_x - left) as f32,
                        (bottom - top) as f32 / (end_y - top) as f32,
                    ),
                );
                tiles.push((image, bounds, uv));
            }
        }
        Ok(Self {
            size: egui::vec2(width as f32, height as f32),
            tiles,
        })
    }
}

impl OriginalTexture {
    fn upload(prepared: PreparedOriginal, ctx: &egui::Context, name: &str) -> Self {
        Self {
            size: prepared.size,
            tiles: prepared
                .tiles
                .into_iter()
                .enumerate()
                .map(|(index, (image, bounds, uv))| {
                    (
                        ctx.load_texture(
                            format!("{name}:{index}"),
                            image,
                            egui::TextureOptions::LINEAR,
                        ),
                        bounds,
                        uv,
                    )
                })
                .collect(),
        }
    }

    pub fn paint(&self, painter: &egui::Painter, rect: egui::Rect) {
        let scale = rect.size() / self.size;
        for (texture, bounds, uv) in &self.tiles {
            let tile = egui::Rect::from_min_max(
                rect.min + bounds.min.to_vec2() * scale,
                rect.min + bounds.max.to_vec2() * scale,
            );
            if painter.clip_rect().intersects(tile) {
                painter.image(texture.id(), tile, *uv, egui::Color32::WHITE);
            }
        }
    }
}

pub struct ThumbnailLoader {
    entries: HashMap<ThumbnailKey, ThumbnailEntry>,
    last_synced_requests: Vec<ThumbnailRequest>,
    next_request_id: u64,
    recent: VecDeque<(ThumbnailKey, egui::TextureHandle, usize)>,
    cache_generation: u64,
}

impl Default for ThumbnailLoader {
    fn default() -> Self {
        Self {
            entries: HashMap::new(),
            last_synced_requests: Vec::new(),
            next_request_id: 1,
            recent: VecDeque::new(),
            cache_generation: 0,
        }
    }
}

impl ThumbnailLoader {
    pub fn original(&self, key: &ThumbnailKey) -> Option<&OriginalTexture> {
        match self.entries.get(key) {
            Some(ThumbnailEntry::Original(image)) => Some(image),
            _ => None,
        }
    }
    pub fn texture(&self, key: &ThumbnailKey) -> Option<&egui::TextureHandle> {
        match self.entries.get(key) {
            Some(ThumbnailEntry::Ready(texture)) => Some(texture),
            None => self
                .recent
                .iter()
                .find(|(cached, _, _)| cached == key)
                .map(|(_, texture, _)| texture),
            _ => None,
        }
    }

    pub fn failure(&self, key: &ThumbnailKey) -> Option<&str> {
        match self.entries.get(key) {
            Some(ThumbnailEntry::Failed { message, .. }) => Some(message),
            _ => None,
        }
    }

    pub fn is_settled(&self, key: &ThumbnailKey) -> bool {
        matches!(
            self.entries.get(key),
            Some(
                ThumbnailEntry::Ready(_)
                    | ThumbnailEntry::Original(_)
                    | ThumbnailEntry::Failed { .. }
            )
        )
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
        let same_generation = self.cache_generation == generation;
        if !same_generation {
            self.recent.clear();
            self.cache_generation = generation;
        }
        self.entries.retain(|key, entry| {
            if seen.contains(key) {
                return true;
            }
            // Only finished gallery textures enter the cache. Viewer originals,
            // 1024 previews and pending work retain their existing lifetime.
            if same_generation
                && matches!(key.resolution, ImageResolution::Preview(edge) if edge != 1024)
            {
                if let ThumbnailEntry::Ready(texture) = entry {
                    self.recent
                        .push_back((key.clone(), texture.clone(), texture.byte_size()));
                }
            }
            false
        });

        for key in &keys {
            if !self.entries.contains_key(key) {
                if let Some(index) = self.recent.iter().position(|(cached, _, _)| cached == key) {
                    let (_, texture, _) = self.recent.remove(index).unwrap();
                    self.entries
                        .insert(key.clone(), ThumbnailEntry::Ready(texture));
                }
            }
        }

        let mut bytes: usize = self.recent.iter().map(|(_, _, bytes)| *bytes).sum();
        while bytes > 32 * 1024 * 1024 || self.recent.len() > 256 {
            if let Some((_, _, evicted_bytes)) = self.recent.pop_front() {
                bytes -= evicted_bytes;
            }
        }
        for key in &keys {
            let must_start = match self.entries.get(key) {
                None => true,
                Some(ThumbnailEntry::Pending(request)) => request.identity.generation != generation,
                Some(ThumbnailEntry::Failed {
                    generation: failed_generation,
                    ..
                }) => *failed_generation != generation,
                Some(ThumbnailEntry::Ready(_) | ThumbnailEntry::Original(_)) => false,
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

    pub fn handle_prepared_result(
        &mut self,
        request: &ThumbnailRequest,
        result: Result<PreparedThumbnail, String>,
        ctx: &egui::Context,
    ) -> bool {
        let Some(ThumbnailEntry::Pending(pending)) = self.entries.get(&request.key) else {
            return false;
        };
        if pending.identity != request.identity {
            return false;
        }

        let loaded = result.map(|thumbnail| match thumbnail {
            PreparedThumbnail::Original(prepared) => ThumbnailEntry::Original(
                OriginalTexture::upload(prepared, ctx, &texture_name(&request.key)),
            ),
            PreparedThumbnail::Preview(image) => ThumbnailEntry::Ready(ctx.load_texture(
                texture_name(&request.key),
                image,
                egui::TextureOptions::LINEAR,
            )),
        });
        let entry = match loaded {
            Ok(entry) => entry,
            Err(message) => ThumbnailEntry::Failed {
                generation: request.identity.generation,
                message,
            },
        };
        self.entries.insert(request.key.clone(), entry);
        true
    }

    #[cfg(test)]
    pub fn handle_result(
        &mut self,
        request: &ThumbnailRequest,
        result: Result<DecodedThumbnail, String>,
        ctx: &egui::Context,
    ) -> bool {
        let result = result.and_then(|decoded| {
            decoded.prepare(
                request.key.resolution,
                ctx.input(|input| input.max_texture_side),
                &piclens_infra::CancellationToken::new(),
            )
        });
        self.handle_prepared_result(request, result, ctx)
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
        "thumbnail:{}:{:?}:{}:{:?}",
        key.source.display(),
        key.modified_unix_ms,
        key.file_size,
        key.resolution
    )
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn original_tiles_preserve_dimensions_and_are_released_on_close() {
        let ctx = egui::Context::default();
        let side = ctx.input(|input| input.max_texture_side);
        let width = side as u32 + 3;
        let key = ThumbnailKey::original(&image());
        let mut loader = ThumbnailLoader::default();
        let first = loader
            .sync_materialized(vec![key.clone()], 1)
            .unwrap()
            .remove(0);
        loader.sync_materialized(Vec::new(), 1);
        let second = loader
            .sync_materialized(vec![key.clone()], 1)
            .unwrap()
            .remove(0);
        assert!(!loader.handle_result(&first, Err("stale".into()), &ctx));
        assert!(loader.handle_result(
            &second,
            Ok(DecodedThumbnail {
                width,
                height: 2,
                rgba: vec![255; width as usize * 2 * 4],
            }),
            &ctx
        ));
        let original = loader.original(&key).unwrap();
        assert_eq!(original.size, egui::vec2(width as f32, 2.0));
        assert!(original.tiles.len() > 1);
        assert!(original
            .tiles
            .iter()
            .all(|(texture, _, _)| texture.size()[0] <= side));
        let area: f32 = original.tiles.iter().map(|(_, rect, _)| rect.area()).sum();
        assert_eq!(area, width as f32 * 2.0);
        loader.sync_materialized(Vec::new(), 1);
        assert!(loader.original(&key).is_none());
        assert!(!loader.handle_result(&second, Err("late".into()), &ctx));
    }

    #[test]
    fn gallery_cache_reuses_ready_textures_and_discards_old_generations() {
        let ctx = egui::Context::default();
        let key = ThumbnailKey::from_image(&image(), 160);
        let mut loader = ThumbnailLoader::default();
        let request = loader
            .sync_materialized(vec![key.clone()], 1)
            .unwrap()
            .remove(0);
        loader.handle_result(
            &request,
            Ok(DecodedThumbnail {
                width: 1,
                height: 1,
                rgba: vec![255; 4],
            }),
            &ctx,
        );
        let id = loader.texture(&key).unwrap().id();
        loader.sync_materialized(Vec::new(), 1);
        // The view queries textures before syncing the new materialized range.
        assert_eq!(loader.texture(&key).unwrap().id(), id);
        assert!(loader.sync_materialized(vec![key.clone()], 1).is_none());
        assert_eq!(loader.texture(&key).unwrap().id(), id);
        loader.sync_materialized(Vec::new(), 2);
        assert!(loader.recent.is_empty());
        assert_eq!(loader.sync_materialized(vec![key], 2).unwrap().len(), 1);
    }

    #[test]
    fn gallery_cache_evicts_oldest_texture_at_byte_limit() {
        let ctx = egui::Context::default();
        let mut loader = ThumbnailLoader {
            cache_generation: 1,
            ..Default::default()
        };
        // 9 x 4 MiB exceeds the 32 MiB inactive texture budget.
        for index in 0..9 {
            let mut key = ThumbnailKey::from_image(&image(), 1000);
            key.source = format!("{index}.png").into();
            let texture = ctx.load_texture(
                format!("cache-{index}"),
                egui::ColorImage::filled([1024, 1024], egui::Color32::WHITE),
                egui::TextureOptions::LINEAR,
            );
            let bytes = texture.byte_size();
            loader.recent.push_back((key, texture, bytes));
        }
        loader.sync_materialized(Vec::new(), 1);
        assert_eq!(loader.recent.len(), 8);
        assert_eq!(
            loader.recent.front().unwrap().0.source,
            PathBuf::from("1.png")
        );
    }

    #[test]
    fn original_preparation_preserves_gutters_and_alpha_and_can_cancel() {
        let rgba: Vec<u8> = (0..15).flat_map(|n| [n * 10, 21, 35, 128]).collect();
        let prepared = PreparedOriginal::prepare(
            DecodedThumbnail {
                width: 5,
                height: 3,
                rgba: rgba.clone(),
            },
            4,
            &piclens_infra::CancellationToken::new(),
        )
        .unwrap();
        for (image, bounds, _) in &prepared.tiles {
            let left = (bounds.min.x as usize).saturating_sub(1);
            let top = (bounds.min.y as usize).saturating_sub(1);
            for y in 0..image.size[1] {
                for x in 0..image.size[0] {
                    let i = ((top + y) * 5 + left + x) * 4;
                    assert_eq!(
                        image.pixels[y * image.size[0] + x],
                        egui::Color32::from_rgba_unmultiplied(
                            rgba[i],
                            rgba[i + 1],
                            rgba[i + 2],
                            rgba[i + 3]
                        )
                    );
                }
            }
        }
        let token = piclens_infra::CancellationToken::new();
        token.cancel();
        assert!(PreparedOriginal::prepare(
            DecodedThumbnail {
                width: 5,
                height: 3,
                rgba
            },
            4,
            &token
        )
        .unwrap_err()
        .contains("canceled"));
    }

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
        assert_eq!(
            ThumbnailKey::from_image(&image(), 1).resolution,
            ImageResolution::Preview(16)
        );
    }

    #[test]
    fn decoded_texture_cannot_exceed_its_requested_edge() {
        let key = ThumbnailKey::from_image(&image(), 16);
        let mut loader = ThumbnailLoader::default();
        let request = loader
            .sync_materialized(vec![key.clone()], 1)
            .unwrap()
            .remove(0);

        assert!(loader.handle_result(
            &request,
            Ok(DecodedThumbnail {
                width: 17,
                height: 1,
                rgba: vec![255; 17 * 4],
            }),
            &egui::Context::default(),
        ));
        assert_eq!(
            loader.failure(&key),
            Some("thumbnail decoder returned invalid RGBA dimensions")
        );
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
    fn close_and_reopen_same_image_rejects_the_old_request() {
        let key = ThumbnailKey::from_image(&image(), 1024);
        let mut loader = ThumbnailLoader::default();
        let first = loader
            .sync_materialized(vec![key.clone()], 1)
            .unwrap()
            .remove(0);
        loader.sync_materialized(Vec::new(), 1);
        let second = loader
            .sync_materialized(vec![key.clone()], 1)
            .unwrap()
            .remove(0);

        assert_ne!(first.identity, second.identity);
        assert!(!loader.handle_result(&first, Err("stale".into()), &egui::Context::default()));
        assert!(loader.handle_result(&second, Err("current".into()), &egui::Context::default()));
        assert_eq!(loader.failure(&key), Some("current"));
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
            resolution: ImageResolution::Preview(160),
        };

        assert!(key.source_matches_disk());
        std::fs::write(&source, b"changed size").unwrap();
        assert!(!key.source_matches_disk());

        std::fs::remove_dir_all(root).unwrap();
    }
}
