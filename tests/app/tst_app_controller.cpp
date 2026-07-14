#include <piclens/app/app_controller.h>

#include <piclens/infrastructure/json_settings_store.h>

#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QSignalSpy>
#include <QTemporaryDir>
#include <QTest>

using namespace piclens::app;
using namespace piclens::core;
using namespace piclens::infrastructure;

namespace {

QString childPath(const QString &directory, const QString &name)
{
    return QDir(directory).filePath(name);
}

void seedSettings(const QString &settingsPath, const AppSettingsPatch &patch)
{
    JsonSettingsStore store(settingsPath);
    static_cast<void>(store.update(patch));
}

QByteArray onePixelBmp()
{
    return QByteArray::fromHex(
        "424d3a0000000000000036000000280000000100000001000000010018000000000004000000130b0000130b00000000000000000000ff00");
}

void writeFile(const QString &path, const QByteArray &bytes)
{
    QFile file(path);
    QVERIFY(file.open(QIODevice::WriteOnly));
    QCOMPARE(file.write(bytes), bytes.size());
}

AppSettingsPatch lastFolderPatch(const QString &folderPath)
{
    AppSettingsPatch patch;
    patch.lastFolderPath = folderPath;
    patch.hasLastFolderPath = true;
    return patch;
}

void waitForReady(AppController &controller)
{
    QTRY_VERIFY_WITH_TIMEOUT(controller.initialized(), 5000);
    QTRY_VERIFY_WITH_TIMEOUT(!controller.settingsBusy(), 5000);
    QTRY_VERIFY_WITH_TIMEOUT(!controller.library()->busy(), 5000);
    QTRY_VERIFY_WITH_TIMEOUT(!controller.folderTree()->busy(), 5000);
}

} // namespace

class AppControllerTests final : public QObject
{
    Q_OBJECT

private slots:
    void restoresValidLastFolderWithoutPickerRequest();
    void missingLastFolderRequestsPicker();
    void pickerSelectionResetsRootAndPersists();
    void treeNavigationDoesNotReplacePickerStartupFolder();
    void backAndForwardRestoreFolderTreeRootContext();
    void sortAndRecursiveModePersistOffControllerThread();
    void visibleThumbnailRequestUpdatesModelAndSizePersistence();
    void selectedRenameUsesComposedWorkerAndRefreshesLibrary();
    void convertVisiblePreservesOriginalAndRefreshesLibrary();
    void convertVisibleToWebpPreservesOriginalAndSkipsJpg();
    void viewerSnapshotRemainsImmutableAcrossLibraryReload();
    void shellViewStateMatchesLegacyDefaultsAndToggles();
};

void AppControllerTests::restoresValidLastFolderWithoutPickerRequest()
{
    QTemporaryDir data;
    QTemporaryDir workspace;
    QVERIFY(data.isValid());
    QVERIFY(workspace.isValid());
    const QString settingsPath = childPath(data.path(), QStringLiteral("settings.json"));
    seedSettings(settingsPath, lastFolderPatch(workspace.path()));
    AppController controller(settingsPath, childPath(data.path(), QStringLiteral("app.log")));
    QSignalSpy picker(&controller, &AppController::folderSelectionRequired);

    controller.initialize();
    waitForReady(controller);

    QCOMPARE(picker.count(), 0);
    QCOMPARE(controller.library()->currentFolderPath(), QDir::cleanPath(workspace.path()));
    QCOMPARE(controller.library()->navigationRootPath(), QDir::cleanPath(workspace.path()));
    QCOMPARE(controller.folderTree()->rootPath(), QDir::cleanPath(workspace.path()));
}

