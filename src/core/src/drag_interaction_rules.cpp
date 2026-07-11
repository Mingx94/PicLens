#include <piclens/core/drag_interaction_rules.h>

#include <algorithm>
#include <cmath>

namespace piclens::core::drag_interaction_rules {

double calculateAutoScrollDelta(
    double pointerY,
    double viewportHeight,
    double edgeSize,
    double maxStep)
{
    if (!std::isfinite(pointerY) || !std::isfinite(viewportHeight)
        || !std::isfinite(edgeSize) || !std::isfinite(maxStep)
        || viewportHeight <= 0 || edgeSize <= 0 || maxStep <= 0) {
        return 0;
    }
    const double effectiveEdge = std::min(edgeSize, viewportHeight / 2.0);
    if (pointerY < effectiveEdge) {
        const double strength = (effectiveEdge - pointerY) / effectiveEdge;
        return -std::clamp(strength * maxStep, 0.0, maxStep);
    }
    if (pointerY > viewportHeight - effectiveEdge) {
        const double strength = (pointerY - (viewportHeight - effectiveEdge)) / effectiveEdge;
        return std::clamp(strength * maxStep, 0.0, maxStep);
    }
    return 0;
}

} // namespace piclens::core::drag_interaction_rules
