#pragma once

#include <QString>
#include <QFileInfo>
#include <QHash>
#include <QCache>
#include <QImage>

#include <mutex>
#include <optional>
#include <stop_token>

namespace piclens::infrastructure {

enum class ThumbnailStatus {
    Ready,
    Unsupported,
    SourceMissing,
    Animated,
    Canceled,
    Failed,
};

struct ThumbnailResult {
    ThumbnailStatus status = ThumbnailStatus::Failed;
    std::optional<QString> cachePath;
    std::optional<QString> message;
    bool cacheHit = false;
};

class ThumbnailService final
{
public:
    static constexpr qint64 DefaultMaxCacheBytes = 512LL * 1024LL * 1024LL;

    ThumbnailService();
    explicit ThumbnailService(
        QString cacheRoot,
        qint64 maxCacheBytes = DefaultMaxCacheBytes);

    [[nodiscard]] const QString &cacheRoot() const;
    [[nodiscard]] QImage cachedImage(const QString &cacheFileName);
    [[nodiscard]] ThumbnailResult getOrCreate(
        const QString &imagePath,
        int requestedSize,
        std::stop_token stopToken = {});

private:
    [[nodiscard]] QString cachePathFor(const QFileInfo &source, int requestedSize) const;
    [[nodiscard]] ThumbnailResult createThumbnail(
        const QFileInfo &source,
        const QString &cachePath,
        int requestedSize,
        std::stop_token stopToken);
    void triggerPrune(const QString &pathToKeep);
    void pruneCache(const QString &pathToKeep);
    void rememberImage(const QString &cachePath, const QImage &image);

    QString m_cacheRoot;
    qint64 m_maxCacheBytes;
    qint64 m_knownCacheBytes = -1;
    QHash<QString, qint64> m_knownCacheFileSizes;
    std::mutex m_pruneMutex;
    QCache<QString, QImage> m_imageCache{128 * 1024};
    std::mutex m_imageCacheMutex;
};

} // namespace piclens::infrastructure
