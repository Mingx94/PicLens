#include <piclens/app/app_controller.h>

#include <QDir>
#include <QQmlContext>
#include <QQmlEngine>
#include <QtQuickTest/quicktest.h>
#include <QQuickStyle>
#include <QTemporaryDir>

#include <memory>

class QuickTestSetup final : public QObject
{
    Q_OBJECT

public slots:
    void applicationAvailable()
    {
        QQuickStyle::setStyle(QStringLiteral("Basic"));
        qmlRegisterUncreatableType<piclens::app::AppController>(
            "PicLens",
            1,
            0,
            "AppController",
            QStringLiteral("Created by the test setup."));

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
