#pragma once

#include <piclens/core/models.h>

namespace piclens::core::settings_rules {

inline constexpr int DefaultThumbnailSize = 160;
inline constexpr int MinThumbnailSize = 120;
inline constexpr int MaxThumbnailSize = 240;
inline constexpr int ThumbnailSizeStep = 20;

[[nodiscard]] AppSettings normalizeSettings(const AppSettings &settings);
[[nodiscard]] int normalizeThumbnailSize(double thumbnailSize);
[[nodiscard]] AppSettings mergeSettingsPatch(
    const AppSettings &current,
    const AppSettingsPatch &patch);

} // namespace piclens::core::settings_rules
