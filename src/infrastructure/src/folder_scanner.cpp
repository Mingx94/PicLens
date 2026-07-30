#include <piclens/infrastructure/folder_scanner.h>

#include <piclens/core/image_format_rules.h>
#include <piclens/core/list_item_sorter.h>
#include <piclens/core/path_rules.h>
#include <piclens/infrastructure/image_animation_probe.h>

#include <QDir>
#include <QFileInfo>
#include <QSet>

namespace piclens::infrastructure {
namespace {

QDir::Filters directoryFilters()
{
    return QDir::Dirs | QDir::Hidden | QDir::System | QDir::NoDotAndDotDot;
}

QDir::Filters fileFilters()
{
    return QDir::Files | QDir::Hidden | QDir::System;
}

QString canonicalDirectoryKey(const QString &path)
{
    QString canonical = QFileInfo(path).canonicalFilePath();
    if (canonical.isEmpty()) {
        return {};
    }
    canonical = QDir::cleanPath(canonical);
    return core::path_rules::pathCaseSensitivity() == Qt::CaseInsensitive
        ? canonical.toCaseFolded()
        : canonical;
}

} // namespace

DirectoryNotFoundError::DirectoryNotFoundError(const QString &path)
    : std::runtime_error(QStringLiteral("Directory not found: %1").arg(path).toStdString())
{
}

ScanCanceledError::ScanCanceledError()
    : std::runtime_error("Folder scan canceled.")
{
}

QVector<core::ListItem> FolderScanner::scan(
    const core::ListQuery &query,
    std::stop_token stopToken) const
{
    throwIfCanceled(stopToken);
    const QFileInfo rootInfo(query.folderPath);
    if (!rootInfo.exists() || !rootInfo.isDir()) {
        throw DirectoryNotFoundError(query.folderPath);
    }

    QVector<core::ListItem> items;
    if (!query.includeSubfolders) {
        const auto folders = directFolders(query.folderPath, stopToken);
        items.reserve(folders.size());
        for (const auto &folder : folders) {
            items.append(folder);
        }

        for (const QFileInfo &file : directFiles(query.folderPath)) {
            throwIfCanceled(stopToken);
            if (auto image = createImageItem(file); image.has_value()) {
                items.append(std::move(*image));
            }
        }
    } else {
        QVector<QString> pending{rootInfo.absoluteFilePath()};
        QSet<QString> visited;

        while (!pending.isEmpty()) {
            throwIfCanceled(stopToken);
            const QString current = pending.takeLast();
            const QString canonical = canonicalDirectoryKey(current);
            if (canonical.isEmpty() || visited.contains(canonical)) {
                continue;
            }
            visited.insert(canonical);

            const auto folders = directFolders(current, stopToken);
            for (const auto &folder : folders) {
                pending.append(folder.path);
            }
            for (const QFileInfo &file : directFiles(current)) {
                throwIfCanceled(stopToken);
                if (auto image = createImageItem(file); image.has_value()) {
                    items.append(std::move(*image));
                }
            }
        }
    }

    return core::list_item_sorter::sort(
        items,
        query.sort,
        !query.includeSubfolders);
}

QVector<core::FolderListItem> FolderScanner::scanChildFolders(
    const QString &folderPath,
    std::stop_token stopToken) const
{
    throwIfCanceled(stopToken);
    const QFileInfo rootInfo(folderPath);
    if (!rootInfo.exists() || !rootInfo.isDir()) {
        throw DirectoryNotFoundError(folderPath);
    }

    QVector<core::ListItem> items;
    const auto folders = directFolders(folderPath, stopToken);
    items.reserve(folders.size());
    for (const auto &folder : folders) {
        items.append(folder);
    }

    const auto sorted = core::list_item_sorter::sort(
        items,
        {.key = core::SortKey::Name, .direction = core::SortDirection::Asc},
        false);
    QVector<core::FolderListItem> result;
    result.reserve(sorted.size());
    for (const auto &item : sorted) {
        result.append(std::get<core::FolderListItem>(item));
    }
    return result;
}

void FolderScanner::throwIfCanceled(std::stop_token stopToken)
{
    if (stopToken.stop_requested()) {
        throw ScanCanceledError();
    }
}

QVector<core::FolderListItem> FolderScanner::directFolders(
    const QString &folderPath,
    std::stop_token stopToken)
{
    const QFileInfoList entries = QDir(folderPath).entryInfoList(directoryFilters(), QDir::NoSort);
    QVector<core::FolderListItem> folders;
    folders.reserve(entries.size());
    for (const QFileInfo &entry : entries) {
        throwIfCanceled(stopToken);
        folders.append({
            .path = entry.absoluteFilePath(),
            .name = entry.fileName(),
            .modifiedAtMs = entry.lastModified().toMSecsSinceEpoch(),
        });
    }
    return folders;
}

QVector<QFileInfo> FolderScanner::directFiles(const QString &folderPath)
{
    return QDir(folderPath).entryInfoList(fileFilters(), QDir::NoSort);
}

std::optional<core::ImageListItem> FolderScanner::createImageItem(const QFileInfo &file)
{
    const auto extension = core::image_format_rules::supportedImageExtension(file.absoluteFilePath());
    if (!extension.has_value()) {
        return std::nullopt;
    }

    const bool requiresAnimationProbe = extension->compare(QStringLiteral("gif"), Qt::CaseInsensitive) == 0
        || extension->compare(QStringLiteral("webp"), Qt::CaseInsensitive) == 0;
    return core::ImageListItem{
        .path = file.absoluteFilePath(),
        .name = file.fileName(),
        .extension = *extension,
        .modifiedAtMs = file.lastModified().toMSecsSinceEpoch(),
        .sizeBytes = file.size(),
        .isAnimated = requiresAnimationProbe
            && image_animation_probe::isAnimated(file.absoluteFilePath()),
    };
}

} // namespace piclens::infrastructure
