#pragma once

#include <QString>

#include <optional>

namespace piclens::core::image_format_rules {

[[nodiscard]] std::optional<QString> supportedImageExtension(const QString &filePath);

} // namespace piclens::core::image_format_rules
