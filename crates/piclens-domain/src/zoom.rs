use crate::models::{Point, ZoomState};

pub const MIN_ZOOM: f64 = 0.1;
pub const MAX_ZOOM: f64 = 8.0;
pub const ZOOM_STEP: f64 = 1.2;

pub fn clamp_zoom(zoom: f64) -> f64 {
    zoom.clamp(MIN_ZOOM, MAX_ZOOM)
}

pub fn reset_zoom_state() -> ZoomState {
    ZoomState::default()
}

pub fn zoom_at_point(
    zoom: f64,
    offset: Point,
    viewport_center: Point,
    pointer: Point,
    delta: i32,
) -> ZoomState {
    let next_zoom = clamp_zoom(if delta > 0 {
        zoom * ZOOM_STEP
    } else {
        zoom / ZOOM_STEP
    });
    let image_point = Point {
        x: (pointer.x - viewport_center.x - offset.x) / zoom,
        y: (pointer.y - viewport_center.y - offset.y) / zoom,
    };
    ZoomState {
        zoom: next_zoom,
        offset: Point {
            x: pointer.x - viewport_center.x - image_point.x * next_zoom,
            y: pointer.y - viewport_center.y - image_point.y * next_zoom,
        },
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn clamp_bounds() {
        assert_eq!(clamp_zoom(0.01), MIN_ZOOM);
        assert_eq!(clamp_zoom(99.0), MAX_ZOOM);
    }
}
