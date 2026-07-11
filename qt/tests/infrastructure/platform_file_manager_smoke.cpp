#include <piclens/infrastructure/platform_file_manager.h>

#include <QCoreApplication>
#include <QFile>
#include <QFileInfo>
#include <QTemporaryDir>

#include <exception>

int main(int argc, char *argv[])
{
    QCoreApplication application(argc, argv);
    QTemporaryDir fixture;
    if (!fixture.isValid()) {
        qCritical("Could not create platform smoke fixture directory.");
        return 2;
    }
    const QString path = fixture.filePath(QStringLiteral("PicLens-platform-smoke.txt"));
    QFile file(path);
    if (!file.open(QIODevice::WriteOnly) || file.write("PicLens platform smoke\n") < 0) {
        qCritical("Could not create platform smoke fixture file.");
        return 3;
    }
    file.close();

    try {
        const piclens::infrastructure::PlatformFileManager manager;
        manager.reveal(path);
        manager.moveToTrash(path);
    } catch (const std::exception &exception) {
        qCritical("Platform file manager smoke failed: %s", exception.what());
        return 4;
    }
    if (QFileInfo::exists(path)) {
        qCritical("Trash operation returned but the source still exists.");
        return 5;
    }
    return 0;
}
