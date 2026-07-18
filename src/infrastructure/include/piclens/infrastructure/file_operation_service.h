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
    using ImageEncoder = std::function<void(const QString &, const QString &, std::stop_token)>;
    using TrashHandler = std::function<void(const QString &, std::stop_token)>;

    FileOperationService();
    FileOperationService(ImageEncoder jpegEncoder, TrashHandler trashHandler);
    FileOperationService(
        ImageEncoder jpegEncoder,
        ImageEncoder webpEncoder,
        TrashHandler trashHandler,
        int maxConcurrentConversions = 0);

    [[nodiscard]] core::FileOperationBatchResult convertVisibleToJpg(
        const QVector<core::ImageListItem> &visibleImages,
        std::stop_token stopToken = {}) const;

    [[nodiscard]] core::FileOperationBatchResult convertVisibleToWebp(
        const QVector<core::ImageListItem> &visibleImages,
        std::stop_token stopToken = {}) const;

    [[nodiscard]] core::FileOperationBatchResult trashSameBasenameExtras(
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
    [[nodiscard]] core::FileOperationBatchResult convertBatch(
        const QVector<core::ImageListItem> &visibleImages,
        std::stop_token stopToken,
        bool convertToWebp) const;
    [[nodiscard]] core::FileOperationResult convertOneToJpg(
        const core::ImageListItem &image,
        std::stop_token stopToken) const;
    [[nodiscard]] core::FileOperationResult convertOneToWebp(
        const core::ImageListItem &image,
        std::stop_token stopToken) const;

    ImageEncoder m_jpegEncoder;
    ImageEncoder m_webpEncoder;
    TrashHandler m_trashHandler;
    int m_maxConcurrentConversions;
};

} // namespace piclens::infrastructure