void AppControllerTests::missingLastFolderRequestsPicker()
{
    QTemporaryDir data;
    QVERIFY(data.isValid());
    AppController controller(
        childPath(data.path(), QStringLiteral("settings.json")),
        childPath(data.path(), QStringLiteral("app.log")));
    QSignalSpy picker(&controller, &AppController::folderSelectionRequired);

    controller.initialize();
    QTRY_VERIFY_WITH_TIMEOUT(controller.initialized(), 5000);

    QCOMPARE(picker.count(), 1);
    QVERIFY(controller.library()->currentFolderPath().isEmpty());
    QCOMPARE(controller.library()->statusText(), QStringLiteral("請選擇資料夾以開始瀏覽。"));
}

void AppControllerTests::pickerSelectionResetsRootAndPersists()
{
    QTemporaryDir data;
    QTemporaryDir workspace;
    QVERIFY(data.isValid());
    QVERIFY(workspace.isValid());
    const QString settingsPath = childPath(data.path(), QStringLiteral("settings.json"));
    AppController controller(settingsPath, childPath(data.path(), QStringLiteral("app.log")));
    controller.initialize();
    QTRY_VERIFY_WITH_TIMEOUT(controller.initialized(), 5000);

    controller.openFolderFromPicker(workspace.path());
    waitForReady(controller);

    QCOMPARE(controller.folderTree()->rootPath(), QDir::cleanPath(workspace.path()));
    const AppSettings persisted = JsonSettingsStore(settingsPath).load();
    QVERIFY(persisted.lastFolderPath.has_value());
    QCOMPARE(*persisted.lastFolderPath, QDir::cleanPath(workspace.path()));
}

void AppControllerTests::treeNavigationDoesNotReplacePickerStartupFolder()
{
    QTemporaryDir data;
    QTemporaryDir workspace;
    QVERIFY(data.isValid());
    QVERIFY(workspace.isValid());
    const QString child = childPath(workspace.path(), QStringLiteral("Child"));
    QVERIFY(QDir().mkpath(child));
    const QString settingsPath = childPath(data.path(), QStringLiteral("settings.json"));
    seedSettings(settingsPath, lastFolderPatch(workspace.path()));
    AppController controller(settingsPath, childPath(data.path(), QStringLiteral("app.log")));
    controller.initialize();
    waitForReady(controller);

    controller.navigateFromTree(child);
    QTRY_VERIFY_WITH_TIMEOUT(!controller.library()->busy(), 5000);

    QCOMPARE(controller.library()->currentFolderPath(), QDir::cleanPath(child));
    QCOMPARE(controller.folderTree()->rootPath(), QDir::cleanPath(workspace.path()));
    const AppSettings persisted = JsonSettingsStore(settingsPath).load();
    QCOMPARE(*persisted.lastFolderPath, QDir::cleanPath(workspace.path()));
}

void AppControllerTests::backAndForwardRestoreFolderTreeRootContext()
{
    QTemporaryDir data;
    QTemporaryDir first;
    QTemporaryDir second;
    QVERIFY(data.isValid());
    QVERIFY(first.isValid());
    QVERIFY(second.isValid());
    const QString firstChild = childPath(first.path(), QStringLiteral("Child"));
    QVERIFY(QDir().mkpath(firstChild));
    const QString settingsPath = childPath(data.path(), QStringLiteral("settings.json"));
    seedSettings(settingsPath, lastFolderPatch(first.path()));
    AppController controller(settingsPath, childPath(data.path(), QStringLiteral("app.log")));
    controller.initialize();
    waitForReady(controller);
    controller.navigateFromTree(firstChild);
    QTRY_VERIFY_WITH_TIMEOUT(!controller.library()->busy(), 5000);
    controller.openFolderFromPicker(second.path());
    waitForReady(controller);

    controller.goBack();
    QTRY_VERIFY_WITH_TIMEOUT(!controller.library()->busy(), 5000);
    QTRY_VERIFY_WITH_TIMEOUT(!controller.folderTree()->busy(), 5000);
    QCOMPARE(controller.library()->currentFolderPath(), QDir::cleanPath(firstChild));
    QCOMPARE(controller.folderTree()->rootPath(), QDir::cleanPath(first.path()));

    controller.goForward();
    QTRY_VERIFY_WITH_TIMEOUT(!controller.library()->busy(), 5000);
    QTRY_VERIFY_WITH_TIMEOUT(!controller.folderTree()->busy(), 5000);
    QCOMPARE(controller.library()->currentFolderPath(), QDir::cleanPath(second.path()));
    QCOMPARE(controller.folderTree()->rootPath(), QDir::cleanPath(second.path()));
}

