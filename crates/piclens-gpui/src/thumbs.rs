//! Viewport thumbnail candidate selection.

use std::collections::HashSet;
use std::ops::Range;

use piclens_domain::ListItem;

/// How many grid tiles fit in `available_width` with a uniform tile and gap.
pub fn grid_column_count(available_width: f32, tile_size: f32, gap: f32) -> usize {
    let cell = tile_size + gap;
    if available_width <= 0.0 || cell <= 0.0 {
        return 1;
    }
    (((available_width + gap) / cell).floor() as usize).max(1)
}

/// Convert a range of gallery rows into item indices.
pub fn item_range_for_rows(
    row_range: Range<usize>,
    columns: usize,
    item_count: usize,
) -> Range<usize> {
    let cols = columns.max(1);
    let start = row_range.start.saturating_mul(cols).min(item_count);
    let end = row_range.end.saturating_mul(cols).min(item_count);
    start..end
}

/// Static-image paths in `viewport` that still need a thumbnail.
///
/// Drops `pending` paths that left the viewport. Returns paths to start, up to
/// `max_in_flight` concurrent work (including remaining in-viewport pending).
pub fn thumb_queue_update(
    items: &[ListItem],
    viewport: Range<usize>,
    cached_or_failed: &HashSet<String>,
    pending: &mut HashSet<String>,
    max_in_flight: usize,
) -> Vec<String> {
    let start = viewport.start.min(items.len());
    let end = viewport.end.min(items.len());
    let in_view: HashSet<String> = items[start..end]
        .iter()
        .filter_map(static_image_path)
        .collect();

    pending.retain(|path| in_view.contains(path));

    let mut slots = max_in_flight.saturating_sub(pending.len());
    let mut to_start = Vec::new();
    for path in items[start..end].iter().filter_map(static_image_path) {
        if slots == 0 {
            break;
        }
        if cached_or_failed.contains(&path) || pending.contains(&path) {
            continue;
        }
        pending.insert(path.clone());
        to_start.push(path);
        slots -= 1;
    }
    to_start
}

fn static_image_path(item: &ListItem) -> Option<String> {
    let image = item.as_image()?;
    if image.is_animated {
        None
    } else {
        Some(image.path.clone())
    }
}

#[cfg(test)]
mod tests {
    use std::collections::HashSet;

    use piclens_domain::{FolderListItem, ImageListItem, ListItem};

    use super::{grid_column_count, item_range_for_rows, thumb_queue_update};

    fn image(path: &str, animated: bool) -> ListItem {
        ListItem::Image(ImageListItem {
            path: path.into(),
            name: path.into(),
            extension: "jpg".into(),
            modified_at_ms: None,
            size_bytes: 1,
            is_animated: animated,
        })
    }

    fn folder(path: &str) -> ListItem {
        ListItem::Folder(FolderListItem {
            path: path.into(),
            name: path.into(),
            modified_at_ms: None,
        })
    }

    #[test]
    fn queues_only_in_range_static_images_and_drops_out_of_range_pending() {
        let items = vec![
            image("/a.jpg", false),
            folder("/dir"),
            image("/anim.gif", true),
            image("/b.jpg", false),
            image("/c.jpg", false),
            image("/d.jpg", false),
        ];
        let cached = HashSet::new();
        let mut pending = HashSet::from(["/d.jpg".into(), "/a.jpg".into()]);

        let queued = thumb_queue_update(&items, 0..4, &cached, &mut pending, 8);

        assert_eq!(queued, vec!["/b.jpg".to_string()]);
        assert!(pending.contains("/a.jpg"));
        assert!(pending.contains("/b.jpg"));
        assert!(
            !pending.contains("/d.jpg"),
            "out-of-range pending work must be dropped"
        );
        assert!(!pending.iter().any(|p| p == "/anim.gif"));
        assert!(!pending.iter().any(|p| p == "/dir"));
    }

    #[test]
    fn wide_gallery_fits_more_columns() {
        let tile = 160.0;
        let gap = 12.0;
        assert_eq!(grid_column_count(700.0, tile, gap), 4);
        assert!(grid_column_count(1600.0, tile, gap) > grid_column_count(700.0, tile, gap));
    }

    #[test]
    fn grid_rows_map_to_item_indices() {
        assert_eq!(item_range_for_rows(1..3, 3, 10), 3..9);
        assert_eq!(item_range_for_rows(0..2, 4, 5), 0..5);
    }
}
