use crate::models::{Point, ZoomState};

pub const MIN_ZOOM: f64 = 0.1;
pub const MAX_ZOOM: f64 = 8.0;
pub const ZOOM_STEP: f64 = 1.2;

pub fn clamp_zoom(zoom: f64) -> f64 {
    zoom.clamp(MIN_ZOOM, MAX_ZOOM)
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

pub fn pan_offset(offset: Point, delta: Point) -> Point {
    Point {
        x: offset.x + delta.x,
        y: offset.y + delta.y,
    }
}

pub fn is_fit_view(zoom: f64, offset: Point) -> bool {
    zoom <= 1.01 && offset.x.abs() < 0.5 && offset.y.abs() < 0.5
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn clamp_bounds() {
        assert_eq!(clamp_zoom(0.01), MIN_ZOOM);
        assert_eq!(clamp_zoom(99.0), MAX_ZOOM);
    }

    #[test]
    fn wheel_zoom_keeps_pointer_anchor() {
        let viewport_center = Point { x: 200.0, y: 150.0 };
        let pointer = Point { x: 260.0, y: 180.0 };
        let start = zoom_at_point(1.0, Point::default(), viewport_center, pointer, 1);
        let image_x = (pointer.x - viewport_center.x - start.offset.x) / start.zoom;
        let image_y = (pointer.y - viewport_center.y - start.offset.y) / start.zoom;
        assert!((image_x - 60.0).abs() < 1e-9);
        assert!((image_y - 30.0).abs() < 1e-9);
        assert_eq!(start.zoom, ZOOM_STEP);
    }

    #[test]
    fn fit_view_requires_fit_zoom_and_centered_offset() {
        assert!(is_fit_view(1.0, Point::default()));
        assert!(!is_fit_view(ZOOM_STEP, Point::default()));
        assert!(!is_fit_view(1.0, Point { x: 1.0, y: 0.0 }));
        assert!(!is_fit_view(1.0, Point { x: 0.0, y: 1.0 }));
    }
}
