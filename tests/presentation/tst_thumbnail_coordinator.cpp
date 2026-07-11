#include <piclens/presentation/thumbnail_coordinator.h>
#include <piclens/core/settings_rules.h>

#include <QSignalSpy>
#include <QTest>

#include <algorithm>
#include <atomic>
#include <condition_variable>
#include <mutex>
#include <thread>

using namespace piclens::presentation;

namespace {

ThumbnailLoadResult readyResult(const QString &path)
{
    return {
        .cachePath = path,
        .errorDetails = std::nullopt,
        .canceled = false,
    };
}

ThumbnailLoadResult canceledResult()
{
    return {
        .cachePath = std::nullopt,
        .errorDetails = std::nullopt,
        .canceled = true,
    };
}

} // namespace

class ThumbnailCoordinatorTests final : public QObject
{
    Q_OBJECT

private slots:
    void readyResultIsDeliveredWithRequestedSize();
    void animatedAndDuplicateRequestsDoNotScheduleExtraWork();
    void cancellationSuppressesLateResultAndReleasesSlot();
    void sizeChangeSuppressesOldGeneration();
    void timedOutStallsDoNotBlockFifthVisibleRequest();
};

void ThumbnailCoordinatorTests::readyResultIsDeliveredWithRequestedSize()
{
    ThumbnailCoordinator coordinator(
        [](const QString &path, int size, std::stop_token) {
            return ThumbnailLoadResult{
                .cachePath = QStringLiteral("%1-%2.png").arg(path).arg(size),
                .errorDetails = std::nullopt,
                .canceled = false,
            };
        });
    QSignalSpy ready(&coordinator, &ThumbnailCoordinator::thumbnailReady);

    coordinator.requestThumbnail(QStringLiteral("photo.jpg"), false);
    QTRY_COMPARE_WITH_TIMEOUT(ready.count(), 1, 5000);

    QCOMPARE(ready.first().at(0).toString(), QStringLiteral("photo.jpg"));
    const int defaultSize = piclens::core::settings_rules::DefaultThumbnailSize;
    QCOMPARE(
        ready.first().at(1).toString(),
        QStringLiteral("photo.jpg-%1.png").arg(defaultSize));
    QCOMPARE(ready.first().at(2).toInt(), defaultSize);
    QCOMPARE(coordinator.activeRequestCount(), 0);
}

void ThumbnailCoordinatorTests::animatedAndDuplicateRequestsDoNotScheduleExtraWork()
{
    std::atomic_int calls = 0;
    struct State {
        std::mutex mutex;
        std::condition_variable condition;
        bool release = false;
    } state;
    ThumbnailCoordinator coordinator(
        [&](const QString &, int, std::stop_token) {
            ++calls;
            std::unique_lock lock(state.mutex);
            state.condition.wait(lock, [&] { return state.release; });
            return readyResult(QStringLiteral("thumb.png"));
        });

    coordinator.requestThumbnail(QStringLiteral("loop.gif"), true);
    coordinator.requestThumbnail(QStringLiteral("photo.jpg"), false);
    coordinator.requestThumbnail(QStringLiteral("photo.jpg"), false);
    QTRY_COMPARE_WITH_TIMEOUT(calls.load(), 1, 5000);
    {
        const std::scoped_lock lock(state.mutex);
        state.release = true;
    }
    state.condition.notify_all();
    QTRY_COMPARE_WITH_TIMEOUT(coordinator.activeRequestCount(), 0, 5000);
    QCOMPARE(calls.load(), 1);
}

void ThumbnailCoordinatorTests::cancellationSuppressesLateResultAndReleasesSlot()
{
    std::atomic_bool started = false;
    ThumbnailCoordinator coordinator(
        [&](const QString &, int, std::stop_token stopToken) {
            started = true;
            while (!stopToken.stop_requested()) {
                std::this_thread::yield();
            }
            return canceledResult();
        });
    QSignalSpy ready(&coordinator, &ThumbnailCoordinator::thumbnailReady);

    coordinator.requestThumbnail(QStringLiteral("photo.jpg"), false);
    QTRY_VERIFY_WITH_TIMEOUT(started.load(), 5000);
    coordinator.cancel(QStringLiteral("photo.jpg"));

    QTRY_COMPARE_WITH_TIMEOUT(coordinator.activeRequestCount(), 0, 5000);
    QTest::qWait(50);
    QCOMPARE(ready.count(), 0);
}

