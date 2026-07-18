#include <piclens/infrastructure/file_operation_service.h>

#include <piclens/core/file_rename_planner.h>
#include <piclens/core/image_format_rules.h>
#include <piclens/core/path_rules.h>
#include <piclens/infrastructure/platform_file_manager.h>

#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QHash>
#include <QImageReader>
#include <QImageWriter>
#include <QSemaphore>
#include <QSet>
#include <QThread>

#include <atomic>
#include <exception>
#include <mutex>
#include <stdexcept>
#include <thread>
#include <utility>
#include <vector>

namespace piclens::infrastructure {
namespace {

constexpr qint64 ConversionBudgetBytes = 512LL * 1024 * 1024;
constexpr qint64 ConversionBudgetUnitBytes = 16LL * 1024 * 1024;
constexpr int ConversionBudgetPermits = static_cast<int>(
    ConversionBudgetBytes / ConversionBudgetUnitBytes);

int recommendedConversionConcurrency(int requested)
{
    if (requested > 0) {
        return std::clamp(requested, 1, 8);
    }
    const int ideal = QThread::idealThreadCount();
    if (ideal <= 0) {
        return 2;
    }
    return std::clamp(ideal - 2, 1, 4);
}

int conversionBudgetPermits(const QString &path)
{
    QImageReader reader(path);
    const QSize size = reader.size();
    if (!size.isValid() || size.isEmpty()) {
        return 4;
    }
    const qint64 width = size.width();
    const qint64 height = size.height();
    const qint64 maximumPixels = ConversionBudgetBytes / 5;
    const qint64 pixels = width > 0 && height > maximumPixels / width
        ? maximumPixels
        : width * height;
    const qint64 estimatedBytes = std::min(ConversionBudgetBytes, pixels * 5);
    return std::clamp(
        static_cast<int>((estimatedBytes + ConversionBudgetUnitBytes - 1)
                         / ConversionBudgetUnitBytes),
        1,
        ConversionBudgetPermits);
}

class ConversionBudgetLease final
{
public:
    ConversionBudgetLease(QSemaphore &budget, int permits, std::stop_token stopToken)
        : m_budget(&budget)
        , m_permits(permits)
    {
        while (!m_budget->tryAcquire(m_permits, 25)) {
            if (stopToken.stop_requested()) {
                throw FileOperationCanceledError();
            }
        }
    }

    ~ConversionBudgetLease()
    {
        m_budget->release(m_permits);
    }

