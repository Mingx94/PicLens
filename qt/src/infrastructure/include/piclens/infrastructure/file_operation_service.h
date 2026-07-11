#pragma once

#include <piclens/core/models.h>

#include <functional>
#include <stdexcept>
#include <stop_token>

namespace piclens::infrastructure {

class FileOperationCanceledError final : public std::runtime_error
{
public:
    FileOperationCanceledError();
};

class FileOperationService final
{
public:
    using JpegEncoder = std::function<void(const QString &, const QString &, std::stop_token)>;
    using TrashHandler = std::function<void(const QString &, std::stop_token)>;

    FileOperationService();
    FileOperationService(JpegEncoder encoder, TrashHandler trashHandler);

    [[nodiscard]] core::FileOperationBatchResult convertVisibleToJpg(
        const QVector<core::ImageListItem> &visibleImages,
        std::stop_token stopToken = {}) const;

    [[nodiscard]] core::FileOperationBatchResult trashSameBasenameNonJpg(
        const QVector<core::ImageListItem> &visibleImages,
        std::stop_token stopToken = {}) const;

    [[nodiscard]] core::FileOperationResult trash(
        const QString &path,
        std::stop_token stopToken = {}) const;

    [[nodiscard]] core::FileOperationResult rename(
        const QString &sourcePath,
        const QString &newFileName,
        std::stop_token stopToken = {}) const;

    [[nodiscard]] core::FileOperationBatchResult renameByDropTarget(
        const QVector<QString> &sourcePaths,
        const QString &targetPath,
        std::stop_token stopToken = {}) const;

private:
    static void throwIfCanceled(std::stop_token stopToken);
    [[nodiscard]] core::FileOperationResult convertOneToJpg(
        const core::ImageListItem &image,
        std::stop_token stopToken) const;

    JpegEncoder m_encoder;
    TrashHandler m_trashHandler;
};

} // namespace piclens::infrastructure
