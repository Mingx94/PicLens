#pragma once

#include <piclens/core/models.h>
#include <piclens/core/file_rename_planner.h>

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
    Q_PROPERTY(bool dragActive READ dragActive NOTIFY dragStateChanged)
    Q_PROPERTY(int dragSourceCount READ dragSourceCount NOTIFY dragStateChanged)
    Q_PROPERTY(bool dropRenamePreviewVisible READ dropRenamePreviewVisible NOTIFY dropRenamePreviewChanged)
    Q_PROPERTY(QString dropRenamePreviewText READ dropRenamePreviewText NOTIFY dropRenamePreviewChanged)
    Q_PROPERTY(int dropRenameCount READ dropRenameCount NOTIFY dropRenamePreviewChanged)
    Q_PROPERTY(int dropRenameSkippedCount READ dropRenameSkippedCount NOTIFY dropRenamePreviewChanged)

public:
    using RenameFunction = std::function<core::FileOperationResult(
        const QString &, const QString &, std::stop_token)>;
    using TrashFunction = std::function<core::FileOperationResult(const QString &, std::stop_token)>;
    using BatchFunction = std::function<core::FileOperationBatchResult(
        const QVector<core::ImageListItem> &, std::stop_token)>;
    using RevealFunction = std::function<void(const QString &)>;
    using DropRenameFunction = std::function<core::FileOperationBatchResult(
        const QVector<QString> &, const QString &, std::stop_token)>;
    using ExistingPathsFunction = std::function<QVector<QString>(const QString &)>;

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
    [[nodiscard]] bool dragActive() const;
    [[nodiscard]] int dragSourceCount() const;
    [[nodiscard]] bool dropRenamePreviewVisible() const;
    [[nodiscard]] QString dropRenamePreviewText() const;
    [[nodiscard]] int dropRenameCount() const;
    [[nodiscard]] int dropRenameSkippedCount() const;

    Q_INVOKABLE void renameSelected(const QString &newBaseName);
    Q_INVOKABLE void trashSelected();
    Q_INVOKABLE void reveal(const QString &path);
    Q_INVOKABLE void convertVisible();
    Q_INVOKABLE void convertVisibleToWebp();
    Q_INVOKABLE void clearSameBasenameExtras();
    Q_INVOKABLE void cancel();
    Q_INVOKABLE void beginImageDrag(const QString &sourcePath);
    Q_INVOKABLE void cancelImageDrag();
    Q_INVOKABLE void requestDropRenamePreview(const QString &targetPath);
    Q_INVOKABLE void confirmDropRename();
    Q_INVOKABLE void cancelDropRenamePreview();

signals:
    void busyChanged();
    void commandAvailabilityChanged();
    void dragStateChanged();
    void dropRenamePreviewChanged();
    void dropRenamePreviewReady();
    void operationFailed(
        const QString &operation,
        const QString &sourcePath,
        const QString &targetPath,
        const QString &reason,
        const QString &details);

private:
    void setBusy(bool busy);
    void finishForFolder(const QString &folderPath);
    void reportFailure(const QString &operation, const core::FileOperationResult &result);
    void startBatch(
        QString operation,
        QString statusName,
        QString inProgressStatus,
        const BatchFunction &function);
    void clearDropRenameState();

    LibraryController *m_library;
    RenameFunction m_rename;
    TrashFunction m_trash;
    BatchFunction m_convertVisible;
    BatchFunction m_convertVisibleToWebp;
    BatchFunction m_clearSameBasenameExtras;
    RevealFunction m_reveal;
    DropRenameFunction m_dropRename;
    ExistingPathsFunction m_existingPaths;
    QThreadPool m_workerPool;
    std::shared_ptr<std::stop_source> m_activeStop;
    bool m_busy = false;
    QVector<QString> m_dragSources;
    QString m_dragOriginPath;
    QString m_dropTargetPath;
    core::DropTargetBatchRenamePlan m_dropRenamePlan;
};

} // namespace piclens::presentation
