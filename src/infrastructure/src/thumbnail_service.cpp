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
#include <limits>
#include <utility>

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

QImage ThumbnailService::cachedImage(const QString &cacheFileName)
{
    const QString safeName = QFileInfo(cacheFileName).fileName();
    if (safeName != cacheFileName || !safeName.endsWith(QStringLiteral(".png"))) {
        return {};
    }
    const QString cachePath = QDir(m_cacheRoot).filePath(safeName);
    {
        const std::scoped_lock lock(m_imageCacheMutex);
        if (const QImage *cached = m_imageCache.object(cachePath)) {
            return *cached;
        }
    }
    QImageReader reader(cachePath, QByteArrayLiteral("png"));
    QImage image = reader.read();
    if (!image.isNull()) {
        rememberImage(cachePath, image);
    }
    return image;
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
    rememberImage(cachePath, image);
    return result(ThumbnailStatus::Ready, cachePath);
}

void ThumbnailService::rememberImage(const QString &cachePath, const QImage &image)
{
    if (image.isNull()) {
        return;
    }
    const qint64 costKiB = std::max<qint64>(1, (image.sizeInBytes() + 1023) / 1024);
    const int cost = static_cast<int>(std::min<qint64>(
        costKiB,
        std::numeric_limits<int>::max()));
    const std::scoped_lock lock(m_imageCacheMutex);
    m_imageCache.insert(cachePath, new QImage(image), cost);
}

void ThumbnailService::triggerPrune(const QString &pathToKeep)
{
    pruneCache(pathToKeep);
}

void ThumbnailService::pruneCache(const QString &pathToKeep)
{
    std::unique_lock lock(m_pruneMutex, std::try_to_lock);
    if (!lock.owns_lock()) {
        m_cacheIndexDirty.store(true, std::memory_order_release);
        return;
    }
    if (m_maxCacheBytes <= 0 || !QDir(m_cacheRoot).exists()) {
        return;
    }

    QFileInfoList cacheFiles;
    if (m_knownCacheBytes < 0
        || m_cacheIndexDirty.exchange(false, std::memory_order_acq_rel)) {
        cacheFiles = QDir(m_cacheRoot).entryInfoList(
            {QStringLiteral("*.png")},
            QDir::Files,
            QDir::Time);
        m_knownCacheBytes = 0;
        m_knownCacheFileSizes.clear();
        for (const QFileInfo &file : cacheFiles) {
            m_knownCacheBytes += file.size();
            m_knownCacheFileSizes.insert(file.absoluteFilePath(), file.size());
        }
    } else {
        const qint64 currentSize = QFileInfo(pathToKeep).size();
        const qint64 previousSize = m_knownCacheFileSizes.value(pathToKeep, 0);
        m_knownCacheBytes += currentSize - previousSize;
        m_knownCacheFileSizes.insert(pathToKeep, currentSize);
    }
    if (m_knownCacheBytes <= m_maxCacheBytes) {
        return;
    }
    if (cacheFiles.isEmpty()) {
        cacheFiles = QDir(m_cacheRoot).entryInfoList(
            {QStringLiteral("*.png")},
            QDir::Files,
            QDir::Time);
    }
    const qint64 pruneTargetBytes = m_maxCacheBytes - std::max<qint64>(
        1,
        m_maxCacheBytes / 10);
    for (auto iterator = cacheFiles.crbegin();
         iterator != cacheFiles.crend() && m_knownCacheBytes > pruneTargetBytes;
         ++iterator) {
        if (core::path_rules::pathEquals(iterator->absoluteFilePath(), pathToKeep)) {
            continue;
        }
        const qint64 bytes = iterator->size();
        if (QFile::remove(iterator->absoluteFilePath())) {
            m_knownCacheBytes -= bytes;
            m_knownCacheFileSizes.remove(iterator->absoluteFilePath());
        }
    }
}

} // namespace piclens::infrastructure
