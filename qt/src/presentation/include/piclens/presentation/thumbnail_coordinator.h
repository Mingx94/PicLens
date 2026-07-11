#pragma once

#include <QObject>
#include <QHash>
#include <QList>
#include <QThreadPool>

#include <chrono>
#include <functional>
#include <memory>
#include <optional>
#include <stop_token>

namespace piclens::presentation {

struct ThumbnailLoadResult {
    std::optional<QString> cachePath;
    std::optional<QString> errorDetails;
    bool canceled = false;
};

class ThumbnailCoordinator final : public QObject
{
    Q_OBJECT
    Q_PROPERTY(int requestedSize READ requestedSize NOTIFY requestedSizeChanged)
    Q_PROPERTY(int activeRequestCount READ activeRequestCount NOTIFY activeRequestCountChanged)

public:
    using LoadFunction = std::function<ThumbnailLoadResult(const QString &, int, std::stop_token)>;

    explicit ThumbnailCoordinator(
        LoadFunction load,
        int maxConcurrent = 4,
        std::chrono::milliseconds timeout = std::chrono::seconds(8),
        QObject *parent = nullptr);
    ~ThumbnailCoordinator() override;

    [[nodiscard]] int requestedSize() const;
    [[nodiscard]] int activeRequestCount() const;

    void setRequestedSize(int requestedSize);
    void requestThumbnail(const QString &sourcePath, bool animated);
    void cancel(const QString &sourcePath);
    void cancelAll();

signals:
    void requestedSizeChanged();
    void activeRequestCountChanged();
    void thumbnailReady(const QString &sourcePath, const QString &cachePath, int requestedSize);
    void thumbnailFailed(const QString &sourcePath, const QString &details, int requestedSize);

private:
    struct Request;

    void schedule();
    void startRequest(const std::shared_ptr<Request> &request);
    void releaseLogicalSlot(const std::shared_ptr<Request> &request);
    void removeIfCurrent(const std::shared_ptr<Request> &request);

    LoadFunction m_load;
    int m_requestedSize = 200;
    int m_maxConcurrent;
    int m_activeRequests = 0;
    std::chrono::milliseconds m_timeout;
    quint64 m_generation = 0;
    QHash<QString, std::shared_ptr<Request>> m_requests;
    QList<std::shared_ptr<Request>> m_pending;
    QThreadPool m_workerPool;
};

} // namespace piclens::presentation
