#pragma once

#include <piclens/core/models.h>

namespace piclens::core::zoom_math {

inline constexpr double MinZoom = 0.1;
inline constexpr double MaxZoom = 8.0;
inline constexpr double ZoomStep = 1.2;

[[nodiscard]] double clampZoom(double zoom);
[[nodiscard]] ZoomState resetZoomState();
[[nodiscard]] ZoomState zoomAtPoint(
    double zoom,
    Point offset,
    Point viewportCenter,
    Point pointer,
    int delta);

} // namespace piclens::core::zoom_math
