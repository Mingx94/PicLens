#pragma once

namespace piclens::core::drag_interaction_rules {

[[nodiscard]] double calculateAutoScrollDelta(
    double pointerY,
    double viewportHeight,
    double edgeSize = 72.0,
    double maxStep = 48.0);

} // namespace piclens::core::drag_interaction_rules
