#include <piclens/app/app_controller.h>

#include <QDir>
#include <QImage>
#include <QQmlContext>
#include <QQmlEngine>
#include <QtQuickTest/quicktest.h>
#include <QtQml/QQmlExtensionPlugin>
#include <QQuickStyle>
#include <QTemporaryDir>

#include <memory>

Q_IMPORT_QML_PLUGIN(PicLensQmlPlugin)

class QuickTestSetup final : public QObject
{
    Q_OBJECT

public slots:
    void applicationAvailable()
    {
        QQuickStyle::setStyle(QStringLiteral("Basic"));

        m_workspace = std::make_unique<QTemporaryDir>();
        m_profile = std::make_unique<QTemporaryDir>();
        if (!m_workspace->isValid() || !m_profile->isValid()) {
            qFatal("Failed to create QML test directories.");
        }
        QDir workspace(m_workspace->path());
        for (int index = 1; index <= 80; ++index) {
            const QString name = QStringLiteral("Folder-%1").arg(index, 2, 10, QLatin1Char('0'));
            if (!workspace.mkdir(name)) {
                qFatal("Failed to create QML folder-tree fixture.");
            }
        }
        QImage image(1, 1, QImage::Format_RGB32);
        image.fill(Qt::black);
        for (int index = 1; index <= 12; ++index) {
            const QString name = QStringLiteral("Z-Image-%1.bmp").arg(index, 2, 10, QLatin1Char('0'));
            if (!image.save(workspace.filePath(name), "BMP")) {
                qFatal("Failed to create QML image fixture.");
            }
        }

        m_controller = std::make_unique<piclens::app::AppController>(
            m_profile->filePath(QStringLiteral("settings.json")),
            m_profile->filePath(QStringLiteral("app.log")),
            m_profile->filePath(QStringLiteral("thumbnails")));
        m_controller->openFolderFromPicker(m_workspace->path());
    }

    void qmlEngineAvailable(QQmlEngine *engine)
    {
        engine->rootContext()->setContextProperty(
            QStringLiteral("testAppController"),
            m_controller.get());
    }

private:
    std::unique_ptr<QTemporaryDir> m_workspace;
    std::unique_ptr<QTemporaryDir> m_profile;
    std::unique_ptr<piclens::app::AppController> m_controller;
};

QUICK_TEST_MAIN_WITH_SETUP(piclens_qml_tests, QuickTestSetup)

#include "tst_qml_main.moc"
