#include <piclens/presentation/file_operation_controller.h>
#include <piclens/presentation/library_controller.h>

#include <QDir>
#include <QSignalSpy>
#include <QTest>

#include <atomic>
#include <stdexcept>
#include <thread>
#include <utility>

using namespace piclens::core;
using namespace piclens::presentation;

namespace {

ImageListItem image(const QString &folder, const QString &name)
{
    return {
        .path = QDir(folder).filePath(name),
        .name = name,
        .extension = QFileInfo(name).suffix(),
        .modifiedAtMs = 0,
        .sizeBytes = 100,
        .isAnimated = false,
    };
}

LibraryController createLibrary(std::atomic_int &scans)
{
    return LibraryController(
        [&scans](const ListQuery &query, std::stop_token) {
            ++scans;
            return QVector<ListItem>{
                image(query.folderPath, QStringLiteral("one.png")),
                image(query.folderPath, QStringLiteral("two.jpg")),
            };
        },
        [](const QString &path) -> std::optional<QString> { return path; });
}

void load(LibraryController &library)
{
    library.navigateToFolder(QStringLiteral("gallery"), true);
    QTRY_VERIFY_WITH_TIMEOUT(!library.busy(), 5000);
}

FileOperationResult operationResult(
    const QString &path,
    FileOperationStatus status = FileOperationStatus::Failed,
    std::optional<QString> targetPath = std::nullopt,
    std::optional<QString> reason = std::nullopt)
{
    return {
        .path = path,
        .status = status,
        .targetPath = std::move(targetPath),
        .reason = std::move(reason),
        .message = std::nullopt,
    };
}

} // namespace

class FileOperationControllerTests final : public QObject
{
    Q_OBJECT

private slots:
    void renamePreservesExtensionRunsOffThreadAndRefreshes();
    void trashProcessesSelectionInOrderAndContinuesFailures();
    void revealFailureIsDiagnosedWithoutMutatingSelection();
    void visibleBatchUsesImmutableSnapshotAndRefreshes();
    void cancelSuppressesBatchRefreshAndReportsCanceledState();
    void dropRenameRequiresPreviewAndPreservesSelectionOrder();
    void cancelDropRenamePreviewSkipsExecution();
};

void FileOperationControllerTests::renamePreservesExtensionRunsOffThreadAndRefreshes()
{
    std::atomic_int scans = 0;
    LibraryController library = createLibrary(scans);
    load(library);
    library.setSelectedPaths({QStringLiteral("gallery/one.png")});
    QString renamedSource;
    QString renamedName;
    std::thread::id operationThread;
    const std::thread::id guiThread = std::this_thread::get_id();
    FileOperationController operations(
        &library,
        [&](const QString &source, const QString &name, std::stop_token) {
            operationThread = std::this_thread::get_id();
            renamedSource = source;
            renamedName = name;
            return operationResult(
                source,
                FileOperationStatus::Renamed,
                QStringLiteral("gallery/renamed.png"));
        },
        [](const QString &path, std::stop_token) { return operationResult(path); },
        [](const QVector<ImageListItem> &, std::stop_token) { return FileOperationBatchResult{}; },
        [](const QVector<ImageListItem> &, std::stop_token) { return FileOperationBatchResult{}; },
        [](const QString &) {});

    QVERIFY(operations.canRename());
    QCOMPARE(operations.selectedBaseName(), QStringLiteral("one"));
    operations.renameSelected(QStringLiteral("renamed"));
    QVERIFY(operations.busy());
    QTRY_VERIFY_WITH_TIMEOUT(!operations.busy(), 5000);
    QTRY_VERIFY_WITH_TIMEOUT(!library.busy(), 5000);

    QCOMPARE(renamedSource, QStringLiteral("gallery/one.png"));
    QCOMPARE(renamedName, QStringLiteral("renamed.png"));
    QVERIFY(operationThread != guiThread);
    QCOMPARE(library.selectedCount(), 0);
    QCOMPARE(scans.load(), 2);
}

