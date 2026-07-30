#include "launch_options.h"

#include <QCommandLineOption>
#include <QCommandLineParser>
#include <QCoreApplication>
#include <QDir>

namespace piclens::app {

LaunchOptions parseLaunchOptions(const QCoreApplication &application)
{
    QCommandLineParser parser;
    parser.setApplicationDescription(QStringLiteral("PicLens Qt image browser"));
    parser.addHelpOption();
    parser.addVersionOption();

    const QCommandLineOption folderOption(
        QStringList{QStringLiteral("f"), QStringLiteral("folder")},
        QStringLiteral("Open a folder after startup."),
        QStringLiteral("path"));
    const QCommandLineOption smokeOption(
        QStringLiteral("smoke-ms"),
        QStringLiteral("Exit after the specified milliseconds (runtime smoke testing)."),
        QStringLiteral("milliseconds"));
    const QCommandLineOption dataRootOption(
        QStringLiteral("data-root"),
        QStringLiteral("Use an isolated settings, log, and thumbnail data directory."),
        QStringLiteral("path"));
    const QCommandLineOption screenshotOption(
        QStringLiteral("screenshot"),
        QStringLiteral("Capture the first window to a PNG after startup."),
        QStringLiteral("path"));
    const QCommandLineOption viewerOption(
        QStringLiteral("viewer"),
        QStringLiteral("Open the specified image in the inline viewer after the library loads."),
        QStringLiteral("path"));
    const QCommandLineOption metricsOption(
        QStringLiteral("metrics"),
        QStringLiteral("Write startup/library performance metrics to a JSON file."),
        QStringLiteral("path"));
    const QCommandLineOption performanceScrollOption(
        QStringLiteral("performance-scroll"),
        QStringLiteral("Exercise virtualized gallery scrolling while collecting metrics."));
    const QCommandLineOption recursiveOption(
        QStringLiteral("include-subfolders"),
        QStringLiteral("Include descendant folders in the initial library scan."));
    const QCommandLineOption searchOption(
        QStringLiteral("search"),
        QStringLiteral("Apply an initial library search query."),
        QStringLiteral("query"));
    const QCommandLineOption listViewOption(
        QStringLiteral("list-view"),
        QStringLiteral("Start with the library in list view."));
    const QCommandLineOption sidebarClosedOption(
        QStringLiteral("sidebar-closed"),
        QStringLiteral("Start with the folder sidebar collapsed."));

    parser.addOptions({
        folderOption,
        smokeOption,
        dataRootOption,
        screenshotOption,
        viewerOption,
        metricsOption,
        performanceScrollOption,
        recursiveOption,
        searchOption,
        listViewOption,
        sidebarClosedOption,
    });
    parser.process(application);

    LaunchOptions options{
        .folderPath = parser.value(folderOption),
        .dataRoot = parser.value(dataRootOption),
        .screenshotPath = parser.value(screenshotOption),
        .viewerPath = parser.value(viewerOption),
        .metricsPath = parser.value(metricsOption),
        .searchQuery = parser.value(searchOption),
        .smokeMilliseconds = std::nullopt,
        .performanceScroll = parser.isSet(performanceScrollOption),
        .includeSubfolders = parser.isSet(recursiveOption),
        .listView = parser.isSet(listViewOption),
        .sidebarClosed = parser.isSet(sidebarClosedOption),
    };

    if (!options.dataRoot.isEmpty()) {
        options.dataRoot = QDir::cleanPath(options.dataRoot);
    }
    if (!options.viewerPath.isEmpty()) {
        options.viewerPath = QDir::cleanPath(options.viewerPath);
    }

    bool smokeOk = false;
    const int smokeMilliseconds = parser.value(smokeOption).toInt(&smokeOk);
    if (smokeOk && smokeMilliseconds >= 0) {
        options.smokeMilliseconds = smokeMilliseconds;
    }
    return options;
}

} // namespace piclens::app
