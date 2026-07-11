#include <piclens/app/app_controller.h>

#include <QCommandLineOption>
#include <QCommandLineParser>
#include <QDir>
#include <QFontDatabase>
#include <QGuiApplication>
#include <QIcon>
#include <QFile>
#include <QFileInfo>
#include <QElapsedTimer>
#include <QJsonDocument>
#include <QJsonObject>
#include <QQmlApplicationEngine>
#include <QQuickWindow>
#include <QQuickStyle>
#include <QSaveFile>
#include <QTimer>

#include <cstdlib>
#include <memory>

#ifdef Q_OS_WIN
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>
#include <psapi.h>
#endif

namespace {

void loadApplicationFonts(QGuiApplication &application)
{
    const QStringList resources{
        QStringLiteral(":/qt/qml/PicLens/fonts/NotoSansCJKtc-Regular.otf"),
        QStringLiteral(":/qt/qml/PicLens/fonts/NotoSansCJKtc-Medium.otf"),
        QStringLiteral(":/qt/qml/PicLens/fonts/NotoSansCJKtc-Bold.otf"),
    };
    for (const QString &resource : resources) {
        QFontDatabase::addApplicationFont(resource);
    }
    QFont font(QStringLiteral("Noto Sans CJK TC"));
    font.setPixelSize(14);
    application.setFont(font);
}

struct ProcessMemorySample {
    qint64 workingSetBytes = -1;
    qint64 peakWorkingSetBytes = -1;
};

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

bool writePerformanceMetrics(
    const QString &path,
    qint64 elapsedMilliseconds,
    piclens::app::AppController *controller)
{
    const QFileInfo destination(path);
    if (!QDir().mkpath(destination.absolutePath())) {
        return false;
    }
    const ProcessMemorySample memory = processMemorySample();
    const QJsonObject metrics{
        {QStringLiteral("elapsedMilliseconds"), elapsedMilliseconds},
        {QStringLiteral("rowCount"), controller->library()->items()->rowCount()},
        {QStringLiteral("imageCount"), controller->library()->visibleImages().size()},
        {QStringLiteral("activeThumbnailRequests"), controller->thumbnails()->activeRequestCount()},
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

int main(int argc, char *argv[])
{
    QQuickStyle::setStyle(QStringLiteral("Basic"));
    QGuiApplication application(argc, argv);
    application.setOrganizationName(QStringLiteral("PicLens"));
    application.setApplicationName(QStringLiteral("PicLens"));
    application.setApplicationVersion(QStringLiteral(PICLENS_VERSION));
    application.setWindowIcon(QIcon(QStringLiteral(":/qt/qml/PicLens/assets/AppIcon.ico")));
    loadApplicationFonts(application);

    QCommandLineParser parser;
    parser.setApplicationDescription(QStringLiteral("PicLens Qt image browser"));
    parser.addHelpOption();
    parser.addVersionOption();
    QCommandLineOption folderOption(
        QStringList{QStringLiteral("f"), QStringLiteral("folder")},
        QStringLiteral("Open a folder after startup."),
        QStringLiteral("path"));
    QCommandLineOption smokeOption(
        QStringLiteral("smoke-ms"),
        QStringLiteral("Exit after the specified milliseconds (runtime smoke testing)."),
        QStringLiteral("milliseconds"));
    QCommandLineOption dataRootOption(
        QStringLiteral("data-root"),
        QStringLiteral("Use an isolated settings, log, and thumbnail data directory."),
        QStringLiteral("path"));
    QCommandLineOption screenshotOption(
        QStringLiteral("screenshot"),
        QStringLiteral("Capture the first window to a PNG after startup."),
        QStringLiteral("path"));
    QCommandLineOption viewerOption(
        QStringLiteral("viewer"),
        QStringLiteral("Open the specified image in the inline viewer after the library loads."),
        QStringLiteral("path"));
    QCommandLineOption metricsOption(
        QStringLiteral("metrics"),
        QStringLiteral("Write startup/library performance metrics to a JSON file."),
        QStringLiteral("path"));
    QCommandLineOption recursiveOption(
        QStringLiteral("include-subfolders"),
        QStringLiteral("Include descendant folders in the initial library scan."));
    QCommandLineOption searchOption(
        QStringLiteral("search"),
        QStringLiteral("Apply an initial library search query."),
        QStringLiteral("query"));
    QCommandLineOption listViewOption(
        QStringLiteral("list-view"),
        QStringLiteral("Start with the library in list view."));
    QCommandLineOption sidebarClosedOption(
        QStringLiteral("sidebar-closed"),
        QStringLiteral("Start with the folder sidebar collapsed."));
    parser.addOption(folderOption);
    parser.addOption(smokeOption);
    parser.addOption(dataRootOption);
    parser.addOption(screenshotOption);
    parser.addOption(viewerOption);
    parser.addOption(metricsOption);
    parser.addOption(recursiveOption);
    parser.addOption(searchOption);
    parser.addOption(listViewOption);
    parser.addOption(sidebarClosedOption);
    parser.process(application);
    QElapsedTimer performanceTimer;
    performanceTimer.start();

    const QString requestedDataRoot = parser.value(dataRootOption);
    const QString dataRoot = requestedDataRoot.isEmpty()
        ? QString{}
        : QDir::cleanPath(requestedDataRoot);
    std::unique_ptr<piclens::app::AppController> appController;
    if (dataRoot.isEmpty()) {
        appController = std::make_unique<piclens::app::AppController>();
    } else {
        QDir().mkpath(dataRoot);
        appController = std::make_unique<piclens::app::AppController>(
            QDir(dataRoot).filePath(QStringLiteral("piclens-settings.json")),
            QDir(dataRoot).filePath(QStringLiteral("Logs/PicLens.log")),
            QDir(dataRoot).filePath(QStringLiteral("Thumbnails")));
    }
    if (parser.isSet(listViewOption)) {
        appController->setGridViewMode(false);
    }
    if (parser.isSet(sidebarClosedOption)) {
        appController->toggleSidebar();
    }
    appController->library()->setSearchQuery(parser.value(searchOption));
    const QString requestedFolder = parser.value(folderOption);
    const bool requestedRecursive = parser.isSet(recursiveOption);
    if (!requestedFolder.isEmpty()) {
        appController->suppressFolderSelection();
        QObject::connect(
            appController.get(),
            &piclens::app::AppController::initializedChanged,
            appController.get(),
            [controller = appController.get(), requestedFolder, requestedRecursive] {
                if (requestedRecursive) {
                    controller->setIncludeSubfolders(true);
                }
                controller->openFolderFromPicker(requestedFolder);
            },
            Qt::SingleShotConnection);
    }
    const QString metricsPath = parser.value(metricsOption);
    if (!metricsPath.isEmpty()) {
        auto metricsScheduled = std::make_shared<bool>(false);
        const auto scheduleMetrics = [
            controller = appController.get(),
            metricsPath,
            requestedFolder,
            metricsScheduled,
            &performanceTimer,
            &application] {
            if (*metricsScheduled || !controller->initialized() || controller->library()->busy()
                || controller->library()->currentFolderPath().isEmpty()
                || (!requestedFolder.isEmpty()
                    && QDir::cleanPath(controller->library()->currentFolderPath())
                        != QDir::cleanPath(QFileInfo(requestedFolder).absoluteFilePath()))) {
                return;
            }
            *metricsScheduled = true;
            QTimer::singleShot(1'500, &application, [
                controller, metricsPath, &performanceTimer] {
                if (!writePerformanceMetrics(
                        metricsPath,
                        performanceTimer.elapsed(),
                        controller)) {
                    qWarning("Could not write performance metrics.");
                }
            });
        };
        QObject::connect(
            appController.get(),
            &piclens::app::AppController::initializedChanged,
            appController.get(),
            scheduleMetrics);
        QObject::connect(
            appController->library(),
            &piclens::presentation::LibraryController::busyChanged,
            appController.get(),
            scheduleMetrics);
    }
    const QString requestedViewerPath = QDir::cleanPath(parser.value(viewerOption));
    if (!parser.value(viewerOption).isEmpty()) {
        const auto openRequestedViewer = [controller = appController.get(), requestedViewerPath] {
            if (controller->initialized()
                && !controller->library()->busy()
                && controller->library()->containsImagePath(requestedViewerPath)
                && !controller->viewer()->isOpen()) {
                controller->openViewer(requestedViewerPath, false);
            }
        };
        QObject::connect(
            appController.get(),
            &piclens::app::AppController::initializedChanged,
            appController.get(),
            openRequestedViewer);
        QObject::connect(
            appController->library(),
            &piclens::presentation::LibraryController::busyChanged,
            appController.get(),
            openRequestedViewer);
    }

    QQmlApplicationEngine engine;
    engine.setInitialProperties({
        {QStringLiteral("appController"), QVariant::fromValue(appController.get())},
    });
    QObject::connect(
        &engine,
        &QQmlApplicationEngine::objectCreationFailed,
        &application,
        [] { QCoreApplication::exit(EXIT_FAILURE); },
        Qt::QueuedConnection);
    engine.loadFromModule(QStringLiteral("PicLens"), QStringLiteral("Main"));

    const QString screenshotPath = parser.value(screenshotOption);
    if (!screenshotPath.isEmpty()) {
        QTimer::singleShot(1'000, &engine, [&engine, screenshotPath] {
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

    bool smokeOk = false;
    const int smokeMilliseconds = parser.value(smokeOption).toInt(&smokeOk);
    if (smokeOk && smokeMilliseconds >= 0) {
        QTimer::singleShot(smokeMilliseconds, &application, &QCoreApplication::quit);
    }
    return application.exec();
}
