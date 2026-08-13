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

pub fn pan_offset(offset: Point, delta: Point) -> Point {
    Point {
        x: offset.x + delta.x,
        y: offset.y + delta.y,
    }
}

/// Pixel size of the viewer image box. Zoom `1.0` fills the canvas (contain).
pub fn viewer_display_box(canvas_width: f64, canvas_height: f64, zoom: f64) -> (f64, f64) {
    let zoom = clamp_zoom(zoom);
    (
        canvas_width.max(1.0) * zoom,
        canvas_height.max(1.0) * zoom,
    )
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
    fn pan_adds_drag_delta() {
        let moved = pan_offset(Point { x: 10.0, y: -4.0 }, Point { x: 8.0, y: 3.0 });
        assert_eq!(moved.x, 18.0);
        assert_eq!(moved.y, -1.0);
    }

    #[test]
    fn display_box_fills_canvas_at_fit_zoom() {
        assert_eq!(viewer_display_box(1280.0, 720.0, 1.0), (1280.0, 720.0));
        let (w, h) = viewer_display_box(1280.0, 720.0, ZOOM_STEP);
        assert!((w - 1280.0 * ZOOM_STEP).abs() < 1e-9);
        assert!((h - 720.0 * ZOOM_STEP).abs() < 1e-9);
        assert!(is_fit_view(1.0, Point::default()));
        assert!(!is_fit_view(ZOOM_STEP, Point::default()));
    }
}
