#pragma once

#include <piclens/core/file_rename_planner.h>

#include <QObject>

#include <functional>

namespace piclens::presentation {

class LibraryController;

class DropRenameController final : public QObject
{
    Q_OBJECT
    Q_PROPERTY(bool dragActive READ dragActive NOTIFY dragStateChanged)
    Q_PROPERTY(int dragSourceCount READ dragSourceCount NOTIFY dragStateChanged)
    Q_PROPERTY(bool previewVisible READ previewVisible NOTIFY previewChanged)
    Q_PROPERTY(QString previewText READ previewText NOTIFY previewChanged)
    Q_PROPERTY(int renameCount READ renameCount NOTIFY previewChanged)
    Q_PROPERTY(int skippedCount READ skippedCount NOTIFY previewChanged)

public:
    using ExistingPathsFunction = std::function<QVector<QString>(const QString &)>;

    DropRenameController(
        LibraryController *library,
        ExistingPathsFunction existingPaths = {},
        QObject *parent = nullptr);

    [[nodiscard]] bool dragActive() const;
    [[nodiscard]] int dragSourceCount() const;
    [[nodiscard]] bool previewVisible() const;
    [[nodiscard]] QString previewText() const;
    [[nodiscard]] int renameCount() const;
    [[nodiscard]] int skippedCount() const;

    void setOperationBusy(bool busy);

    Q_INVOKABLE void beginImageDrag(const QString &sourcePath);
    Q_INVOKABLE void cancelImageDrag();
    Q_INVOKABLE void requestPreview(const QString &targetPath);
    Q_INVOKABLE void confirm();
    Q_INVOKABLE void cancelPreview();

signals:
    void dragStateChanged();
    void previewChanged();
    void previewReady();
    void previewFailed(const QString &targetPath, const QString &details);
    void executionRequested(const QVector<QString> &sourcePaths, const QString &targetPath);

private:
    void clearState();

    LibraryController *m_library;
    ExistingPathsFunction m_existingPaths;
    QVector<QString> m_dragSources;
    QString m_dragOriginPath;
    QString m_dropTargetPath;
    core::DropTargetBatchRenamePlan m_plan;
    bool m_operationBusy = false;
};

} // namespace piclens::presentation