void FileOperationControllerTests::trashProcessesSelectionInOrderAndContinuesFailures()
{
    std::atomic_int scans = 0;
    LibraryController library = createLibrary(scans);
    load(library);
    library.setSelectedPaths({QStringLiteral("gallery/two.jpg"), QStringLiteral("gallery/one.png")});
    QStringList trashed;
    FileOperationController operations(
        &library,
        [](const QString &path, const QString &, std::stop_token) { return operationResult(path); },
        [&](const QString &path, std::stop_token) {
            trashed.append(path);
            const bool failed = path.endsWith(QStringLiteral("two.jpg"));
            return operationResult(
                path,
                failed ? FileOperationStatus::Failed : FileOperationStatus::Trashed,
                std::nullopt,
                failed ? std::optional<QString>{QStringLiteral("trash_failed")} : std::nullopt);
        },
        [](const QVector<ImageListItem> &, std::stop_token) { return FileOperationBatchResult{}; },
        [](const QVector<ImageListItem> &, std::stop_token) { return FileOperationBatchResult{}; },
        [](const QString &) {});
    QSignalSpy failed(&operations, &FileOperationController::operationFailed);

    QVERIFY(operations.canTrash());
    operations.trashSelected();
    QTRY_VERIFY_WITH_TIMEOUT(!operations.busy(), 5000);
    QTRY_VERIFY_WITH_TIMEOUT(!library.busy(), 5000);

    QCOMPARE(trashed, QStringList({QStringLiteral("gallery/two.jpg"), QStringLiteral("gallery/one.png")}));
    QCOMPARE(failed.count(), 1);
    QCOMPARE(library.selectedCount(), 0);
    QCOMPARE(scans.load(), 2);
}

void FileOperationControllerTests::revealFailureIsDiagnosedWithoutMutatingSelection()
{
    std::atomic_int scans = 0;
    LibraryController library = createLibrary(scans);
    load(library);
    library.setSelectedPaths({QStringLiteral("gallery/one.png")});
    FileOperationController operations(
        &library,
        [](const QString &path, const QString &, std::stop_token) { return operationResult(path); },
        [](const QString &path, std::stop_token) { return operationResult(path); },
        [](const QVector<ImageListItem> &, std::stop_token) { return FileOperationBatchResult{}; },
        [](const QVector<ImageListItem> &, std::stop_token) { return FileOperationBatchResult{}; },
        [](const QString &) { throw std::runtime_error("launch denied"); });
    QSignalSpy failed(&operations, &FileOperationController::operationFailed);

    operations.reveal(QStringLiteral("gallery/one.png"));

    QCOMPARE(failed.count(), 1);
    QCOMPARE(failed.at(0).at(0).toString(), QStringLiteral("reveal"));
    QCOMPARE(library.selectedCount(), 1);
    QVERIFY(library.statusText().contains(QStringLiteral("無法開啟")));
}

void FileOperationControllerTests::visibleBatchUsesImmutableSnapshotAndRefreshes()
{
    std::atomic_int scans = 0;
    LibraryController library = createLibrary(scans);
    load(library);
    QVector<ImageListItem> received;
    std::thread::id operationThread;
    const std::thread::id guiThread = std::this_thread::get_id();
    FileOperationController operations(
        &library,
        [](const QString &path, const QString &, std::stop_token) { return operationResult(path); },
        [](const QString &path, std::stop_token) { return operationResult(path); },
        [&](const QVector<ImageListItem> &images, std::stop_token) {
            operationThread = std::this_thread::get_id();
            received = images;
            FileOperationBatchResult batch;
            for (const auto &item : images) {
                batch.items.append(operationResult(item.path, FileOperationStatus::Converted));
            }
            return batch;
        },
        [](const QVector<ImageListItem> &, std::stop_token) { return FileOperationBatchResult{}; },
        [](const QString &) {});

    QVERIFY(operations.canProcessVisible());
    QCOMPARE(operations.visibleImageCount(), 2);
    operations.convertVisible();
    QTRY_VERIFY_WITH_TIMEOUT(!operations.busy(), 5000);
    QTRY_VERIFY_WITH_TIMEOUT(!library.busy(), 5000);

    QCOMPARE(received.size(), 2);
    QCOMPARE(received.at(0).name, QStringLiteral("one.png"));
    QVERIFY(operationThread != guiThread);
    QCOMPARE(scans.load(), 2);
}

void FileOperationControllerTests::cancelSuppressesBatchRefreshAndReportsCanceledState()
{
    std::atomic_int scans = 0;
    std::atomic_bool started = false;
    LibraryController library = createLibrary(scans);
    load(library);
    FileOperationController operations(
        &library,
        [](const QString &path, const QString &, std::stop_token) { return operationResult(path); },
        [](const QString &path, std::stop_token) { return operationResult(path); },
        [&](const QVector<ImageListItem> &, std::stop_token stopToken) -> FileOperationBatchResult {
            started = true;
            while (!stopToken.stop_requested()) {
                std::this_thread::yield();
            }
            throw std::runtime_error("canceled");
        },
        [](const QVector<ImageListItem> &, std::stop_token) { return FileOperationBatchResult{}; },
        [](const QString &) {});

    operations.convertVisible();
    QTRY_VERIFY_WITH_TIMEOUT(started.load(), 5000);
    operations.cancel();
    QTRY_VERIFY_WITH_TIMEOUT(!operations.busy(), 5000);

    QCOMPARE(scans.load(), 1);
    QVERIFY(library.statusText().contains(QStringLiteral("已取消")));
}

