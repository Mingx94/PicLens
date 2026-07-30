#include "runtime_diagnostics.h"

#include "launch_options.h"

#include <piclens/app/app_controller.h>

#include <QDir>
#include <QElapsedTimer>
#include <QFile>
#include <QFileInfo>
#include <QGuiApplication>
#include <QJsonDocument>
#include <QJsonObject>
#include <QQmlApplicationEngine>
#include <QQuickWindow>
#include <QSaveFile>
#include <QSGRendererInterface>
#include <QThread>
#include <QTimer>

#include <algorithm>
#include <cmath>
#include <ctime>
#include <memory>

#ifdef Q_OS_WIN
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>
#include <psapi.h>
#endif

namespace piclens::app {
namespace {

struct ProcessMemorySample {
    qint64 workingSetBytes = -1;
    qint64 peakWorkingSetBytes = -1;
};

struct PerformanceMetricsState {
    qint64 libraryReadyMilliseconds = -1;
    qint64 firstThumbnailMilliseconds = -1;
    QElapsedTimer frameTimer;
    bool receivedFirstFrame = false;
    QVector<double> frameIntervalsMilliseconds;
};

QString graphicsApiName()
{
    switch (QQuickWindow::graphicsApi()) {
    case QSGRendererInterface::Unknown:
        return QStringLiteral("unknown");
    case QSGRendererInterface::Software:
        return QStringLiteral("software");
    case QSGRendererInterface::OpenVG:
        return QStringLiteral("openvg");
    case QSGRendererInterface::OpenGL:
        return QStringLiteral("opengl");
    case QSGRendererInterface::Direct3D11:
        return QStringLiteral("direct3d11");
    case QSGRendererInterface::Vulkan:
        return QStringLiteral("vulkan");
    case QSGRendererInterface::Metal:
        return QStringLiteral("metal");
    case QSGRendererInterface::Null:
        return QStringLiteral("null");
    default:
        return QStringLiteral("api-%1").arg(static_cast<int>(QQuickWindow::graphicsApi()));
    }
}

double percentile(QVector<double> values, double fraction)
{
    if (values.isEmpty()) {
        return -1;
    }
    std::sort(values.begin(), values.end());
    const int index = std::clamp(
        static_cast<int>(std::ceil(fraction * values.size())) - 1,
        0,
        static_cast<int>(values.size()) - 1);
    return values.at(index);
}

ProcessMemorySample processMemorySample()
{
#ifdef Q_OS_WIN
    PROCESS_MEMORY_COUNTERS counters{};
    counters.cb = sizeof(counters);
    if (GetProcessMemoryInfo(GetCurrentProcess(), &counters, sizeof(counters))) {
        return {
            .workingSetBytes = static_cast<qint64>(counters.WorkingSetSize),
            .peakWorkingSetBytes = static_cast<qint64>(counters.PeakWorkingSetSize),
        };
    }
#elif defined(Q_OS_LINUX)
    QFile status(QStringLiteral("/proc/self/status"));
    if (status.open(QIODevice::ReadOnly)) {
        qint64 workingSet = -1;
        qint64 peakWorkingSet = -1;
        while (!status.atEnd()) {
            const QByteArray line = status.readLine();
            const QList<QByteArray> parts = line.simplified().split(' ');
            if (parts.size() >= 2 && parts.at(0) == "VmRSS:") {
                workingSet = parts.at(1).toLongLong() * 1024;
            } else if (parts.size() >= 2 && parts.at(0) == "VmHWM:") {
                peakWorkingSet = parts.at(1).toLongLong() * 1024;
            }
        }
        return {.workingSetBytes = workingSet, .peakWorkingSetBytes = peakWorkingSet};
    }
#endif
    return {};
}

double processCpuMilliseconds()
{
    return static_cast<double>(std::clock()) * 1'000.0 / CLOCKS_PER_SEC;
}

bool writePerformanceMetrics(
    const QString &path,
    qint64 elapsedMilliseconds,
    AppController *controller,
    const PerformanceMetricsState &timing)
{
    const QFileInfo destination(path);
    if (!QDir().mkpath(destination.absolutePath())) {
        return false;
    }
    const ProcessMemorySample memory = processMemorySample();
    const double cpuMilliseconds = processCpuMilliseconds();
    const int logicalProcessorCount = std::max(1, QThread::idealThreadCount());
    const double elapsedSeconds = std::max(0.001, elapsedMilliseconds / 1'000.0);
    const int completedThumbnails = controller->thumbnails()->completedRequestCount();
    const QJsonObject metrics{
        {QStringLiteral("elapsedMilliseconds"), elapsedMilliseconds},
        {QStringLiteral("libraryReadyMilliseconds"), timing.libraryReadyMilliseconds},
        {QStringLiteral("firstThumbnailMilliseconds"), timing.firstThumbnailMilliseconds},
        {QStringLiteral("renderFrameSampleCount"), timing.frameIntervalsMilliseconds.size()},
        {QStringLiteral("renderFrameIntervalP95Milliseconds"),
         percentile(timing.frameIntervalsMilliseconds, 0.95)},
        {QStringLiteral("renderFrameIntervalP99Milliseconds"),
         percentile(timing.frameIntervalsMilliseconds, 0.99)},
        {QStringLiteral("graphicsApi"), graphicsApiName()},
        {QStringLiteral("processCpuMilliseconds"), cpuMilliseconds},
        {QStringLiteral("logicalProcessorCount"), logicalProcessorCount},
        {QStringLiteral("averageCpuUtilizationPercent"),
         cpuMilliseconds / (elapsedSeconds * 1'000.0) / logicalProcessorCount * 100.0},
        {QStringLiteral("rowCount"), controller->library()->items()->rowCount()},
        {QStringLiteral("imageCount"), controller->library()->visibleImages().size()},
        {QStringLiteral("activeThumbnailRequests"), controller->thumbnails()->activeRequestCount()},
        {QStringLiteral("completedThumbnailRequests"), completedThumbnails},
        {QStringLiteral("thumbnailThroughputPerSecond"), completedThumbnails / elapsedSeconds},
        {QStringLiteral("maxConcurrentThumbnailRequests"),
         controller->thumbnails()->maxConcurrentRequestCount()},
        {QStringLiteral("thumbnailCacheHits"), controller->thumbnails()->cacheHitCount()},
        {QStringLiteral("includeSubfolders"), controller->library()->includeSubfolders()},
        {QStringLiteral("sortKey"), controller->library()->sortKey()},
        {QStringLiteral("sortDirection"), controller->library()->sortDirection()},
        {QStringLiteral("thumbnailSize"), controller->thumbnails()->requestedSize()},
        {QStringLiteral("workingSetBytes"), memory.workingSetBytes},
        {QStringLiteral("peakWorkingSetBytes"), memory.peakWorkingSetBytes},
        {QStringLiteral("folderPath"), controller->library()->currentFolderPath()},
    };
    QSaveFile output(destination.absoluteFilePath());
    if (!output.open(QIODevice::WriteOnly)) {
        return false;
    }
    const QByteArray json = QJsonDocument(metrics).toJson(QJsonDocument::Indented);
    return output.write(json) == json.size() && output.commit();
}

} // namespace

void installRuntimeDiagnostics(
    QGuiApplication &application,
    AppController &controller,
    QQmlApplicationEngine &engine,
    const LaunchOptions &options,
    QElapsedTimer &performanceTimer)
{
    auto performanceState = std::make_shared<PerformanceMetricsState>();
    if (!options.metricsPath.isEmpty()) {
        auto metricsScheduled = std::make_shared<bool>(false);
        QObject::connect(
            controller.thumbnails(),
            &presentation::ThumbnailCoordinator::thumbnailReady,
            &controller,
            [performanceState, &performanceTimer](const QString &, const QString &, int) {
                if (performanceState->firstThumbnailMilliseconds < 0) {
                    performanceState->firstThumbnailMilliseconds = performanceTimer.elapsed();
                }
            });
        const auto scheduleMetrics = [
            &controller,
            metricsPath = options.metricsPath,
            requestedFolder = options.folderPath,
            metricsScheduled,
            performanceState,
            &performanceTimer,
            &application] {
            if (*metricsScheduled || !controller.initialized() || controller.library()->busy()
                || controller.library()->currentFolderPath().isEmpty()
                || (!requestedFolder.isEmpty()
                    && QDir::cleanPath(controller.library()->currentFolderPath())
                        != QDir::cleanPath(QFileInfo(requestedFolder).absoluteFilePath()))) {
                return;
            }
            *metricsScheduled = true;
            performanceState->libraryReadyMilliseconds = performanceTimer.elapsed();
            QTimer::singleShot(1'500, &application, [
                &controller, metricsPath, performanceState, &performanceTimer] {
                if (!writePerformanceMetrics(
                        metricsPath,
                        performanceTimer.elapsed(),
                        &controller,
                        *performanceState)) {
                    qWarning("Could not write performance metrics.");
                }
            });
        };
        QObject::connect(
            &controller,
            &AppController::initializedChanged,
            &controller,
            scheduleMetrics);
        QObject::connect(
            controller.library(),
            &presentation::LibraryController::busyChanged,
            &controller,
            scheduleMetrics);
    }

    if (!options.viewerPath.isEmpty()) {
        const auto openRequestedViewer = [&controller, requestedViewerPath = options.viewerPath] {
            if (controller.initialized()
                && !controller.library()->busy()
                && controller.library()->containsImagePath(requestedViewerPath)
                && !controller.viewer()->isOpen()) {
                controller.openViewer(requestedViewerPath, false);
            }
        };
        QObject::connect(
            &controller,
            &AppController::initializedChanged,
            &controller,
            openRequestedViewer);
        QObject::connect(
            controller.library(),
            &presentation::LibraryController::busyChanged,
            &controller,
            openRequestedViewer);
    }

    if (options.performanceScroll && !engine.rootObjects().isEmpty()) {
        QObject *rootObject = engine.rootObjects().constFirst();
        auto exerciseStarted = std::make_shared<bool>(false);
        const auto exerciseGallery = [&controller, rootObject, exerciseStarted, &application] {
            if (*exerciseStarted || !controller.initialized() || controller.library()->busy()
                || controller.library()->items()->rowCount() == 0) {
                return;
            }
            *exerciseStarted = true;
            QTimer::singleShot(100, &application, [rootObject] {
                QMetaObject::invokeMethod(rootObject, "runPerformanceExercise");
            });
        };
        QObject::connect(
            &controller,
            &AppController::initializedChanged,
            &controller,
            exerciseGallery);
        QObject::connect(
            controller.library(),
            &presentation::LibraryController::busyChanged,
            &controller,
            exerciseGallery);
        exerciseGallery();
    }

    if (!options.metricsPath.isEmpty() && !engine.rootObjects().isEmpty()) {
        if (auto *window = qobject_cast<QQuickWindow *>(engine.rootObjects().constFirst())) {
            performanceState->frameTimer.start();
            QObject::connect(
                window,
                &QQuickWindow::frameSwapped,
                &controller,
                [performanceState] {
                    const double interval = performanceState->frameTimer.nsecsElapsed() / 1'000'000.0;
                    performanceState->frameTimer.restart();
                    if (performanceState->receivedFirstFrame) {
                        performanceState->frameIntervalsMilliseconds.append(interval);
                    } else {
                        performanceState->receivedFirstFrame = true;
                    }
                });
        }
    }

    if (!options.screenshotPath.isEmpty()) {
        QTimer::singleShot(1'000, &engine, [&engine, screenshotPath = options.screenshotPath] {
            if (engine.rootObjects().isEmpty()) {
                return;
            }
            auto *window = qobject_cast<QQuickWindow *>(engine.rootObjects().constFirst());
            if (!window) {
                return;
            }
            const QFileInfo destination(screenshotPath);
            QDir().mkpath(destination.absolutePath());
            window->grabWindow().save(destination.absoluteFilePath(), "PNG");
        });
    }

    if (options.smokeMilliseconds.has_value()) {
        QTimer::singleShot(*options.smokeMilliseconds, &application, &QCoreApplication::quit);
    }
}

} // namespace piclens::app
