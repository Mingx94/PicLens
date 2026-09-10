//! Source-owned egui components inspired by shadcn/ui.
//! Components return responses; application actions and file operations stay in views.

mod button;
mod feedback;
mod field;
mod gallery;
mod surface;

pub use button::{Button, ButtonSize, ButtonVariant};
pub use feedback::{alert, Toast, ToastResponse};
pub use field::{checkbox, select, slider, Input};
pub use gallery::{gallery_thumbnail_size, gallery_tile_height, GalleryTile, GalleryTilePreview};
pub use surface::{badge, dialog, focus_ring, input_frame, surface_frame, Surface};
