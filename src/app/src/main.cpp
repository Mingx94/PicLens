#include "application_bootstrap.h"
#include "launch_options.h"
#include "runtime_diagnostics.h"

#include <piclens/app/app_controller.h>

#include <QElapsedTimer>
#include <QGuiApplication>
#include <QQmlApplicationEngine>
#include <QQuickStyle>
#include <QtQml/QQmlExtensionPlugin>

Q_IMPORT_QML_PLUGIN(PicLensQmlPlugin)

int main(int argc, char *argv[])
{
    QQuickStyle::setStyle(QStringLiteral("Basic"));
    QGuiApplication application(argc, argv);
    piclens::app::configureApplication(application);
    const piclens::app::LaunchOptions options = piclens::app::parseLaunchOptions(application);

    QElapsedTimer performanceTimer;
    performanceTimer.start();

    auto appController = piclens::app::createAppController(options);
    piclens::app::applyStartupOptions(*appController, options);

    QQmlApplicationEngine engine;
    piclens::app::loadApplicationQml(engine, *appController, application);
    piclens::app::installRuntimeDiagnostics(
        application,
        *appController,
        engine,
        options,
        performanceTimer);

    return application.exec();
}
