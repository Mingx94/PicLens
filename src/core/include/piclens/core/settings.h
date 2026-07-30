#pragma once

#include <piclens/core/library_items.h>

#include <QString>

#include <optional>

namespace piclens::core {

inline constexpr int DefaultThumbnailSize = 160;

struct AppSettings {
    std::optional<QString> lastFolderPath;
    SortState sort;
    bool includeSubfolders = false;
    int thumbnailSize = DefaultThumbnailSize;

    bool operator==(const AppSettings &) const = default;
};

struct AppSettingsPatch {
    std::optional<QString> lastFolderPath;
    bool hasLastFolderPath = false;
    std::optional<SortState> sort;
    std::optional<bool> includeSubfolders;
    std::optional<int> thumbnailSize;
};

} // namespace piclens::core
