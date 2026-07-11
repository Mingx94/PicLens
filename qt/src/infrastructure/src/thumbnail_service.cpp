#include <piclens/infrastructure/thumbnail_service.h>

#include <piclens/core/image_format_rules.h>
#include <piclens/core/path_rules.h>
#include <piclens/infrastructure/app_data_paths.h>
#include <piclens/infrastructure/folder_scanner.h>

#include <QCryptographicHash>
#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QImageReader>
#include <QSaveFile>

#include <algorithm>
#include <utility>
#include <vector>

namespace piclens::infrastructure {
namespace {

ThumbnailResult result(
    ThumbnailStatus status,
    std::optional<QString> cachePath = std::nullopt,
    std::optional<QString> message = std::nullopt,
    bool cacheHit = false)
{
    return {
        .status = status,
        .cachePath = std::move(cachePath),
        .message = std::move(message),
        .cacheHit = cacheHit,
    };
}

QString normalizedPathForKey(const QString &path)
{
    QString normalized = QDir::cleanPath(QFileInfo(path).absoluteFilePath());
    return core::path_rules::pathCaseSensitivity() == Qt::CaseInsensitive
        ? normalized.toCaseFolded()
        : normalized;
}

bool validCachedPng(const QString &path)
{
    const QFileInfo info(path);
    if (!info.isFile() || info.size() <= 0) {
        return false;
    }
    QImageReader reader(path, QByteArrayLiteral("png"));
    return reader.canRead() && reader.size().isValid();
}

} // namespace

ThumbnailService::ThumbnailService()
    : ThumbnailService(app_data_paths::thumbnailCacheRoot())
{
}

ThumbnailService::ThumbnailService(QString cacheRoot, qint64 maxCacheBytes)
    : m_cacheRoot(QDir::cleanPath(QFileInfo(std::move(cacheRoot)).absoluteFilePath()))
    , m_maxCacheBytes(std::max<qint64>(0, maxCacheBytes))
{
}

const QString &ThumbnailService::cacheRoot() const
{
    return m_cacheRoot;
}

ThumbnailResult ThumbnailService::getOrCreate(
    const QString &imagePath,
    int requestedSize,
    std::stop_token stopToken)
{
    if (stopToken.stop_requested()) {
        return result(ThumbnailStatus::Canceled);
    }
    if (requestedSize <= 0
        || !core::image_format_rules::supportedImageExtension(imagePath).has_value()) {
        return result(ThumbnailStatus::Unsupported);
    }

    const QFileInfo source(imagePath);
    if (!source.isFile()) {
        return result(ThumbnailStatus::SourceMissing);
    }
    if (FolderScanner::isKnownAnimatedImage(source.absoluteFilePath())) {
        return result(ThumbnailStatus::Animated);
    }

    const QString cachePath = cachePathFor(source, requestedSize);
    if (validCachedPng(cachePath)) {
        return result(ThumbnailStatus::Ready, cachePath, std::nullopt, true);
    }
    if (QFileInfo::exists(cachePath)) {
        QFile::remove(cachePath);
    }
    if (stopToken.stop_requested()) {
        return result(ThumbnailStatus::Canceled);
    }
    if (!QDir().mkpath(m_cacheRoot)) {
        return result(
            ThumbnailStatus::Failed,
            std::nullopt,
            QStringLiteral("Thumbnail cache directory could not be created."));
    }

    ThumbnailResult created = createThumbnail(source, cachePath, requestedSize, stopToken);
    if (created.status == ThumbnailStatus::Ready) {
        triggerPrune(cachePath);
    }
    return created;
}

QString ThumbnailService::cachePathFor(const QFileInfo &source, int requestedSize) const
{
    const QByteArray key = QStringLiteral("v1\n%1\n%2\n%3\n%4")
                               .arg(
                                   normalizedPathForKey(source.absoluteFilePath()),
                                   QString::number(source.lastModified().toMSecsSinceEpoch()),
                                   QString::number(source.size()),
                                   QString::number(requestedSize))
                               .toUtf8();
    const QString hash = QString::fromLatin1(
        QCryptographicHash::hash(key, QCryptographicHash::Sha256).toHex());
    return QDir(m_cacheRoot).filePath(hash + QStringLiteral(".png"));
}

ThumbnailResult ThumbnailService::createThumbnail(
    const QFileInfo &source,
    const QString &cachePath,
    int requestedSize,
    std::stop_token stopToken)
{
    QImageReader reader(source.absoluteFilePath());
    reader.setAutoTransform(true);
    const QSize sourceSize = reader.size();
    if (!sourceSize.isValid() || sourceSize.isEmpty()) {
        return result(ThumbnailStatus::Failed, std::nullopt, reader.errorString());
    }
    const QSize scaledSize = sourceSize.scaled(
        QSize(requestedSize, requestedSize),
        Qt::KeepAspectRatio);
    reader.setScaledSize(scaledSize);
    if (stopToken.stop_requested()) {
        return result(ThumbnailStatus::Canceled);
    }

    QImage image = reader.read();
    if (image.isNull()) {
        return result(ThumbnailStatus::Failed, std::nullopt, reader.errorString());
    }
    if (image.width() > requestedSize || image.height() > requestedSize) {
        image = image.scaled(
            requestedSize,
            requestedSize,
            Qt::KeepAspectRatio,
            Qt::SmoothTransformation);
    }
    if (stopToken.stop_requested()) {
        return result(ThumbnailStatus::Canceled);
    }

    QSaveFile output(cachePath);
    if (!output.open(QIODevice::WriteOnly)) {
        return result(ThumbnailStatus::Failed, std::nullopt, output.errorString());
    }
    if (!image.save(&output, "PNG", 90)) {
        output.cancelWriting();
        return result(
            ThumbnailStatus::Failed,
            std::nullopt,
            QStringLiteral("Thumbnail could not be encoded."));
    }
    if (stopToken.stop_requested()) {
        output.cancelWriting();
        return result(ThumbnailStatus::Canceled);
    }
    if (!output.commit()) {
        return result(ThumbnailStatus::Failed, std::nullopt, output.errorString());
    }
    return result(ThumbnailStatus::Ready, cachePath);
}

void ThumbnailService::triggerPrune(const QString &pathToKeep)
{
    const bool shouldPrune = m_maxCacheBytes <= 1024 * 1024
        || ++m_generatedSinceLastPrune >= 50;
    if (!shouldPrune) {
        return;
    }
    bool expected = false;
    if (!m_pruning.compare_exchange_strong(expected, true)) {
        return;
    }
    m_generatedSinceLastPrune = 0;
    pruneCache(pathToKeep);
    m_pruning = false;
}

void ThumbnailService::pruneCache(const QString &pathToKeep)
{
    const std::scoped_lock lock(m_pruneMutex);
    if (m_maxCacheBytes <= 0 || !QDir(m_cacheRoot).exists()) {
        return;
    }

    const QFileInfoList cacheFiles = QDir(m_cacheRoot).entryInfoList(
        {QStringLiteral("*.png")},
        QDir::Files,
        QDir::Time);
    std::vector<QFileInfo> files(cacheFiles.cbegin(), cacheFiles.cend());
    qint64 totalBytes = 0;
    for (const QFileInfo &file : files) {
        totalBytes += file.size();
    }
    std::sort(files.begin(), files.end(), [](const QFileInfo &left, const QFileInfo &right) {
        return left.lastModified() > right.lastModified();
    });
    for (auto iterator = files.rbegin();
         iterator != files.rend() && totalBytes > m_maxCacheBytes;
         ++iterator) {
        if (core::path_rules::pathEquals(iterator->absoluteFilePath(), pathToKeep)) {
            continue;
        }
        const qint64 bytes = iterator->size();
        if (QFile::remove(iterator->absoluteFilePath())) {
            totalBytes -= bytes;
        }
    }
}

} // namespace piclens::infrastructure
