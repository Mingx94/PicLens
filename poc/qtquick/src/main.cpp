#include "imagemodel.h"
#include "thumbnailprovider.h"

#include <QCommandLineOption>
#include <QCommandLineParser>
#include <QDir>
#include <QGuiApplication>
#include <QQmlApplicationEngine>
#include <QQuickStyle>
#include <QVariant>

int main(int argc, char *argv[])
{
    QGuiApplication app(argc, argv);
    QGuiApplication::setApplicationName(QStringLiteral("PicLens Qt Quick PoC"));
    QGuiApplication::setOrganizationName(QStringLiteral("PicLens"));

    QCommandLineParser parser;
    parser.setApplicationDescription(QStringLiteral("PicLens Qt 6 / Qt Quick migration PoC"));
    parser.addHelpOption();
    const QCommandLineOption folderOption(
        QStringList{QStringLiteral("f"), QStringLiteral("folder")},
        QStringLiteral("啟動後立即掃描指定的圖片資料夾。"),
        QStringLiteral("path"));
    parser.addOption(folderOption);
    parser.process(app);

    QQuickStyle::setStyle(QStringLiteral("Fusion"));

    ImageModel imageModel;
    QQmlApplicationEngine engine;
    engine.setInitialProperties({
        {QStringLiteral("imageModel"), QVariant::fromValue(static_cast<QObject *>(&imageModel))},
    });
    engine.addImageProvider(QStringLiteral("thumbnail"), new ThumbnailProvider());

    QObject::connect(
        &engine,
        &QQmlApplicationEngine::objectCreationFailed,
        &app,
        [] { QCoreApplication::exit(EXIT_FAILURE); },
        Qt::QueuedConnection);

    engine.loadFromModule(QStringLiteral("PicLens.Poc"), QStringLiteral("Main"));

    if (parser.isSet(folderOption)) {
        imageModel.openFolder(QUrl::fromLocalFile(QDir(parser.value(folderOption)).absolutePath()));
    }

    return app.exec();
}
