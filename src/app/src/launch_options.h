#pragma once

#include <QString>

#include <optional>

class QCoreApplication;

namespace piclens::app {

struct LaunchOptions {
    QString folderPath;
    QString dataRoot;
    QString screenshotPath;
    QString viewerPath;
    QString metricsPath;
    QString searchQuery;
    std::optional<int> smokeMilliseconds;
    bool performanceScroll = false;
    bool includeSubfolders = false;
    bool listView = false;
    bool sidebarClosed = false;
};

[[nodiscard]] LaunchOptions parseLaunchOptions(const QCoreApplication &application);

} // namespace piclens::app
