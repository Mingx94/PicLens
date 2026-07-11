#pragma once

#include <QString>

namespace piclens::infrastructure::app_data_paths {

inline constexpr auto DataRootEnvironmentVariable = "PICLENS_DATA_ROOT";

[[nodiscard]] QString appRoot();
[[nodiscard]] QString settingsPath();
[[nodiscard]] QString logPath();
[[nodiscard]] QString thumbnailCacheRoot();

} // namespace piclens::infrastructure::app_data_paths