    ConversionBudgetLease(const ConversionBudgetLease &) = delete;
    ConversionBudgetLease &operator=(const ConversionBudgetLease &) = delete;

private:
    QSemaphore *m_budget;
    int m_permits;
};

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

QString conversionTargetPath(const core::ImageListItem &image, bool convertToWebp)
{
    const QFileInfo source(image.path);
    return QDir(source.absolutePath()).filePath(
        source.completeBaseName()
        + (convertToWebp ? QStringLiteral(".webp") : QStringLiteral(".jpg")));
}

QString conversionTargetKey(const core::ImageListItem &image, bool convertToWebp)
{
    return core::path_rules::pathKey(conversionTargetPath(image, convertToWebp));
}

bool mayRequireEncoding(const core::ImageListItem &image, bool convertToWebp)
{
    if (!QFileInfo(image.path).isFile() || image.isAnimated
        || !core::image_format_rules::supportedImageExtension(image.path).has_value()) {
        return false;
    }
    if (convertToWebp ? (isJpgExtension(image.extension) || isWebpExtension(image.extension))
                      : isJpgExtension(image.extension)) {
        return false;
    }
    return !QFileInfo::exists(conversionTargetPath(image, convertToWebp));
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
    TrashHandler trashHandler,
    int maxConcurrentConversions)
    : m_jpegEncoder(std::move(jpegEncoder))
    , m_webpEncoder(std::move(webpEncoder))
    , m_trashHandler(std::move(trashHandler))
    , m_maxConcurrentConversions(recommendedConversionConcurrency(maxConcurrentConversions))
{
    if (!m_jpegEncoder || !m_webpEncoder || !m_trashHandler) {
        throw std::invalid_argument("File operation handlers are required.");
    }
}

core::FileOperationBatchResult FileOperationService::convertVisibleToJpg(
    const QVector<core::ImageListItem> &visibleImages,
    std::stop_token stopToken) const
{
    return convertBatch(visibleImages, stopToken, false);
}

core::FileOperationBatchResult FileOperationService::convertVisibleToWebp(
    const QVector<core::ImageListItem> &visibleImages,
    std::stop_token stopToken) const
{
    return convertBatch(visibleImages, stopToken, true);
}

core::FileOperationBatchResult FileOperationService::convertBatch(
    const QVector<core::ImageListItem> &visibleImages,
    std::stop_token stopToken,
    bool convertToWebp) const
{
    throwIfCanceled(stopToken);
    if (visibleImages.isEmpty()) {
        return {};
    }

    std::vector<std::vector<std::size_t>> targetGroups;
    QHash<QString, qsizetype> groupByTarget;
    groupByTarget.reserve(visibleImages.size());
    for (qsizetype index = 0; index < visibleImages.size(); ++index) {
        const QString key = conversionTargetKey(visibleImages.at(index), convertToWebp);
        auto group = groupByTarget.constFind(key);
        if (group == groupByTarget.cend()) {
            const qsizetype newGroup = static_cast<qsizetype>(targetGroups.size());
            groupByTarget.insert(key, newGroup);
            targetGroups.push_back({});
            group = groupByTarget.constFind(key);
        }
        targetGroups.at(static_cast<std::size_t>(*group)).push_back(
            static_cast<std::size_t>(index));
    }

    const int workerCount = std::min(
        m_maxConcurrentConversions,
        static_cast<int>(targetGroups.size()));
    std::vector<std::vector<std::size_t>> workerGroups(
        static_cast<std::size_t>(workerCount));
    for (std::size_t group = 0; group < targetGroups.size(); ++group) {
        workerGroups.at(group % static_cast<std::size_t>(workerCount)).push_back(group);
    }

    std::vector<std::optional<core::FileOperationResult>> results(
        static_cast<std::size_t>(visibleImages.size()));
    QSemaphore conversionBudget(ConversionBudgetPermits);
    std::atomic_bool aborted = false;
    std::exception_ptr failure;
    std::mutex failureMutex;
    std::vector<std::jthread> workers;
    workers.reserve(static_cast<std::size_t>(workerCount));

    for (int worker = 0; worker < workerCount; ++worker) {
        workers.emplace_back([&, worker] {
            try {
                for (const std::size_t group : workerGroups.at(static_cast<std::size_t>(worker))) {
                    for (const std::size_t index : targetGroups.at(group)) {
                        if (aborted.load(std::memory_order_acquire)) {
                            return;
                        }
                        throwIfCanceled(stopToken);
                        const auto &image = visibleImages.at(static_cast<qsizetype>(index));
                        if (mayRequireEncoding(image, convertToWebp)) {
                            ConversionBudgetLease lease(
                                conversionBudget,
                                conversionBudgetPermits(image.path),
                                stopToken);
                            results.at(index) = convertToWebp
                                ? convertOneToWebp(image, stopToken)
                                : convertOneToJpg(image, stopToken);
                        } else {
                            results.at(index) = convertToWebp
                                ? convertOneToWebp(image, stopToken)
                                : convertOneToJpg(image, stopToken);
                        }
                    }
                }
            } catch (...) {
                if (!aborted.exchange(true, std::memory_order_acq_rel)) {
                    const std::scoped_lock lock(failureMutex);
                    failure = std::current_exception();
                }
            }
        });
    }
    for (auto &worker : workers) {
        worker.join();
    }
    if (failure) {
        std::rethrow_exception(failure);
    }

    core::FileOperationBatchResult batch;
    batch.items.reserve(visibleImages.size());
    for (auto &converted : results) {
        if (!converted.has_value()) {
            throw FileOperationCanceledError();
        }
        batch.items.append(std::move(*converted));
    }
    return batch;
}

core::FileOperationBatchResult FileOperationService::trashSameBasenameExtras(
    const QVector<core::ImageListItem> &visibleImages,
    std::stop_token stopToken) const
{
    QSet<QString> preservedBasenames;
    for (const auto &image : visibleImages) {
        if (isJpgExtension(image.extension) || isWebpExtension(image.extension)) {
            preservedBasenames.insert(basenameKey(image.path));
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
                QStringLiteral("keep_jpg")));
        } else if (isWebpExtension(image.extension)) {
            result.items.append(makeResult(
                image.path,
                core::FileOperationStatus::Skipped,
                std::nullopt,
                QStringLiteral("keep_webp")));
        } else if (!preservedBasenames.contains(basenameKey(image.path))) {
            result.items.append(makeResult(
                image.path,
                core::FileOperationStatus::Skipped,
                std::nullopt,
                QStringLiteral("no_matching_jpg_or_webp")));
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