void AppControllerTests::sortAndRecursiveModePersistOffControllerThread()
{
    QTemporaryDir data;
    QTemporaryDir workspace;
    QVERIFY(data.isValid());
    QVERIFY(workspace.isValid());
    const QString settingsPath = childPath(data.path(), QStringLiteral("settings.json"));
    seedSettings(settingsPath, lastFolderPatch(workspace.path()));
    AppController controller(settingsPath, childPath(data.path(), QStringLiteral("app.log")));
    controller.initialize();
    waitForReady(controller);

    controller.library()->changeSort({
        .key = SortKey::ModifiedAt,
        .direction = SortDirection::Desc,
    });
    controller.library()->setIncludeSubfolders(true);
    QTRY_VERIFY_WITH_TIMEOUT(!controller.settingsBusy(), 5000);
    QTRY_VERIFY_WITH_TIMEOUT(!controller.library()->busy(), 5000);

    const AppSettings persisted = JsonSettingsStore(settingsPath).load();
    QCOMPARE(persisted.sort, SortState({
        .key = SortKey::ModifiedAt,
        .direction = SortDirection::Desc,
    }));
    QVERIFY(persisted.includeSubfolders);
}

void AppControllerTests::visibleThumbnailRequestUpdatesModelAndSizePersistence()
{
    QTemporaryDir data;
    QTemporaryDir workspace;
    QVERIFY(data.isValid());
    QVERIFY(workspace.isValid());
    const QString source = childPath(workspace.path(), QStringLiteral("photo.bmp"));
    const QString settingsPath = childPath(data.path(), QStringLiteral("settings.json"));
    const QString cacheRoot = childPath(data.path(), QStringLiteral("thumbnails"));
    writeFile(source, onePixelBmp());
    seedSettings(settingsPath, lastFolderPatch(workspace.path()));
    AppController controller(
        settingsPath,
        childPath(data.path(), QStringLiteral("app.log")),
        cacheRoot);
    controller.initialize();
    waitForReady(controller);
    QCOMPARE(controller.library()->items()->rowCount(), 1);

    controller.requestThumbnail(source, false);
    QTRY_VERIFY_WITH_TIMEOUT(
        !controller.library()->items()->data(
             controller.library()->items()->index(0),
             piclens::presentation::LibraryItemModel::ThumbnailPathRole)
             .toString()
             .isEmpty(),
        5000);
    const QString firstThumbnail = controller.library()->items()->data(
        controller.library()->items()->index(0),
        piclens::presentation::LibraryItemModel::ThumbnailPathRole).toString();
    QVERIFY(QFileInfo::exists(firstThumbnail));

    controller.setThumbnailSize(181);
    QCOMPARE(controller.thumbnails()->requestedSize(), 180);
    QCOMPARE(
        controller.library()->items()->data(
            controller.library()->items()->index(0),
            piclens::presentation::LibraryItemModel::ThumbnailPathRole).toString(),
        QString{});
    QTRY_VERIFY_WITH_TIMEOUT(!controller.settingsBusy(), 5000);
    QCOMPARE(JsonSettingsStore(settingsPath).load().thumbnailSize, 180);

    controller.requestThumbnail(source, false);
    QTRY_VERIFY_WITH_TIMEOUT(
        !controller.library()->items()->data(
             controller.library()->items()->index(0),
             piclens::presentation::LibraryItemModel::ThumbnailPathRole)
             .toString()
             .isEmpty(),
        5000);
    const QString secondThumbnail = controller.library()->items()->data(
        controller.library()->items()->index(0),
        piclens::presentation::LibraryItemModel::ThumbnailPathRole).toString();
    QVERIFY(secondThumbnail != firstThumbnail);
}

