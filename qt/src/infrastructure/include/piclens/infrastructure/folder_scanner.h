#pragma once

#include <piclens/core/models.h>

#include <QFileInfo>

#include <optional>
#include <stdexcept>
#include <stop_token>

namespace piclens::infrastructure {

class DirectoryNotFoundError final : public std::runtime_error
{
public:
    explicit DirectoryNotFoundError(const QString &path);
};

class ScanCanceledError final : public std::runtime_error
{
public:
    ScanCanceledError();
};

class FolderScanner final
{
public:
    [[nodiscard]] QVector<core::ListItem> scan(
        const core::ListQuery &query,
        std::stop_token stopToken = {}) const;

    [[nodiscard]] QVector<core::FolderListItem> scanChildFolders(
        const QString &folderPath,
        std::stop_token stopToken = {}) const;

    [[nodiscard]] static bool isKnownAnimatedImage(const QString &path);

private:
    static void throwIfCanceled(std::stop_token stopToken);
    [[nodiscard]] static QVector<core::FolderListItem> directFolders(
        const QString &folderPath,
        std::stop_token stopToken);
    [[nodiscard]] static QVector<QFileInfo> directFiles(const QString &folderPath);
    [[nodiscard]] static std::optional<core::ImageListItem> createImageItem(const QFileInfo &file);
};

} // namespace piclens::infrastructure
