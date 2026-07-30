#pragma once

#include <piclens/core/file_operations.h>
#include <piclens/core/library_items.h>
#include <piclens/presentation/drop_rename_controller.h>

#include <QObject>
#include <QThreadPool>

#include <functional>
#include <memory>
#include <stop_token>

namespace piclens::presentation {

class LibraryController;

class FileOperationController final : public QObject
{
    Q_OBJECT
    Q_PROPERTY(bool busy READ busy NOTIFY busyChanged)
    Q_PROPERTY(bool canRename READ canRename NOTIFY commandAvailabilityChanged)
    Q_PROPERTY(bool canTrash READ canTrash NOTIFY commandAvailabilityChanged)
    Q_PROPERTY(QString selectedBaseName READ selectedBaseName NOTIFY commandAvailabilityChanged)
    Q_PROPERTY(bool canProcessVisible READ canProcessVisible NOTIFY commandAvailabilityChanged)
    Q_PROPERTY(int visibleImageCount READ visibleImageCount NOTIFY commandAvailabilityChanged)
    Q_PROPERTY(piclens::presentation::DropRenameController *dropRename READ dropRename CONSTANT)

public:
    using RenameFunction = std::function<core::FileOperationResult(
        const QString &, const QString &, std::stop_token)>;
    using TrashFunction = std::function<core::FileOperationResult(const QString &, std::stop_token)>;
    using BatchFunction = std::function<core::FileOperationBatchResult(
        const QVector<core::ImageListItem> &, std::stop_token)>;
    using RevealFunction = std::function<void(const QString &)>;
    using DropRenameFunction = std::function<core::FileOperationBatchResult(
        const QVector<QString> &, const QString &, std::stop_token)>;
    using ExistingPathsFunction = DropRenameController::ExistingPathsFunction;

    FileOperationController(
        LibraryController *library,
        RenameFunction rename,
        TrashFunction trash,
        BatchFunction convertVisible,
        BatchFunction convertVisibleToWebp,
        BatchFunction clearSameBasenameExtras,
        RevealFunction reveal,
        DropRenameFunction dropRename = {},
        ExistingPathsFunction existingPaths = {},
        QObject *parent = nullptr);
    ~FileOperationController() override;

    [[nodiscard]] bool busy() const;
    [[nodiscard]] bool canRename() const;
    [[nodiscard]] bool canTrash() const;
    [[nodiscard]] QString selectedBaseName() const;
    [[nodiscard]] bool canProcessVisible() const;
    [[nodiscard]] int visibleImageCount() const;
    [[nodiscard]] DropRenameController *dropRename();

    Q_INVOKABLE void renameSelected(const QString &newBaseName);
    Q_INVOKABLE void trashSelected();
    Q_INVOKABLE void reveal(const QString &path);
    Q_INVOKABLE void convertVisible();
    Q_INVOKABLE void convertVisibleToWebp();
    Q_INVOKABLE void clearSameBasenameExtras();
    Q_INVOKABLE void cancel();

signals:
    void busyChanged();
    void commandAvailabilityChanged();
    void operationFailed(
        const QString &operation,
        const QString &sourcePath,
        const QString &targetPath,
        const QString &reason,
        const QString &details);

private:
    struct OperationTaskResult {
        std::optional<core::FileOperationResult> result;
        core::FileOperationBatchResult batch;
        QString exceptionDetails;
        bool canceled = false;
    };
    using OperationFunction = std::function<OperationTaskResult(std::stop_token)>;
    using OperationCompletion = std::function<void(const OperationTaskResult &)>;

    void setBusy(bool busy);
    void runOperation(
        QString inProgressStatus,
        OperationFunction function,
        OperationCompletion completion);
    [[nodiscard]] bool handleOperationInterruption(
        const OperationTaskResult &task,
        const QString &operation,
        const QString &canceledStatus,
        const QString &errorStatus);
    void finishForFolder(const QString &folderPath);
    void reportFailure(const QString &operation, const core::FileOperationResult &result);
    void startBatch(
        QString operation,
        QString statusName,
        QString inProgressStatus,
        const BatchFunction &function);
    void startDropRename(const QVector<QString> &sources, const QString &targetPath);

    LibraryController *m_library;
    RenameFunction m_rename;
    TrashFunction m_trash;
    BatchFunction m_convertVisible;
    BatchFunction m_convertVisibleToWebp;
    BatchFunction m_clearSameBasenameExtras;
    RevealFunction m_reveal;
    DropRenameFunction m_dropRename;
    DropRenameController m_dropRenameController;
    QThreadPool m_workerPool;
    std::shared_ptr<std::stop_source> m_activeStop;
    bool m_busy = false;
};

} // namespace piclens::presentation
