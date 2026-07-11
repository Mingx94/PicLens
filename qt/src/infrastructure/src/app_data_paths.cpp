#include <piclens/infrastructure/app_data_paths.h>

#include <QDir>
#include <QProcessEnvironment>
#include <QRegularExpression>
#include <QStandardPaths>

namespace piclens::infrastructure::app_data_paths {
namespace {

QString expandEnvironmentVariables(QString value)
{
    static const QRegularExpression pattern(QStringLiteral("%([^%]+)%"));
    const QProcessEnvironment environment = QProcessEnvironment::systemEnvironment();
    qsizetype offset = 0;
    while (true) {
        const QRegularExpressionMatch match = pattern.match(value, offset);
        if (!match.hasMatch()) {
            break;
        }
        const QString name = match.captured(1);
        const QString replacement = environment.contains(name) ? environment.value(name) : match.captured(0);
        value.replace(match.capturedStart(), match.capturedLength(), replacement);
        offset = match.capturedStart() + replacement.size();
    }
    return value;
}

QString localAppDataRoot()
{
    const QString dataRoot = QStandardPaths::writableLocation(QStandardPaths::GenericDataLocation);
    return dataRoot.trimmed().isEmpty() ? QDir::tempPath() : dataRoot;
}

} // namespace

QString appRoot()
{
    const QString configuredRoot = qEnvironmentVariable(DataRootEnvironmentVariable);
    if (!configuredRoot.trimmed().isEmpty()) {
        return QDir::cleanPath(QFileInfo(expandEnvironmentVariables(configuredRoot)).absoluteFilePath());
    }
    return QDir(localAppDataRoot()).filePath(QStringLiteral("PicLens"));
}

QString settingsPath()
{
    return QDir(appRoot()).filePath(QStringLiteral("piclens-settings.json"));
}

QString logPath()
{
    return QDir(appRoot()).filePath(QStringLiteral("Logs/PicLens.log"));
}

QString thumbnailCacheRoot()
{
    return QDir(appRoot()).filePath(QStringLiteral("Thumbnails"));
}

} // namespace piclens::infrastructure::app_data_paths
