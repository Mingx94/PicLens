#include <piclens/presentation/thumbnail_coordinator.h>

#include <piclens/core/path_rules.h>
#include <piclens/core/settings_rules.h>

#include <QFutureWatcher>
#include <QTimer>
#include <QtConcurrentRun>

#include <algorithm>
#include <stdexcept>
#include <utility>

namespace piclens::presentation {

struct ThumbnailCoordinator::Request {
    QString sourcePath;
    QString pathKey;
    int requestedSize = 0;
    quint64 generation = 0;
    std::shared_ptr<std::stop_source> stop = std::make_shared<std::stop_source>();
    QTimer *timer = nullptr;
    bool started = false;
    bool canceled = false;
    bool timedOut = false;
    bool logicalSlotReleased = false;
};

ThumbnailCoordinator::ThumbnailCoordinator(
    LoadFunction load,
    int maxConcurrent,
    std::chrono::milliseconds timeout,
    QObject *parent)
    : QObject(parent)
    , m_load(std::move(load))
    , m_maxConcurrent(std::max(1, maxConcurrent))
    , m_timeout(std::max(std::chrono::milliseconds(1), timeout))
{
    if (!m_load) {
        throw std::invalid_argument("Thumbnail loader is required.");
    }
    m_workerPool.setMaxThreadCount(std::max(m_maxConcurrent * 2, m_maxConcurrent + 1));
    m_workerPool.setExpiryTimeout(30'000);
}

ThumbnailCoordinator::~ThumbnailCoordinator()
{
    cancelAll();
    m_workerPool.waitForDone();
}

int ThumbnailCoordinator::requestedSize() const
{
    return m_requestedSize;
}

int ThumbnailCoordinator::activeRequestCount() const
{
    return m_activeRequests;
}

int ThumbnailCoordinator::completedRequestCount() const
{
    return m_completedRequests;
}

int ThumbnailCoordinator::cacheHitCount() const
{
    return m_cacheHits;
}

void ThumbnailCoordinator::setRequestedSize(int requestedSize)
{
    const int normalized = core::settings_rules::normalizeThumbnailSize(requestedSize);
    if (m_requestedSize == normalized) {
        return;
    }
    cancelAll();
    m_requestedSize = normalized;
    emit requestedSizeChanged();
}

void ThumbnailCoordinator::requestThumbnail(const QString &sourcePath, bool animated)
{
    if (animated || sourcePath.trimmed().isEmpty()) {
        return;
    }
    const QString key = core::path_rules::pathKey(sourcePath);
    const auto existing = m_requests.constFind(key);
    if (existing != m_requests.cend()
        && (*existing)->requestedSize == m_requestedSize
        && !(*existing)->canceled
        && !(*existing)->timedOut) {
        return;
    }
    cancel(sourcePath);

    auto request = std::make_shared<Request>();
    request->sourcePath = sourcePath;
    request->pathKey = key;
    request->requestedSize = m_requestedSize;
    request->generation = m_generation;
    m_requests.insert(key, request);
    m_pending.append(request);
    schedule();
}

void ThumbnailCoordinator::cancel(const QString &sourcePath)
{
    const QString key = core::path_rules::pathKey(sourcePath);
    const auto iterator = m_requests.find(key);
    if (iterator == m_requests.end()) {
        return;
    }
    const auto request = *iterator;
    request->canceled = true;
    request->stop->request_stop();
    if (request->timer) {
        request->timer->stop();
        request->timer->deleteLater();
        request->timer = nullptr;
    }
    releaseLogicalSlot(request);
    m_requests.erase(iterator);
    schedule();
}

void ThumbnailCoordinator::cancelAll()
{
    ++m_generation;
    const auto requests = m_requests;
    m_requests.clear();
    m_pending.clear();
    for (const auto &request : requests) {
        request->canceled = true;
        request->stop->request_stop();
        if (request->timer) {
            request->timer->stop();
            request->timer->deleteLater();
            request->timer = nullptr;
        }
        releaseLogicalSlot(request);
    }
}

void ThumbnailCoordinator::schedule()
{
    while (m_activeRequests < m_maxConcurrent && !m_pending.isEmpty()) {
        const auto request = m_pending.takeFirst();
        if (request->canceled || request->generation != m_generation) {
            continue;
        }
        startRequest(request);
    }
}

void ThumbnailCoordinator::startRequest(const std::shared_ptr<Request> &request)
{
    request->started = true;
    ++m_activeRequests;
    emit activeRequestCountChanged();

    request->timer = new QTimer(this);
    request->timer->setSingleShot(true);
    connect(request->timer, &QTimer::timeout, this, [this, request] {
        request->timer->deleteLater();
        request->timer = nullptr;
        if (request->canceled || request->generation != m_generation) {
            return;
        }
        request->timedOut = true;
        request->stop->request_stop();
        removeIfCurrent(request);
        releaseLogicalSlot(request);
        emit thumbnailFailed(
            request->sourcePath,
            QStringLiteral("thumbnail_timeout"),
            request->requestedSize);
        schedule();
    });
    request->timer->start(static_cast<int>(m_timeout.count()));

    const LoadFunction load = m_load;
    auto *watcher = new QFutureWatcher<ThumbnailLoadResult>(this);
    connect(watcher, &QFutureWatcher<ThumbnailLoadResult>::finished, this, [this, watcher, request] {
        const ThumbnailLoadResult result = watcher->result();
        watcher->deleteLater();
        if (request->timer) {
            request->timer->stop();
            request->timer->deleteLater();
            request->timer = nullptr;
        }
        releaseLogicalSlot(request);
        removeIfCurrent(request);
        if (!request->canceled
            && !request->timedOut
            && request->generation == m_generation
            && !result.canceled) {
            if (result.cachePath.has_value()) {
                ++m_completedRequests;
                if (result.cacheHit) {
                    ++m_cacheHits;
                }
                emit statisticsChanged();
                emit thumbnailReady(
                    request->sourcePath,
                    *result.cachePath,
                    request->requestedSize);
            } else if (result.errorDetails.has_value()) {
                emit thumbnailFailed(
                    request->sourcePath,
                    *result.errorDetails,
                    request->requestedSize);
            }
        }
        schedule();
    });
    watcher->setFuture(QtConcurrent::run(&m_workerPool, [load, request] {
        try {
            return load(
                request->sourcePath,
                request->requestedSize,
                request->stop->get_token());
        } catch (const std::exception &exception) {
            return ThumbnailLoadResult{
                .cachePath = std::nullopt,
                .errorDetails = QString::fromUtf8(exception.what()),
                .canceled = request->stop->stop_requested(),
            };
        } catch (...) {
            return ThumbnailLoadResult{
                .cachePath = std::nullopt,
                .errorDetails = QStringLiteral("Unknown thumbnail loader error."),
                .canceled = request->stop->stop_requested(),
            };
        }
    }));
}

void ThumbnailCoordinator::releaseLogicalSlot(const std::shared_ptr<Request> &request)
{
    if (!request->started || request->logicalSlotReleased) {
        return;
    }
    request->logicalSlotReleased = true;
    m_activeRequests = std::max(0, m_activeRequests - 1);
    emit activeRequestCountChanged();
}

void ThumbnailCoordinator::removeIfCurrent(const std::shared_ptr<Request> &request)
{
    const auto iterator = m_requests.find(request->pathKey);
    if (iterator != m_requests.end() && *iterator == request) {
        m_requests.erase(iterator);
    }
}

} // namespace piclens::presentation
