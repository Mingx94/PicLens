#include <piclens/core/zoom_math.h>

#include <algorithm>

namespace piclens::core::zoom_math {

double clampZoom(double zoom)
{
    return std::clamp(zoom, MinZoom, MaxZoom);
}

ZoomState resetZoomState()
{
    return {};
}

ZoomState zoomAtPoint(
    double zoom,
    Point offset,
    Point viewportCenter,
    Point pointer,
    int delta)
{
    const double nextZoom = clampZoom(delta > 0 ? zoom * ZoomStep : zoom / ZoomStep);
    const Point imagePoint{
        .x = (pointer.x - viewportCenter.x - offset.x) / zoom,
        .y = (pointer.y - viewportCenter.y - offset.y) / zoom,
    };

    return {
        .zoom = nextZoom,
        .offset = {
            .x = pointer.x - viewportCenter.x - imagePoint.x * nextZoom,
            .y = pointer.y - viewportCenter.y - imagePoint.y * nextZoom,
        },
    };
}

} // namespace piclens::core::zoom_math
