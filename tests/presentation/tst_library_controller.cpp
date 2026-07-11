#include <piclens/presentation/library_controller.h>
#include <piclens/core/path_rules.h>

#include <QAbstractItemModel>
#include <QSignalSpy>
#include <QTest>
#include <QThread>

#include <atomic>
#include <condition_variable>
#include <mutex>
#include <stdexcept>
#include <thread>

using namespace piclens::core;
using namespace piclens::presentation;

namespace {

ImageListItem imageItem(
    const QString &folder,
    const QString &name,
    qint64 modifiedAtMs = 0)
{
    return {
        .path = folder + QLatin1Char('/') + name,
        .name = name,
        .extension = QStringLiteral("jpg"),
        .modifiedAtMs = modifiedAtMs,
        .sizeBytes = 1024,
        .isAnimated = false,
    };
}

FolderListItem folderItem(const QString &folder, const QString &name)
{
    return {
        .path = folder + QLatin1Char('/') + name,
        .name = name,
        .modifiedAtMs = std::nullopt,
    };
}

LibraryController::ResolveFolderFunction resolver()
{
    return [](const QString &path) -> std::optional<QString> {
        const QString normalized = path.trimmed();
        return normalized.isEmpty() || normalized == QStringLiteral("invalid")
            ? std::nullopt
            : std::optional<QString>{normalized};
    };
}

QString modelName(const LibraryItemModel *model, int row)
{
    return model->data(model->index(row), LibraryItemModel::NameRole).toString();
}

} // namespace

class LibraryControllerTests final : public QObject
{
    Q_OBJECT

private slots:
    void navigationHistoryUsesOneStateOwner();
    void sortReordersWithoutRescanningAndClearsSelection();
    void recursiveModePersistsReloadsAndClearsSelection();
    void staleScanCannotOverwriteNewerFolder();
    void sortChangeDuringScanRejectsOldOrdering();
    void largeLibraryUsesSingleModelResetOffGuiThread();
    void reloadAndFileOperationRefreshClearSelection();
    void scanFailureUsesTraditionalChineseErrorState();
    void invalidFolderDoesNotMutateHistory();
    void modelExposesStableRoles();
    void modelUpdatesOnlyChangedSelectionRows();
    void searchAndSortPreserveLoadedThumbnails();
    void selectionGesturesUseOneOrderedImageOwner();
    void viewerSnapshotUsesImageOrderAndPreferredSelection();
    void searchFiltersNameAndPathWithoutRescanning();
};

void LibraryControllerTests::navigationHistoryUsesOneStateOwner()
{
    LibraryController controller(
        [](const ListQuery &query, std::stop_token) {
            return QVector<ListItem>{imageItem(query.folderPath, query.folderPath + QStringLiteral(".jpg"))};
        },
        resolver());

    controller.navigateToFolder(QStringLiteral("A"), true);
    QTRY_VERIFY_WITH_TIMEOUT(!controller.busy(), 5000);
    controller.navigateToFolder(QStringLiteral("B"));
    QTRY_VERIFY_WITH_TIMEOUT(!controller.busy(), 5000);
    QCOMPARE(controller.currentFolderPath(), QStringLiteral("B"));
    QVERIFY(controller.canGoBack());
    QVERIFY(!controller.canGoForward());

    controller.goBack();
    QTRY_VERIFY_WITH_TIMEOUT(!controller.busy(), 5000);
    QCOMPARE(controller.currentFolderPath(), QStringLiteral("A"));
    QCOMPARE(modelName(controller.items(), 0), QStringLiteral("A.jpg"));
    QVERIFY(controller.canGoForward());

    controller.goForward();
    QTRY_VERIFY_WITH_TIMEOUT(!controller.busy(), 5000);
    QCOMPARE(controller.currentFolderPath(), QStringLiteral("B"));
    QCOMPARE(modelName(controller.items(), 0), QStringLiteral("B.jpg"));
}