void ThumbnailCoordinatorTests::sizeChangeSuppressesOldGeneration()
{
    struct State {
        std::mutex mutex;
        std::condition_variable condition;
        int calls = 0;
        bool firstStarted = false;
        bool releaseFirst = false;
    } state;
    ThumbnailCoordinator coordinator(
        [&](const QString &, int size, std::stop_token) {
            int call = 0;
            {
                const std::scoped_lock lock(state.mutex);
                call = ++state.calls;
            }
            if (call == 1) {
                std::unique_lock lock(state.mutex);
                state.firstStarted = true;
                state.condition.notify_all();
                state.condition.wait(lock, [&] { return state.releaseFirst; });
                return readyResult(QStringLiteral("old.png"));
            }
            return readyResult(QStringLiteral("new-%1.png").arg(size));
        },
        4,
        std::chrono::seconds(5));
    QSignalSpy ready(&coordinator, &ThumbnailCoordinator::thumbnailReady);

    coordinator.requestThumbnail(QStringLiteral("photo.jpg"), false);
    QTRY_VERIFY_WITH_TIMEOUT(([&] {
        const std::scoped_lock lock(state.mutex);
        return state.firstStarted;
    })(), 5000);
    coordinator.setRequestedSize(180);
    coordinator.requestThumbnail(QStringLiteral("photo.jpg"), false);
    QTRY_COMPARE_WITH_TIMEOUT(ready.count(), 1, 5000);
    QCOMPARE(ready.first().at(1).toString(), QStringLiteral("new-180.png"));

    {
        const std::scoped_lock lock(state.mutex);
        state.releaseFirst = true;
    }
    state.condition.notify_all();
    QTest::qWait(50);
    QCOMPARE(ready.count(), 1);
}

void ThumbnailCoordinatorTests::timedOutStallsDoNotBlockFifthVisibleRequest()
{
    struct State {
        std::mutex mutex;
        std::condition_variable condition;
        int calls = 0;
        int stalled = 0;
        bool release = false;
    } state;
    ThumbnailCoordinator coordinator(
        [&](const QString &path, int, std::stop_token) {
            int call = 0;
            {
                const std::scoped_lock lock(state.mutex);
                call = ++state.calls;
            }
            if (call <= 4) {
                std::unique_lock lock(state.mutex);
                ++state.stalled;
                state.condition.notify_all();
                state.condition.wait(lock, [&] { return state.release; });
            }
            return readyResult(path + QStringLiteral(".png"));
        },
        4,
        std::chrono::milliseconds(50));
    QSignalSpy ready(&coordinator, &ThumbnailCoordinator::thumbnailReady);
    QSignalSpy failed(&coordinator, &ThumbnailCoordinator::thumbnailFailed);

    for (int index = 1; index <= 5; ++index) {
        coordinator.requestThumbnail(QStringLiteral("image-%1.jpg").arg(index), false);
    }
    QTRY_VERIFY_WITH_TIMEOUT(([&] {
        const std::scoped_lock lock(state.mutex);
        return state.stalled == 4;
    })(), 5000);
    QTRY_VERIFY_WITH_TIMEOUT(([&] {
        return std::any_of(ready.cbegin(), ready.cend(), [](const QList<QVariant> &arguments) {
            return arguments.at(0).toString() == QStringLiteral("image-5.jpg");
        });
    })(), 5000);
    QVERIFY(failed.count() >= 4);

    {
        const std::scoped_lock lock(state.mutex);
        state.release = true;
    }
    state.condition.notify_all();
    QTRY_COMPARE_WITH_TIMEOUT(coordinator.activeRequestCount(), 0, 5000);
}

QTEST_GUILESS_MAIN(ThumbnailCoordinatorTests)

#include "tst_thumbnail_coordinator.moc"