void AppControllerTests::selectedRenameUsesComposedWorkerAndRefreshesLibrary()
{
    QTemporaryDir data;
    QTemporaryDir workspace;
    QVERIFY(data.isValid());
    QVERIFY(workspace.isValid());
    const QString source = childPath(workspace.path(), QStringLiteral("original.bmp"));
    const QString renamed = childPath(workspace.path(), QStringLiteral("renamed.bmp"));
    const QString settingsPath = childPath(data.path(), QStringLiteral("settings.json"));
    writeFile(source, onePixelBmp());
    seedSettings(settingsPath, lastFolderPatch(workspace.path()));
    AppController controller(
        settingsPath,
        childPath(data.path(), QStringLiteral("app.log")),
        childPath(data.path(), QStringLiteral("thumbnails")));
    controller.initialize();
    waitForReady(controller);

    controller.library()->setSelectedPaths({source});
    QVERIFY(controller.fileOperations()->canRename());
    controller.fileOperations()->renameSelected(QStringLiteral("renamed"));
    QTRY_VERIFY_WITH_TIMEOUT(!controller.fileOperations()->busy(), 5000);
    QTRY_VERIFY_WITH_TIMEOUT(!controller.library()->busy(), 5000);

    QVERIFY(!QFileInfo::exists(source));
    QVERIFY(QFileInfo::exists(renamed));
    QCOMPARE(controller.library()->selectedCount(), 0);
    QCOMPARE(controller.library()->items()->rowCount(), 1);
    QCOMPARE(
        controller.library()->items()->data(
            controller.library()->items()->index(0),
            piclens::presentation::LibraryItemModel::NameRole).toString(),
        QStringLiteral("renamed.bmp"));
}

void AppControllerTests::convertVisiblePreservesOriginalAndRefreshesLibrary()
{
    QTemporaryDir data;
    QTemporaryDir workspace;
    QVERIFY(data.isValid());
    QVERIFY(workspace.isValid());
    const QString source = childPath(workspace.path(), QStringLiteral("source.bmp"));
    const QString converted = childPath(workspace.path(), QStringLiteral("source.jpg"));
    const QString settingsPath = childPath(data.path(), QStringLiteral("settings.json"));
    writeFile(source, onePixelBmp());
    seedSettings(settingsPath, lastFolderPatch(workspace.path()));
    AppController controller(
        settingsPath,
        childPath(data.path(), QStringLiteral("app.log")),
        childPath(data.path(), QStringLiteral("thumbnails")));
    controller.initialize();
    waitForReady(controller);

    QCOMPARE(controller.fileOperations()->visibleImageCount(), 1);
    controller.fileOperations()->convertVisible();
    QTRY_VERIFY_WITH_TIMEOUT(!controller.fileOperations()->busy(), 5000);
    QTRY_VERIFY_WITH_TIMEOUT(!controller.library()->busy(), 5000);

    QVERIFY(QFileInfo::exists(source));
    QVERIFY(QFileInfo::exists(converted));
    QCOMPARE(controller.library()->items()->rowCount(), 2);
}