void LibraryControllerTests::sortReordersWithoutRescanningAndClearsSelection()
{
    std::atomic_int scans = 0;
    LibraryController controller(
        [&](const ListQuery &query, std::stop_token) {
            ++scans;
            return QVector<ListItem>{
                imageItem(query.folderPath, QStringLiteral("older.jpg"), 100),
                imageItem(query.folderPath, QStringLiteral("newer.jpg"), 200),
            };
        },
        resolver());
    QSignalSpy persistence(&controller, &LibraryController::sortPersistenceRequested);

    controller.navigateToFolder(QStringLiteral("gallery"), true);
    QTRY_VERIFY_WITH_TIMEOUT(!controller.busy(), 5000);
    controller.setSelectedPaths({QStringLiteral("gallery/older.jpg")});
    QCOMPARE(controller.selectedCount(), 1);

    controller.changeSort({.key = SortKey::ModifiedAt, .direction = SortDirection::Desc});

    QCOMPARE(scans.load(), 1);
    QCOMPARE(controller.selectedCount(), 0);
    QCOMPARE(modelName(controller.items(), 0), QStringLiteral("newer.jpg"));
    QCOMPARE(controller.sortLabel(), QStringLiteral("修改時間最新到最舊"));
    QCOMPARE(persistence.count(), 1);
}

void LibraryControllerTests::recursiveModePersistsReloadsAndClearsSelection()
{
    std::atomic_int scans = 0;
    std::atomic_bool recursive = false;
    LibraryController controller(
        [&](const ListQuery &query, std::stop_token) {
            ++scans;
            recursive = query.includeSubfolders;
            return QVector<ListItem>{imageItem(query.folderPath, QStringLiteral("photo.jpg"))};
        },
        resolver());
    QSignalSpy persistence(&controller, &LibraryController::includeSubfoldersPersistenceRequested);

    controller.navigateToFolder(QStringLiteral("gallery"), true);
    QTRY_VERIFY_WITH_TIMEOUT(!controller.busy(), 5000);
    controller.setSelectedPaths({QStringLiteral("gallery/photo.jpg")});
    controller.setIncludeSubfolders(true);
    QTRY_VERIFY_WITH_TIMEOUT(!controller.busy(), 5000);

    QCOMPARE(scans.load(), 2);
    QVERIFY(recursive.load());
    QCOMPARE(controller.selectedCount(), 0);
    QCOMPARE(controller.recursiveModeLabel(), QStringLiteral("含子資料夾"));
    QCOMPARE(persistence.count(), 1);
}

void LibraryControllerTests::staleScanCannotOverwriteNewerFolder()
{
    struct State {
        std::mutex mutex;
        std::condition_variable condition;
        bool firstStarted = false;
        bool releaseFirst = false;
        bool firstFinished = false;
    } state;

    LibraryController controller(
        [&](const ListQuery &query, std::stop_token) {
            if (query.folderPath == QStringLiteral("first")) {
                std::unique_lock lock(state.mutex);
                state.firstStarted = true;
                state.condition.notify_all();
                state.condition.wait(lock, [&] { return state.releaseFirst; });
                state.firstFinished = true;
                return QVector<ListItem>{imageItem(query.folderPath, QStringLiteral("first.jpg"))};
            }
            return QVector<ListItem>{imageItem(query.folderPath, QStringLiteral("second.jpg"))};
        },
        resolver());

    controller.navigateToFolder(QStringLiteral("first"), true);
    QTRY_VERIFY_WITH_TIMEOUT(([&] {
        const std::scoped_lock lock(state.mutex);
        return state.firstStarted;
    })(), 5000);
    controller.navigateToFolder(QStringLiteral("second"));
    QTRY_VERIFY_WITH_TIMEOUT(!controller.busy(), 5000);
    QCOMPARE(modelName(controller.items(), 0), QStringLiteral("second.jpg"));

    {
        const std::scoped_lock lock(state.mutex);
        state.releaseFirst = true;
    }
    state.condition.notify_all();
    QTRY_VERIFY_WITH_TIMEOUT(([&] {
        const std::scoped_lock lock(state.mutex);
        return state.firstFinished;
    })(), 5000);
    QTest::qWait(50);

    QCOMPARE(controller.currentFolderPath(), QStringLiteral("second"));
    QCOMPARE(modelName(controller.items(), 0), QStringLiteral("second.jpg"));
}

