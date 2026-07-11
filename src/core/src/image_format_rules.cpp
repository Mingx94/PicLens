#include <piclens/core/image_format_rules.h>

#include <QFileInfo>
#include <QSet>

namespace piclens::core::image_format_rules {

std::optional<QString> supportedImageExtension(const QString &filePath)
{
    static const QSet<QString> supportedExtensions{
        QStringLiteral("jpg"),
        QStringLiteral("jpeg"),
        QStringLiteral("png"),
        QStringLiteral("bmp"),
        QStringLiteral("webp"),
        QStringLiteral("gif"),
    };

    QString normalizedPath = filePath;
    normalizedPath.replace(QLatin1Char('\\'), QLatin1Char('/'));
    const QString extension = QFileInfo(normalizedPath).suffix().toLower();
    return supportedExtensions.contains(extension)
        ? std::optional<QString>{extension}
        : std::nullopt;
}

} // namespace piclens::core::image_format_rules