void AppControllerTests::convertVisibleToWebpPreservesOriginalAndSkipsJpg()
{
    QTemporaryDir data;
    QTemporaryDir workspace;
    QVERIFY(data.isValid());
    QVERIFY(workspace.isValid());
    const QString source = childPath(workspace.path(), QStringLiteral("source.bmp"));
    const QString jpg = childPath(workspace.path(), QStringLiteral("existing.jpg"));
    const QString converted = childPath(workspace.path(), QStringLiteral("source.webp"));
    const QString skippedJpgTarget = childPath(workspace.path(), QStringLiteral("existing.webp"));
    const QString settingsPath = childPath(data.path(), QStringLiteral("settings.json"));
    writeFile(source, onePixelBmp());
    writeFile(jpg, QByteArrayLiteral("jpg-source"));
    seedSettings(settingsPath, lastFolderPatch(workspace.path()));
    AppController controller(
        settingsPath,
        childPath(data.path(), QStringLiteral("app.log")),
        childPath(data.path(), QStringLiteral("thumbnails")));
    controller.initialize();
    waitForReady(controller);

    QCOMPARE(controller.fileOperations()->visibleImageCount(), 2);
    controller.fileOperations()->convertVisibleToWebp();
    QTRY_VERIFY_WITH_TIMEOUT(!controller.fileOperations()->busy(), 5000);
    QTRY_VERIFY_WITH_TIMEOUT(!controller.library()->busy(), 5000);

    QVERIFY(QFileInfo::exists(source));
    QVERIFY(QFileInfo::exists(jpg));
    QVERIFY(QFileInfo::exists(converted));
    QVERIFY(!QFileInfo::exists(skippedJpgTarget));
    QCOMPARE(controller.library()->items()->rowCount(), 3);
}

void AppControllerTests::viewerSnapshotRemainsImmutableAcrossLibraryReload()
{
    QTemporaryDir data;
    QTemporaryDir workspace;
    QVERIFY(data.isValid());
    QVERIFY(workspace.isValid());
    const QString first = childPath(workspace.path(), QStringLiteral("first.bmp"));
    const QString second = childPath(workspace.path(), QStringLiteral("second.bmp"));
    const QString third = childPath(workspace.path(), QStringLiteral("third.bmp"));
    const QString settingsPath = childPath(data.path(), QStringLiteral("settings.json"));
    writeFile(first, onePixelBmp());
    writeFile(second, onePixelBmp());
    seedSettings(settingsPath, lastFolderPatch(workspace.path()));
    AppController controller(
        settingsPath,
        childPath(data.path(), QStringLiteral("app.log")),
        childPath(data.path(), QStringLiteral("thumbnails")));
    controller.initialize();
    waitForReady(controller);

    controller.openViewer(first, false);
    QVERIFY(controller.viewer()->isOpen());
    QCOMPARE(controller.viewer()->imageCount(), 2);
    QCOMPARE(controller.viewer()->currentName(), QStringLiteral("first.bmp"));

    writeFile(third, onePixelBmp());
    controller.reload();
    QTRY_VERIFY_WITH_TIMEOUT(!controller.library()->busy(), 5000);
    QCOMPARE(controller.library()->items()->rowCount(), 3);
    QCOMPARE(controller.viewer()->imageCount(), 2);
    QCOMPARE(controller.viewer()->currentName(), QStringLiteral("first.bmp"));

    controller.viewer()->next();
    QCOMPARE(controller.viewer()->currentName(), QStringLiteral("second.bmp"));
    controller.viewer()->close();
    QVERIFY(!controller.viewer()->isOpen());
}

void AppControllerTests::shellViewStateMatchesLegacyDefaultsAndToggles()
{
    QTemporaryDir data;
    QVERIFY(data.isValid());
    AppController controller(
        childPath(data.path(), QStringLiteral("settings.json")),
        childPath(data.path(), QStringLiteral("app.log")));
    QSignalSpy sidebarChanged(&controller, &AppController::sidebarOpenChanged);
    QSignalSpy viewModeChanged(&controller, &AppController::gridViewModeChanged);

    QVERIFY(controller.sidebarOpen());
    QVERIFY(controller.gridViewMode());
    controller.toggleSidebar();
    QVERIFY(!controller.sidebarOpen());
    QCOMPARE(sidebarChanged.count(), 1);
    controller.setGridViewMode(false);
    QVERIFY(!controller.gridViewMode());
    QCOMPARE(viewModeChanged.count(), 1);
    controller.setGridViewMode(false);
    QCOMPARE(viewModeChanged.count(), 1);
    controller.setGridViewMode(true);
    QVERIFY(controller.gridViewMode());
    QCOMPARE(viewModeChanged.count(), 2);
}

QTEST_GUILESS_MAIN(AppControllerTests)

#include "tst_app_controller.moc"
