#include "thumbnailprovider.h"

#include <QFutureWatcher>
#include <QImageReader>
#include <QQuickTextureFactory>
#include <QtConcurrentRun>

#include <algorithm>

namespace
{
QString decodePath(const QString &id)
{
    const qsizetype separatorIndex = id.indexOf(QLatin1Char('/'));
    const QString encodedPath = separatorIndex >= 0 ? id.first(separatorIndex) : id;
    return QString::fromUtf8(QByteArray::fromBase64(encodedPath.toLatin1(), QByteArray::Base64UrlEncoding));
}

QImage loadThumbnail(const QString &path, const QSize &requestedSize)
{
    QImageReader reader(path);
    reader.setAutoTransform(true);

    const QSize sourceSize = reader.size();
    const QSize targetSize = requestedSize.isValid() ? requestedSize : QSize(320, 240);
    if (sourceSize.isValid()) {
        QSize decodeSize = sourceSize.scaled(targetSize, Qt::KeepAspectRatioByExpanding);
        if (decodeSize.width() < sourceSize.width() || decodeSize.height() < sourceSize.height()) {
            reader.setScaledSize(decodeSize);
        }
    }

    return reader.read();
}

class ThumbnailResponse final : public QQuickImageResponse
{
public:
    ThumbnailResponse(const QString &path, const QSize &requestedSize, QThreadPool *pool)
    {
        connect(&m_watcher, &QFutureWatcherBase::finished, this, [this] {
            if (!m_watcher.isCanceled()) {
                m_image = m_watcher.result();
            }
            emit finished();
        });

        m_watcher.setFuture(QtConcurrent::run(pool, [path, requestedSize] {
            return loadThumbnail(path, requestedSize);
        }));
    }

    QQuickTextureFactory *textureFactory() const override
    {
        return QQuickTextureFactory::textureFactoryForImage(m_image);
    }

    QString errorString() const override
    {
        return m_image.isNull() ? QStringLiteral("無法解碼縮圖") : QString();
    }

    void cancel() override
    {
        m_watcher.cancel();
    }

private:
    QFutureWatcher<QImage> m_watcher;
    QImage m_image;
};
}

ThumbnailProvider::ThumbnailProvider()
{
    const int idealThreadCount = std::max(1, QThread::idealThreadCount());
    m_pool.setMaxThreadCount(std::min(4, idealThreadCount));
    m_pool.setExpiryTimeout(15'000);
}

QQuickImageResponse *ThumbnailProvider::requestImageResponse(const QString &id, const QSize &requestedSize)
{
    return new ThumbnailResponse(decodePath(id), requestedSize, &m_pool);
}
