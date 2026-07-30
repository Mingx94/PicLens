#pragma once

#include <memory>

class QGuiApplication;
class QQmlApplicationEngine;

namespace piclens::app {

class AppController;
struct LaunchOptions;

void configureApplication(QGuiApplication &application);
[[nodiscard]] std::unique_ptr<AppController> createAppController(const LaunchOptions &options);
void applyStartupOptions(AppController &controller, const LaunchOptions &options);
void loadApplicationQml(
    QQmlApplicationEngine &engine,
    AppController &controller,
    QGuiApplication &application);

} // namespace piclens::app