void FileOperationControllerTests::dropRenameRequiresPreviewAndPreservesSelectionOrder()
{
    std::atomic_int scans = 0;
    LibraryController library = createLibrary(scans);
    load(library);
    library.setSelectedPaths({QStringLiteral("gallery/two.jpg"), QStringLiteral("gallery/one.png")});
    QVector<QString> receivedSources;
    QString receivedTarget;
    FileOperationController operations(
        &library,
        [](const QString &path, const QString &, std::stop_token) { return operationResult(path); },
        [](const QString &path, std::stop_token) { return operationResult(path); },
        [](const QVector<ImageListItem> &, std::stop_token) { return FileOperationBatchResult{}; },
        [](const QVector<ImageListItem> &, std::stop_token) { return FileOperationBatchResult{}; },
        [](const QString &) {},
        [&](const QVector<QString> &sources, const QString &target, std::stop_token) {
            receivedSources = sources;
            receivedTarget = target;
            FileOperationBatchResult batch;
            batch.items.append(operationResult(
                QStringLiteral("gallery/one.png"),
                FileOperationStatus::Renamed,
                QStringLiteral("gallery/two-01.png")));
            return batch;
        },
        [](const QString &) {
            return QVector<QString>{
                QStringLiteral("gallery/one.png"),
                QStringLiteral("gallery/two.jpg")};
        });
    QSignalSpy previewReady(&operations, &FileOperationController::dropRenamePreviewReady);

    operations.beginImageDrag(QStringLiteral("gallery/one.png"));
    QCOMPARE(operations.dragSourceCount(), 2);
    operations.requestDropRenamePreview(QStringLiteral("gallery/two.jpg"));

    QCOMPARE(previewReady.count(), 1);
    QVERIFY(operations.dropRenamePreviewVisible());
    QCOMPARE(operations.dropRenameCount(), 1);
    QCOMPARE(operations.dropRenameSkippedCount(), 0);
    QVERIFY(operations.dropRenamePreviewText().contains(QStringLiteral("one.png → two-01.png")));
    QVERIFY(receivedSources.isEmpty());

    operations.confirmDropRename();
    QTRY_VERIFY_WITH_TIMEOUT(!operations.busy(), 5000);
    QTRY_VERIFY_WITH_TIMEOUT(!library.busy(), 5000);
    QCOMPARE(receivedSources, QVector<QString>({
        QStringLiteral("gallery/two.jpg"), QStringLiteral("gallery/one.png")}));
    QCOMPARE(receivedTarget, QStringLiteral("gallery/two.jpg"));
    QCOMPARE(library.selectedCount(), 0);
    QCOMPARE(scans.load(), 2);
}

void FileOperationControllerTests::cancelDropRenamePreviewSkipsExecution()
{
    std::atomic_int scans = 0;
    std::atomic_int calls = 0;
    LibraryController library = createLibrary(scans);
    load(library);
    FileOperationController operations(
        &library,
        [](const QString &path, const QString &, std::stop_token) { return operationResult(path); },
        [](const QString &path, std::stop_token) { return operationResult(path); },
        [](const QVector<ImageListItem> &, std::stop_token) { return FileOperationBatchResult{}; },
        [](const QVector<ImageListItem> &, std::stop_token) { return FileOperationBatchResult{}; },
        [](const QString &) {},
        [&](const QVector<QString> &, const QString &, std::stop_token) {
            ++calls;
            return FileOperationBatchResult{};
        },
        [](const QString &) {
            return QVector<QString>{
                QStringLiteral("gallery/one.png"),
                QStringLiteral("gallery/two.jpg")};
        });

    operations.beginImageDrag(QStringLiteral("gallery/one.png"));
    operations.requestDropRenamePreview(QStringLiteral("gallery/two.jpg"));
    QVERIFY(operations.dropRenamePreviewVisible());
    operations.cancelDropRenamePreview();

    QVERIFY(!operations.dropRenamePreviewVisible());
    QVERIFY(!operations.dragActive());
    QCOMPARE(calls.load(), 0);
    QCOMPARE(scans.load(), 1);
    QVERIFY(library.statusText().contains(QStringLiteral("已取消拖放重新命名")));
}

QTEST_GUILESS_MAIN(FileOperationControllerTests)

#include "tst_file_operation_controller.moc"
