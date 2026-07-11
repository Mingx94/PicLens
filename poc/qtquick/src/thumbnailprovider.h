#pragma once

#include <QQuickAsyncImageProvider>
#include <QThreadPool>

class ThumbnailProvider final : public QQuickAsyncImageProvider
{
public:
    ThumbnailProvider();
    QQuickImageResponse *requestImageResponse(const QString &id, const QSize &requestedSize) override;

private:
    QThreadPool m_pool;
};