void LibraryControllerTests::sortChangeDuringScanRejectsOldOrdering()
{
    struct State {
        std::mutex mutex;
        std::condition_variable condition;
        int calls = 0;
        bool firstStarted = false;
        bool releaseFirst = false;
        bool firstFinished = false;
    } state;

    LibraryController controller(
        [&](const ListQuery &query, std::stop_token) {
            int call = 0;
            {
                const std::scoped_lock lock(state.mutex);
                call = ++state.calls;
            }
            if (call == 1) {
                std::unique_lock lock(state.mutex);
                state.firstStarted = true;
                state.condition.notify_all();
                state.condition.wait(lock, [&] { return state.releaseFirst; });
                state.firstFinished = true;
                return QVector<ListItem>{
                    imageItem(query.folderPath, QStringLiteral("older.jpg"), 100),
                    imageItem(query.folderPath, QStringLiteral("newer.jpg"), 200),
                };
            }
            return QVector<ListItem>{
                imageItem(query.folderPath, QStringLiteral("newer.jpg"), 200),
                imageItem(query.folderPath, QStringLiteral("older.jpg"), 100),
            };
        },
        resolver());

    controller.navigateToFolder(QStringLiteral("gallery"), true);
    QTRY_VERIFY_WITH_TIMEOUT(([&] {
        const std::scoped_lock lock(state.mutex);
        return state.firstStarted;
    })(), 5000);
    controller.changeSort({.key = SortKey::ModifiedAt, .direction = SortDirection::Desc});
    QTRY_VERIFY_WITH_TIMEOUT(!controller.busy(), 5000);
    QCOMPARE(modelName(controller.items(), 0), QStringLiteral("newer.jpg"));

    {
        const std::scoped_lock lock(state.mutex);
        state.releaseFirst = true;
    }
    state.condition.notify_all();
    QTRY_VERIFY_WITH_TIMEOUT(([&] {
        const std::scoped_lock lock(state.mutex);
        return state.firstFinished;
    })(), 5000);
    QTest::qWait(50);

    QCOMPARE(modelName(controller.items(), 0), QStringLiteral("newer.jpg"));
}

