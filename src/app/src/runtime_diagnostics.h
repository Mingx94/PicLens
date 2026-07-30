#pragma once

class QElapsedTimer;
class QGuiApplication;
class QQmlApplicationEngine;

namespace piclens::app {

class AppController;
struct LaunchOptions;

void installRuntimeDiagnostics(
    QGuiApplication &application,
    AppController &controller,
    QQmlApplicationEngine &engine,
    const LaunchOptions &options,
    QElapsedTimer &performanceTimer);

} // namespace piclens::app
