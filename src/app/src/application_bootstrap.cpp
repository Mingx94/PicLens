#include "application_bootstrap.h"

#include "launch_options.h"

#include <piclens/app/app_controller.h>

#include <QCoreApplication>
#include <QDir>
#include <QFontDatabase>
#include <QFontInfo>
#include <QGuiApplication>
#include <QIcon>
#include <QQmlApplicationEngine>
#include <QQuickImageProvider>

#include <cstdlib>

namespace piclens::app {
namespace {

class ThumbnailImageProvider final : public QQuickImageProvider
{
public:
    explicit ThumbnailImageProvider(infrastructure::ThumbnailService *service)
        : QQuickImageProvider(
              QQuickImageProvider::Image,
              QQuickImageProvider::ForceAsynchronousImageLoading)
        , m_service(service)
    {
    }

    QImage requestImage(
        const QString &id,
        QSize *size,
        const QSize &requestedSize) override
    {
        QImage image = m_service ? m_service->cachedImage(id) : QImage{};
        if (size) {
            *size = image.size();
        }
        if (!image.isNull() && requestedSize.isValid()
            && (image.width() > requestedSize.width() || image.height() > requestedSize.height())) {
            image = image.scaled(
                requestedSize,
                Qt::KeepAspectRatio,
                Qt::SmoothTransformation);
        }
        return image;
    }

private:
    infrastructure::ThumbnailService *m_service;
};

void configureApplicationFont(QGuiApplication &application)
{
    QFont font = QFontDatabase::systemFont(QFontDatabase::GeneralFont);
    const QStringList preferredFamilies{
#ifdef Q_OS_WIN
        QStringLiteral("Microsoft JhengHei UI"),
        QStringLiteral("Microsoft JhengHei"),
#elif defined(Q_OS_MACOS)
        QStringLiteral("PingFang TC"),
        QStringLiteral("Heiti TC"),
#else
        QStringLiteral("Noto Sans CJK TC"),
        QStringLiteral("Noto Sans TC"),
        QStringLiteral("WenQuanYi Micro Hei"),
#endif
    };
    for (const QString &family : preferredFamilies) {
        const QFont candidate(family);
        if (QFontInfo(candidate).family().compare(family, Qt::CaseInsensitive) == 0) {
            font.setFamilies({family, font.family()});
            break;
        }
    }
    font.setStyleHint(QFont::SansSerif);
    font.setPixelSize(14);
    application.setFont(font);
}

} // namespace

void configureApplication(QGuiApplication &application)
{
    application.setOrganizationName(QStringLiteral("PicLens"));
    application.setApplicationName(QStringLiteral("PicLens"));
    application.setApplicationVersion(QStringLiteral(PICLENS_VERSION));
    application.setWindowIcon(QIcon(QStringLiteral(":/qt/qml/PicLens/assets/AppIcon.ico")));
    configureApplicationFont(application);
}

std::unique_ptr<AppController> createAppController(const LaunchOptions &options)
{
    if (options.dataRoot.isEmpty()) {
        return std::make_unique<AppController>();
    }

    QDir().mkpath(options.dataRoot);
    return std::make_unique<AppController>(
        QDir(options.dataRoot).filePath(QStringLiteral("piclens-settings.json")),
        QDir(options.dataRoot).filePath(QStringLiteral("Logs/PicLens.log")),
        QDir(options.dataRoot).filePath(QStringLiteral("Thumbnails")));
}

void applyStartupOptions(AppController &controller, const LaunchOptions &options)
{
    if (options.listView) {
        controller.setGridViewMode(false);
    }
    if (options.sidebarClosed) {
        controller.toggleSidebar();
    }
    controller.library()->setSearchQuery(options.searchQuery);

    if (options.folderPath.isEmpty()) {
        return;
    }

    controller.suppressFolderSelection();
    QObject::connect(
        &controller,
        &AppController::initializedChanged,
        &controller,
        [&controller, folderPath = options.folderPath, recursive = options.includeSubfolders] {
            if (recursive) {
                controller.setIncludeSubfolders(true);
            }
            controller.openFolderFromPicker(folderPath);
        },
        Qt::SingleShotConnection);
}

void loadApplicationQml(
    QQmlApplicationEngine &engine,
    AppController &controller,
    QGuiApplication &application)
{
    engine.addImageProvider(
        QStringLiteral("piclens-thumbnails"),
        new ThumbnailImageProvider(controller.thumbnailService()));
    engine.setInitialProperties({
        {QStringLiteral("appController"), QVariant::fromValue(&controller)},
    });
    QObject::connect(
        &engine,
        &QQmlApplicationEngine::objectCreationFailed,
        &application,
        [] { QCoreApplication::exit(EXIT_FAILURE); },
        Qt::QueuedConnection);
    engine.loadFromModule(QStringLiteral("PicLens"), QStringLiteral("Main"));
}

} // namespace piclens::app