void LibraryControllerTests::largeLibraryUsesSingleModelResetOffGuiThread()
{
    std::thread::id scanThread;
    const std::thread::id guiThread = std::this_thread::get_id();
    LibraryController controller(
        [&](const ListQuery &query, std::stop_token) {
            scanThread = std::this_thread::get_id();
            QVector<ListItem> items;
            items.reserve(10'000);
            for (int index = 1; index <= 10'000; ++index) {
                items.append(imageItem(
                    query.folderPath,
                    QStringLiteral("image-%1.jpg").arg(index, 5, 10, QLatin1Char('0')),
                    index));
            }
            return items;
        },
        resolver());
    int rowCountWhenReadySignaled = -1;
    connect(&controller, &LibraryController::busyChanged, &controller, [&] {
        if (!controller.busy()) {
            rowCountWhenReadySignaled = controller.items()->rowCount();
        }
    });
    QSignalSpy reset(controller.items(), &QAbstractItemModel::modelReset);

    controller.navigateToFolder(QStringLiteral("large"), true);
    QTRY_VERIFY_WITH_TIMEOUT(!controller.busy(), 5000);

    QCOMPARE(controller.items()->rowCount(), 10'000);
    QCOMPARE(rowCountWhenReadySignaled, 10'000);
    QCOMPARE(reset.count(), 1);
    QVERIFY(scanThread != guiThread);

    controller.setSearchQuery(QStringLiteral("09999"));
    QCOMPARE(controller.items()->rowCount(), 1);
    QCOMPARE(modelName(controller.items(), 0), QStringLiteral("image-09999.jpg"));
    QCOMPARE(reset.count(), 2);
    controller.setSearchQuery({});
    QCOMPARE(controller.items()->rowCount(), 10'000);
    QCOMPARE(reset.count(), 3);
}

void LibraryControllerTests::reloadAndFileOperationRefreshClearSelection()
{
    std::atomic_int scans = 0;
    LibraryController controller(
        [&](const ListQuery &query, std::stop_token) {
            ++scans;
            return QVector<ListItem>{imageItem(query.folderPath, QStringLiteral("photo.jpg"))};
        },
        resolver());

    controller.navigateToFolder(QStringLiteral("gallery"), true);
    QTRY_VERIFY_WITH_TIMEOUT(!controller.busy(), 5000);
    controller.setSelectedPaths({QStringLiteral("gallery/photo.jpg")});
    controller.reload();
    QCOMPARE(controller.selectedCount(), 0);
    QTRY_VERIFY_WITH_TIMEOUT(!controller.busy(), 5000);

    controller.setSelectedPaths({QStringLiteral("gallery/photo.jpg")});
    controller.refreshAfterFileOperation();
    QCOMPARE(controller.selectedCount(), 0);
    QTRY_VERIFY_WITH_TIMEOUT(!controller.busy(), 5000);
    QCOMPARE(scans.load(), 3);
}

void LibraryControllerTests::scanFailureUsesTraditionalChineseErrorState()
{
    LibraryController controller(
        [](const ListQuery &, std::stop_token) -> QVector<ListItem> {
            throw std::runtime_error("access denied");
        },
        resolver());

    controller.navigateToFolder(QStringLiteral("gallery"), true);
    QTRY_VERIFY_WITH_TIMEOUT(!controller.busy(), 5000);

    QCOMPARE(controller.items()->rowCount(), 0);
    QCOMPARE(controller.errorMessage(), QStringLiteral("無法載入資料夾：access denied"));
    QCOMPARE(controller.statusText(), controller.errorMessage());
}

void LibraryControllerTests::invalidFolderDoesNotMutateHistory()
{
    std::atomic_int scans = 0;
    LibraryController controller(
        [&](const ListQuery &, std::stop_token) {
            ++scans;
            return QVector<ListItem>{};
        },
        resolver());

    controller.navigateToFolder(QStringLiteral("invalid"), true);

    QCOMPARE(scans.load(), 0);
    QVERIFY(controller.currentFolderPath().isEmpty());
    QVERIFY(!controller.canGoBack());
    QVERIFY(controller.statusText().contains(QStringLiteral("資料夾無法使用")));
}

void LibraryControllerTests::modelExposesStableRoles()
{
    LibraryItemModel model;
    const auto roles = model.roleNames();

    QCOMPARE(roles.value(LibraryItemModel::ItemTypeRole), QByteArrayLiteral("itemType"));
    QCOMPARE(roles.value(LibraryItemModel::PathRole), QByteArrayLiteral("path"));
    QCOMPARE(roles.value(LibraryItemModel::NameRole), QByteArrayLiteral("name"));
    QCOMPARE(roles.value(LibraryItemModel::SelectedRole), QByteArrayLiteral("selected"));
    QCOMPARE(roles.value(LibraryItemModel::ThumbnailPathRole), QByteArrayLiteral("thumbnailPath"));
    QCOMPARE(roles.value(LibraryItemModel::ThumbnailUrlRole), QByteArrayLiteral("thumbnailUrl"));
}

void LibraryControllerTests::modelUpdatesOnlyChangedSelectionRows()
{
    LibraryItemModel model;
    model.replaceItems({
        imageItem(QStringLiteral("gallery"), QStringLiteral("one.jpg")),
        imageItem(QStringLiteral("gallery"), QStringLiteral("two.jpg")),
        imageItem(QStringLiteral("gallery"), QStringLiteral("three.jpg")),
    });
    QSignalSpy changed(&model, &QAbstractItemModel::dataChanged);

    model.setSelectedPathKeys({piclens::core::path_rules::pathKey(
        QStringLiteral("gallery/two.jpg"))});

    QCOMPARE(changed.count(), 1);
    QCOMPARE(changed.first().at(0).value<QModelIndex>().row(), 1);
    QCOMPARE(changed.first().at(1).value<QModelIndex>().row(), 1);
}

void LibraryControllerTests::searchAndSortPreserveLoadedThumbnails()
{
    LibraryController controller(
        [](const ListQuery &query, std::stop_token) {
            return QVector<ListItem>{
                imageItem(query.folderPath, QStringLiteral("one.jpg"), 100),
                imageItem(query.folderPath, QStringLiteral("two.jpg"), 200),
            };
        },
        resolver());
    controller.navigateToFolder(QStringLiteral("gallery"), true);
    QTRY_VERIFY_WITH_TIMEOUT(!controller.busy(), 5000);
    controller.items()->setThumbnailPath(
        QStringLiteral("gallery/two.jpg"),
        QStringLiteral("C:/cache/two.png"),
        160);

    controller.setSearchQuery(QStringLiteral("two"));
    QCOMPARE(controller.items()->rowCount(), 1);
    QCOMPARE(
        controller.items()->data(
            controller.items()->index(0), LibraryItemModel::ThumbnailPathRole).toString(),
        QStringLiteral("C:/cache/two.png"));
    QCOMPARE(
        controller.items()->data(
            controller.items()->index(0), LibraryItemModel::ThumbnailUrlRole).toUrl().toString(),
        QStringLiteral("image://piclens-thumbnails/two.png"));

    controller.setSearchQuery({});
    controller.changeSort({.key = SortKey::ModifiedAt, .direction = SortDirection::Desc});
    QCOMPARE(modelName(controller.items(), 0), QStringLiteral("two.jpg"));
    QCOMPARE(
        controller.items()->data(
            controller.items()->index(0), LibraryItemModel::ThumbnailPathRole).toString(),
        QStringLiteral("C:/cache/two.png"));
}

void LibraryControllerTests::selectionGesturesUseOneOrderedImageOwner()
{
    LibraryController controller(
        [](const ListQuery &query, std::stop_token) {
            return QVector<ListItem>{
                imageItem(query.folderPath, QStringLiteral("one.jpg")),
                folderItem(query.folderPath, QStringLiteral("nested")),
                imageItem(query.folderPath, QStringLiteral("two.jpg")),
                imageItem(query.folderPath, QStringLiteral("three.jpg")),
            };
        },
        resolver());

    controller.navigateToFolder(QStringLiteral("gallery"), true);
    QTRY_VERIFY_WITH_TIMEOUT(!controller.busy(), 5000);

    controller.selectPath(QStringLiteral("gallery/one.jpg"), false, false);
    QCOMPARE(controller.selectedPaths(), QStringList{QStringLiteral("gallery/one.jpg")});
    QVERIFY(controller.hasSingleSelectedImage());
    QCOMPARE(controller.selectionSummary(), QStringLiteral("已選取 1 張圖片"));

    controller.selectPath(QStringLiteral("gallery/three.jpg"), true, false);
    QCOMPARE(
        controller.selectedPaths(),
        QStringList({QStringLiteral("gallery/one.jpg"), QStringLiteral("gallery/three.jpg")}));
    QVERIFY(controller.hasSelectedImages());
    QVERIFY(!controller.hasSingleSelectedImage());

    controller.selectPath(QStringLiteral("gallery/three.jpg"), true, false);
    QCOMPARE(controller.selectedPaths(), QStringList{QStringLiteral("gallery/one.jpg")});

    controller.selectPath(QStringLiteral("gallery/three.jpg"), false, true);
    QCOMPARE(
        controller.selectedPaths(),
        QStringList({
            QStringLiteral("gallery/one.jpg"),
            QStringLiteral("gallery/two.jpg"),
            QStringLiteral("gallery/three.jpg"),
        }));
    QCOMPARE(controller.selectedCount(), 3);
    QCOMPARE(controller.selectionSummary(), QStringLiteral("已選取 3 張圖片"));

    controller.prepareContextSelection(QStringLiteral("gallery/two.jpg"));
    QCOMPARE(controller.selectedCount(), 3);
    controller.prepareContextSelection(QStringLiteral("gallery/one.jpg"));
    QCOMPARE(controller.selectedCount(), 3);

    controller.clearSelection();
    controller.prepareContextSelection(QStringLiteral("gallery/two.jpg"));
    QCOMPARE(controller.selectedPaths(), QStringList{QStringLiteral("gallery/two.jpg")});
    controller.selectPath(QStringLiteral("gallery/nested"), false, false);
    QCOMPARE(controller.selectedPaths(), QStringList{QStringLiteral("gallery/two.jpg")});
}

void LibraryControllerTests::viewerSnapshotUsesImageOrderAndPreferredSelection()
{
    LibraryController controller(
        [](const ListQuery &query, std::stop_token) {
            return QVector<ListItem>{
                imageItem(query.folderPath, QStringLiteral("one.jpg")),
                folderItem(query.folderPath, QStringLiteral("nested")),
                imageItem(query.folderPath, QStringLiteral("two.jpg")),
                imageItem(query.folderPath, QStringLiteral("three.jpg")),
            };
        },
        resolver());
    controller.navigateToFolder(QStringLiteral("gallery"), true);
    QTRY_VERIFY_WITH_TIMEOUT(!controller.busy(), 5000);
    controller.setSelectedPaths({
        QStringLiteral("gallery/three.jpg"),
        QStringLiteral("gallery/one.jpg"),
    });

    const auto clicked = controller.createImageSequenceSnapshot(
        QStringLiteral("gallery/two.jpg"), false);
    QVERIFY(clicked.has_value());
    QCOMPARE(clicked->images.size(), 3);
    QCOMPARE(clicked->currentIndex, 1);
    QCOMPARE(clicked->images.at(1).name, QStringLiteral("two.jpg"));

    const auto selected = controller.createImageSequenceSnapshot(
        QStringLiteral("gallery/two.jpg"), true);
    QVERIFY(selected.has_value());
    QCOMPARE(selected->currentIndex, 2);
    QCOMPARE(selected->images.at(selected->currentIndex).name, QStringLiteral("three.jpg"));
}

void LibraryControllerTests::searchFiltersNameAndPathWithoutRescanning()
{
    std::atomic_int scans = 0;
    LibraryController controller(
        [&](const ListQuery &query, std::stop_token) {
            ++scans;
            return QVector<ListItem>{
                folderItem(query.folderPath, QStringLiteral("Summer Albums")),
                imageItem(query.folderPath, QStringLiteral("Sunset.JPG")),
                imageItem(query.folderPath + QStringLiteral("/Trips"), QStringLiteral("ocean.png")),
            };
        },
        resolver());
    QSignalSpy queryChanged(&controller, &LibraryController::searchQueryChanged);

    controller.navigateToFolder(QStringLiteral("gallery"), true);
    QTRY_VERIFY_WITH_TIMEOUT(!controller.busy(), 5000);
    QCOMPARE(controller.items()->rowCount(), 3);

    controller.setSearchQuery(QStringLiteral("su"));
    QCOMPARE(scans.load(), 1);
    QCOMPARE(controller.items()->rowCount(), 2);
    QCOMPARE(modelName(controller.items(), 0), QStringLiteral("Summer Albums"));
    QCOMPARE(modelName(controller.items(), 1), QStringLiteral("Sunset.JPG"));
    QVERIFY(controller.hasSearchQuery());
    QCOMPARE(controller.visibleImages().size(), 1);
    QVERIFY(controller.statusText().contains(QStringLiteral("2 個項目")));

    controller.setSearchQuery(QStringLiteral("trips"));
    QCOMPARE(controller.items()->rowCount(), 1);
    QCOMPARE(modelName(controller.items(), 0), QStringLiteral("ocean.png"));
    QVERIFY(controller.containsImagePath(QStringLiteral("gallery/Trips/ocean.png")));

    controller.setSearchQuery(QStringLiteral("missing"));
    QCOMPARE(controller.items()->rowCount(), 0);
    QVERIFY(controller.visibleImages().isEmpty());
    controller.setSearchQuery({});
    QCOMPARE(controller.items()->rowCount(), 3);
    QVERIFY(!controller.hasSearchQuery());
    QCOMPARE(queryChanged.count(), 4);
    QCOMPARE(scans.load(), 1);
}

QTEST_GUILESS_MAIN(LibraryControllerTests)

#include "tst_library_controller.moc"
