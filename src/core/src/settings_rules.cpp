#include <piclens/core/settings_rules.h>

#include <algorithm>
#include <cmath>

namespace piclens::core::settings_rules {

AppSettings normalizeSettings(const AppSettings &settings)
{
    AppSettings normalized = settings;
    normalized.thumbnailSize = settings.thumbnailSize == 0
        ? DefaultThumbnailSize
        : normalizeThumbnailSize(settings.thumbnailSize);
    return normalized;
}

int normalizeThumbnailSize(double thumbnailSize)
{
    if (!std::isfinite(thumbnailSize)) {
        return DefaultThumbnailSize;
    }

    const int stepped = static_cast<int>(std::round(thumbnailSize / ThumbnailSizeStep))
        * ThumbnailSizeStep;
    return std::clamp(stepped, MinThumbnailSize, MaxThumbnailSize);
}

AppSettings mergeSettingsPatch(const AppSettings &current, const AppSettingsPatch &patch)
{
    AppSettings merged = normalizeSettings(current);
    if (patch.hasLastFolderPath) {
        merged.lastFolderPath = patch.lastFolderPath;
    }
    if (patch.sort.has_value()) {
        merged.sort = *patch.sort;
    }
    if (patch.includeSubfolders.has_value()) {
        merged.includeSubfolders = *patch.includeSubfolders;
    }
    if (patch.thumbnailSize.has_value()) {
        merged.thumbnailSize = normalizeThumbnailSize(*patch.thumbnailSize);
    }
    return merged;
}

} // namespace piclens::core::settings_rules
