#include <piclens/infrastructure/file_operation_service.h>

#include <piclens/core/file_rename_planner.h>
#include <piclens/core/image_format_rules.h>
#include <piclens/core/path_rules.h>
#include <piclens/infrastructure/platform_file_manager.h>

#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QImageReader>
#include <QImageWriter>
#include <QSet>

#include <stdexcept>
#include <utility>

namespace piclens::infrastructure {
namespace {

bool isJpgExtension(const QString &extension)
{
    return extension.compare(QStringLiteral("jpg"), Qt::CaseInsensitive) == 0
        || extension.compare(QStringLiteral("jpeg"), Qt::CaseInsensitive) == 0;
}

bool isWebpExtension(const QString &extension)
{
    return extension.compare(QStringLiteral("webp"), Qt::CaseInsensitive) == 0;
}

QString basenameKey(const QString &path)
{
    const QFileInfo info(path);
    QString key = QDir::cleanPath(info.absolutePath()) + QChar::Null + info.completeBaseName();
    return core::path_rules::pathCaseSensitivity() == Qt::CaseInsensitive
        ? key.toCaseFolded()
        : key;
}

QString exceptionMessage(const std::exception &exception)
{
    return QString::fromUtf8(exception.what());
}

core::FileOperationResult makeResult(
    QString path,
    core::FileOperationStatus status,
    std::optional<QString> targetPath = std::nullopt,
    std::optional<QString> reason = std::nullopt,
    std::optional<QString> message = std::nullopt)
{
    return {
        .path = std::move(path),
        .status = status,
        .targetPath = std::move(targetPath),
        .reason = std::move(reason),
        .message = std::move(message),
    };
}

core::FileOperationResult invalidRenameRequest(
    const QString &sourcePath,
    const std::optional<QString> &validationReason)
{
    return makeResult(
        sourcePath,
        core::FileOperationStatus::Failed,
        std::nullopt,
        QStringLiteral("invalid_request"),
        validationReason == QStringLiteral("unsupported_extension")
            ? QStringLiteral("檔名必須使用支援的圖片副檔名。")
            : QStringLiteral("檔名必須是不含路徑分隔符號的單一檔名。"));
}

QVector<QString> existingTargetDirectoryFiles(const QString &targetPath)
{
    const QDir directory(QFileInfo(targetPath).absolutePath());
    if (!directory.exists()) {
        return {};
    }

    QVector<QString> paths;
    const QFileInfoList files = directory.entryInfoList(
        QDir::Files | QDir::Hidden | QDir::System,
        QDir::NoSort);
    paths.reserve(files.size());
    for (const QFileInfo &file : files) {
        paths.append(file.absoluteFilePath());
    }
    return paths;
}

void encodeImage(
    const QString &sourcePath,
    const QString &targetPath,
    const QByteArray &format,
    int quality,
    const QString &formatName,
    std::stop_token stopToken)
{
    if (stopToken.stop_requested()) {
        throw FileOperationCanceledError();
    }

    QImageReader reader(sourcePath);
    reader.setAutoTransform(true);
    const QImage image = reader.read();
    if (image.isNull()) {
        throw std::runtime_error(
            QStringLiteral("Image could not be decoded: %1").arg(reader.errorString()).toStdString());
    }
    if (stopToken.stop_requested()) {
        throw FileOperationCanceledError();
    }

    QFile output(targetPath);
    if (!output.open(QIODevice::WriteOnly | QIODevice::NewOnly)) {
        throw std::runtime_error(output.errorString().toStdString());
    }

    QImageWriter writer(&output, format);
    writer.setQuality(quality);
    if (!writer.canWrite()) {
        throw std::runtime_error(
            QStringLiteral("%1 encoder is unavailable: %2")
                .arg(formatName, writer.errorString())
                .toStdString());
    }
    if (!writer.write(image)) {
        const QString message = writer.errorString();
        output.close();
        QFile::remove(targetPath);
        throw std::runtime_error(
            QStringLiteral("%1 could not be encoded: %2")
                .arg(formatName, message)
                .toStdString());
    }
    output.close();
}

void encodeAsJpeg(
    const QString &sourcePath,
    const QString &targetPath,
    std::stop_token stopToken)
{
    encodeImage(
        sourcePath,
        targetPath,
        QByteArrayLiteral("jpeg"),
        100,
        QStringLiteral("JPEG"),
        stopToken);
}

void encodeAsLosslessWebp(
    const QString &sourcePath,
    const QString &targetPath,
    std::stop_token stopToken)
{
    encodeImage(
        sourcePath,
        targetPath,
        QByteArrayLiteral("webp"),
        100,
        QStringLiteral("WebP"),
        stopToken);
}

} // namespace

FileOperationCanceledError::FileOperationCanceledError()
    : std::runtime_error("File operation canceled.")
{
}

FileOperationService::FileOperationService()
    : FileOperationService(
          encodeAsJpeg,
          encodeAsLosslessWebp,
          [](const QString &path, std::stop_token stopToken) {
              if (stopToken.stop_requested()) {
                  throw FileOperationCanceledError();
              }
              PlatformFileManager().moveToTrash(path);
          })
{
}

FileOperationService::FileOperationService(ImageEncoder jpegEncoder, TrashHandler trashHandler)
    : FileOperationService(
          std::move(jpegEncoder),
          encodeAsLosslessWebp,
          std::move(trashHandler))
{
}

FileOperationService::FileOperationService(
    ImageEncoder jpegEncoder,
    ImageEncoder webpEncoder,
    TrashHandler trashHandler)
    : m_jpegEncoder(std::move(jpegEncoder))
    , m_webpEncoder(std::move(webpEncoder))
    , m_trashHandler(std::move(trashHandler))
{
    if (!m_jpegEncoder || !m_webpEncoder || !m_trashHandler) {
        throw std::invalid_argument("File operation handlers are required.");
    }
}

core::FileOperationBatchResult FileOperationService::convertVisibleToJpg(
    const QVector<core::ImageListItem> &visibleImages,
    std::stop_token stopToken) const
{
    core::FileOperationBatchResult result;
    result.items.reserve(visibleImages.size());
    for (const auto &image : visibleImages) {
        throwIfCanceled(stopToken);
        result.items.append(convertOneToJpg(image, stopToken));
    }
    return result;
}

core::FileOperationBatchResult FileOperationService::convertVisibleToWebp(
    const QVector<core::ImageListItem> &visibleImages,
    std::stop_token stopToken) const
{
    core::FileOperationBatchResult result;
    result.items.reserve(visibleImages.size());
    for (const auto &image : visibleImages) {
        throwIfCanceled(stopToken);
        result.items.append(convertOneToWebp(image, stopToken));
    }
    return result;
}

core::FileOperationBatchResult FileOperationService::trashSameBasenameNonJpg(
    const QVector<core::ImageListItem> &visibleImages,
    std::stop_token stopToken) const
{
    QSet<QString> jpgBasenames;
    for (const auto &image : visibleImages) {
        if (isJpgExtension(image.extension)) {
            jpgBasenames.insert(basenameKey(image.path));
        }
    }

    core::FileOperationBatchResult result;
    result.items.reserve(visibleImages.size());
    for (const auto &image : visibleImages) {
        throwIfCanceled(stopToken);
        if (isJpgExtension(image.extension)) {
            result.items.append(makeResult(
                image.path,
                core::FileOperationStatus::Skipped,
                std::nullopt,
                QStringLiteral("already_jpg")));
        } else if (!jpgBasenames.contains(basenameKey(image.path))) {
            result.items.append(makeResult(
                image.path,
                core::FileOperationStatus::Skipped,
                std::nullopt,
                QStringLiteral("no_matching_jpg")));
        } else {
            result.items.append(trash(image.path, stopToken));
        }
    }
    return result;
}

core::FileOperationResult FileOperationService::trash(
    const QString &path,
    std::stop_token stopToken) const
{
    throwIfCanceled(stopToken);
    if (!QFileInfo(path).exists()) {
        return makeResult(
            path,
            core::FileOperationStatus::Failed,
            std::nullopt,
            QStringLiteral("source_missing"));
    }

    try {
        m_trashHandler(path, stopToken);
        throwIfCanceled(stopToken);
        return makeResult(path, core::FileOperationStatus::Trashed);
    } catch (const FileOperationCanceledError &) {
        throw;
    } catch (const std::exception &exception) {
        return makeResult(
            path,
            core::FileOperationStatus::Failed,
            std::nullopt,
            QStringLiteral("trash_failed"),
            exceptionMessage(exception));
    }
}

core::FileOperationResult FileOperationService::rename(
    const QString &sourcePath,
    const QString &newFileName,
    std::stop_token stopToken) const
{
    throwIfCanceled(stopToken);
    const auto validation = core::file_rename_planner::validateImageFileName(newFileName);
    if (!validation.isValid) {
        return invalidRenameRequest(sourcePath, validation.reason);
    }
    if (!core::image_format_rules::supportedImageExtension(sourcePath).has_value()) {
        return makeResult(
            sourcePath,
            core::FileOperationStatus::Failed,
            std::nullopt,
            QStringLiteral("invalid_request"),
            QStringLiteral("路徑必須指向支援的圖片檔案。"));
    }

    const QFileInfo sourceInfo(sourcePath);
    if (!sourceInfo.exists() || !sourceInfo.isFile()) {
        return makeResult(
            sourcePath,
            core::FileOperationStatus::Failed,
            std::nullopt,
            QStringLiteral("source_missing"));
    }

    const QString targetPath = QDir(sourceInfo.absolutePath()).filePath(newFileName);
    if (core::path_rules::pathEquals(sourcePath, targetPath)) {
        return makeResult(
            sourcePath,
            core::FileOperationStatus::Skipped,
            targetPath,
            QStringLiteral("same_name"));
    }
    if (QFileInfo::exists(targetPath)) {
        return makeResult(
            sourcePath,
            core::FileOperationStatus::Failed,
            targetPath,
            QStringLiteral("invalid_request"),
            QStringLiteral("已有相同名稱的檔案。"));
    }

    QFile source(sourcePath);
    if (source.rename(targetPath)) {
        return makeResult(sourcePath, core::FileOperationStatus::Renamed, targetPath);
    }
    return makeResult(
        sourcePath,
        core::FileOperationStatus::Failed,
        targetPath,
        QStringLiteral("rename_failed"),
        source.errorString());
}

core::FileOperationBatchResult FileOperationService::renameByDropTarget(
    const QVector<QString> &sourcePaths,
    const QString &targetPath,
    std::stop_token stopToken) const
{
    const auto plan = core::file_rename_planner::planDropTargetBatchRename(
        sourcePaths,
        targetPath,
        existingTargetDirectoryFiles(targetPath));
    core::FileOperationBatchResult result;
    result.items.reserve(plan.items.size());

    for (const auto &item : plan.items) {
        throwIfCanceled(stopToken);
        if (item.shouldSkip) {
            result.items.append(makeResult(
                item.sourcePath,
                core::FileOperationStatus::Skipped,
                item.targetPath,
                item.reason));
            continue;
        }
        if (!QFileInfo(item.sourcePath).isFile()) {
            result.items.append(makeResult(
                item.sourcePath,
                core::FileOperationStatus::Failed,
                item.targetPath,
                QStringLiteral("source_missing")));
            continue;
        }

        QFile source(item.sourcePath);
        if (source.rename(item.targetPath)) {
            result.items.append(makeResult(
                item.sourcePath,
                core::FileOperationStatus::Renamed,
                item.targetPath));
        } else {
            result.items.append(makeResult(
                item.sourcePath,
                core::FileOperationStatus::Failed,
                item.targetPath,
                QStringLiteral("rename_failed"),
                source.errorString()));
        }
    }
    return result;
}

void FileOperationService::throwIfCanceled(std::stop_token stopToken)
{
    if (stopToken.stop_requested()) {
        throw FileOperationCanceledError();
    }
}

core::FileOperationResult FileOperationService::convertOneToJpg(
    const core::ImageListItem &image,
    std::stop_token stopToken) const
{
    if (!QFileInfo(image.path).isFile()) {
        return makeResult(
            image.path,
            core::FileOperationStatus::Failed,
            std::nullopt,
            QStringLiteral("source_missing"));
    }
    if (isJpgExtension(image.extension)) {
        return makeResult(
            image.path,
            core::FileOperationStatus::Skipped,
            std::nullopt,
            QStringLiteral("already_jpg"));
    }
    if (image.isAnimated) {
        return makeResult(
            image.path,
            core::FileOperationStatus::Skipped,
            std::nullopt,
            QStringLiteral("animated_unsupported"));
    }
    if (!core::image_format_rules::supportedImageExtension(image.path).has_value()) {
        return makeResult(
            image.path,
            core::FileOperationStatus::Skipped,
            std::nullopt,
            QStringLiteral("unsupported_extension"));
    }

    const QFileInfo sourceInfo(image.path);
    const QString targetPath = QDir(sourceInfo.absolutePath()).filePath(
        sourceInfo.completeBaseName() + QStringLiteral(".jpg"));
    if (QFileInfo::exists(targetPath)) {
        return makeResult(
            image.path,
            core::FileOperationStatus::Skipped,
            targetPath,
            QStringLiteral("target_exists"));
    }

    try {
        m_jpegEncoder(image.path, targetPath, stopToken);
        throwIfCanceled(stopToken);
        return makeResult(image.path, core::FileOperationStatus::Converted, targetPath);
    } catch (const FileOperationCanceledError &) {
        QFile::remove(targetPath);
        throw;
    } catch (const std::exception &exception) {
        QFile::remove(targetPath);
        return makeResult(
            image.path,
            core::FileOperationStatus::Failed,
            targetPath,
            QStringLiteral("conversion_failed"),
            exceptionMessage(exception));
    }
}

core::FileOperationResult FileOperationService::convertOneToWebp(
    const core::ImageListItem &image,
    std::stop_token stopToken) const
{
    if (!QFileInfo(image.path).isFile()) {
        return makeResult(
            image.path,
            core::FileOperationStatus::Failed,
            std::nullopt,
            QStringLiteral("source_missing"));
    }
    if (isJpgExtension(image.extension)) {
        return makeResult(
            image.path,
            core::FileOperationStatus::Skipped,
            std::nullopt,
            QStringLiteral("jpg_source_skipped"));
    }
    if (isWebpExtension(image.extension)) {
        return makeResult(
            image.path,
            core::FileOperationStatus::Skipped,
            std::nullopt,
            QStringLiteral("already_webp"));
    }
    if (image.isAnimated) {
        return makeResult(
            image.path,
            core::FileOperationStatus::Skipped,
            std::nullopt,
            QStringLiteral("animated_unsupported"));
    }
    if (!core::image_format_rules::supportedImageExtension(image.path).has_value()) {
        return makeResult(
            image.path,
            core::FileOperationStatus::Skipped,
            std::nullopt,
            QStringLiteral("unsupported_extension"));
    }

    const QFileInfo sourceInfo(image.path);
    const QString targetPath = QDir(sourceInfo.absolutePath()).filePath(
        sourceInfo.completeBaseName() + QStringLiteral(".webp"));
    if (QFileInfo::exists(targetPath)) {
        return makeResult(
            image.path,
            core::FileOperationStatus::Skipped,
            targetPath,
            QStringLiteral("target_exists"));
    }

    try {
        m_webpEncoder(image.path, targetPath, stopToken);
        throwIfCanceled(stopToken);
        return makeResult(image.path, core::FileOperationStatus::Converted, targetPath);
    } catch (const FileOperationCanceledError &) {
        QFile::remove(targetPath);
        throw;
    } catch (const std::exception &exception) {
        QFile::remove(targetPath);
        return makeResult(
            image.path,
            core::FileOperationStatus::Failed,
            targetPath,
            QStringLiteral("conversion_failed"),
            exceptionMessage(exception));
    }
}

} // namespace piclens::infrastructure
