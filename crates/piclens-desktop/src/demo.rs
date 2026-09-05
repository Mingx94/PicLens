//! Deterministic interface states for headless render and screenshot tests.

use piclens_domain::{FolderListItem, ImageListItem, ListItem, ListQuery};

use crate::model::{AppModel, Loadable};

pub fn loaded_library() -> AppModel {
    let query = ListQuery {
        folder_path: "C:/fixture".into(),
        include_subfolders: false,
        sort: Default::default(),
    };
    let mut model = AppModel::new(None);
    model.current_folder = Some(query.folder_path.clone().into());
    model.library_query = Some(query);
    let items = vec![
        ListItem::Folder(FolderListItem {
            path: "C:/fixture/album".into(),
            name: "album".into(),
            modified_at_ms: None,
        }),
        ListItem::Image(ImageListItem {
            path: "C:/fixture/image2.png".into(),
            name: "image2.png".into(),
            extension: "png".into(),
            modified_at_ms: None,
            size_bytes: 42,
            is_animated: false,
        }),
    ];
    model.visible_items = items.clone();
    model.library = Loadable::Ready(items);
    model
}

pub fn large_library(item_count: usize) -> AppModel {
    let mut model = loaded_library();
    let items = (0..item_count)
        .map(|index| {
            ListItem::Image(ImageListItem {
                path: format!("C:/fixture/image{index}.png"),
                name: format!("image{index}.png"),
                extension: "png".into(),
                modified_at_ms: None,
                size_bytes: 42,
                is_animated: false,
            })
        })
        .collect::<Vec<_>>();
    model.visible_items = items.clone();
    model.library = Loadable::Ready(items);
    model
}
