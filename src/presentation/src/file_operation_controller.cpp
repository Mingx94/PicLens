#include <piclens/presentation/file_operation_controller.h>

#include <piclens/core/path_rules.h>
#include <piclens/presentation/library_controller.h>

#include <QFileInfo>
#include <QFutureWatcher>
#include <QtConcurrentRun>

#include <algorithm>
#include <exception>
#include <stdexcept>
#include <utility>

namespace piclens::presentation {
namespace {

struct RenameTaskResult {
    core::FileOperationResult result;
    QString exceptionDetails;
    bool canceled = false;
};

struct TrashTaskResult {
    core::FileOperationBatchResult batch;
    QString exceptionDetails;
    bool canceled = false;
};

struct BatchTaskResult {
    core::FileOperationBatchResult batch;
    QString exceptionDetails;
    bool canceled = false;
};

QString dropRenameReasonText(const std::optional<QString> &reason)
{
    return reason == core::file_rename_planner::AlreadyTargetSequenceReason
        ? QStringLiteral("已符合目標序列")
        : QStringLiteral("略過");
}

} // namespace

FileOperationController::FileOperationController(
    LibraryController *library,
    RenameFunction rename,
    TrashFunction trash,
    BatchFunction convertVisible,
    BatchFunction clearSameBasename,
    RevealFunction reveal,
    DropRenameFunction dropRename,
    ExistingPathsFunction existingPaths,
    QObject *parent)
    : QObject(parent)
    , m_library(library)
    , m_rename(std::move(rename))
    , m_trash(std::move(trash))
    , m_convertVisible(std::move(convertVisible))
    , m_clearSameBasename(std::move(clearSameBasename))
    , m_reveal(std::move(reveal))
    , m_dropRename(std::move(dropRename))
    , m_existingPaths(std::move(existingPaths))
{
    if (!m_library || !m_rename || !m_trash || !m_convertVisible
        || !m_clearSameBasename || !m_reveal) {
        throw std::invalid_argument("File operation controller dependencies are required.");
    }
    if (!m_dropRename) {
        m_dropRename = [](const QVector<QString> &, const QString &, std::stop_token) {
            return core::FileOperationBatchResult{};
        };
    }
    if (!m_existingPaths) {
        m_existingPaths = [](const QString &) { return QVector<QString>{}; };
    }
    m_workerPool.setMaxThreadCount(1);
    m_workerPool.setExpiryTimeout(30'000);
    connect(m_library, &LibraryController::selectionChanged, this, [this] {
        emit commandAvailabilityChanged();
    });
    connect(m_library, &LibraryController::busyChanged, this, [this] {
        emit commandAvailabilityChanged();
    });
    connect(m_library, &LibraryController::searchQueryChanged, this, [this] {
        emit commandAvailabilityChanged();
    });
}

FileOperationController::~FileOperationController()
{
    cancel();
    m_workerPool.waitForDone();
}

bool FileOperationController::busy() const { return m_busy; }
bool FileOperationController::canRename() const { return !m_busy && m_library->hasSingleSelectedImage(); }
bool FileOperationController::canTrash() const { return !m_busy && m_library->hasSelectedImages(); }
QString FileOperationController::selectedBaseName() const
{
    return m_library->hasSingleSelectedImage()
        ? QFileInfo(m_library->selectedPaths().constFirst()).completeBaseName()
        : QString{};
}
bool FileOperationController::canProcessVisible() const
{
    return !m_busy && !m_library->busy() && !m_library->visibleImages().isEmpty();
}
int FileOperationController::visibleImageCount() const
{
    return m_library->visibleImages().size();
}
bool FileOperationController::dragActive() const { return !m_dragSources.isEmpty(); }
int FileOperationController::dragSourceCount() const { return m_dragSources.size(); }
bool FileOperationController::dropRenamePreviewVisible() const
{
    return !m_dropTargetPath.isEmpty() && m_dropRenamePlan.total > 0;
}
int FileOperationController::dropRenameCount() const
{
    return static_cast<int>(std::count_if(
        m_dropRenamePlan.items.cbegin(),
        m_dropRenamePlan.items.cend(),
        [](const auto &item) { return !item.shouldSkip; }));
}
int FileOperationController::dropRenameSkippedCount() const
{
    return m_dropRenamePlan.items.size() - dropRenameCount();
}
QString FileOperationController::dropRenamePreviewText() const
{
    QStringList lines;
    const int previewCount = std::min(12, static_cast<int>(m_dropRenamePlan.items.size()));
    for (int index = 0; index < previewCount; ++index) {
        const auto &item = m_dropRenamePlan.items.at(index);
        const QString sourceName = QFileInfo(item.sourcePath).fileName();
        lines.append(item.shouldSkip
            ? QStringLiteral("%1：%2").arg(sourceName, dropRenameReasonText(item.reason))
            : QStringLiteral("%1 → %2").arg(sourceName, QFileInfo(item.targetPath).fileName()));
    }
    if (m_dropRenamePlan.items.size() > previewCount) {
        lines.append(QStringLiteral("另有 %1 個項目…").arg(m_dropRenamePlan.items.size() - previewCount));
    }
    return lines.join(QLatin1Char('\n'));
}

void FileOperationController::renameSelected(const QString &newBaseName)
{
    if (!canRename()) {
        return;
    }
    const QString baseName = newBaseName.trimmed();
    if (baseName.isEmpty()) {
        m_library->setExternalStatus(QStringLiteral("檔名不可為空白。"));
        return;
    }
    const QString sourcePath = m_library->selectedPaths().constFirst();
    const QString suffix = QFileInfo(sourcePath).suffix();
    const QString newFileName = suffix.isEmpty() ? baseName : baseName + QLatin1Char('.') + suffix;
    const QString folderPath = m_library->currentFolderPath();
    auto stop = std::make_shared<std::stop_source>();
    m_activeStop = stop;
    setBusy(true);
    m_library->setExternalStatus(QStringLiteral("正在重新命名圖片…"));

    auto *watcher = new QFutureWatcher<RenameTaskResult>(this);
    connect(watcher, &QFutureWatcher<RenameTaskResult>::finished, this, [this, watcher, folderPath, stop] {
        const RenameTaskResult task = watcher->result();
        watcher->deleteLater();
        if (m_activeStop == stop) {
            m_activeStop.reset();
            setBusy(false);
        }
        if (task.canceled) {
            m_library->setExternalStatus(QStringLiteral("已取消重新命名。"));
            return;
        }
        if (!task.exceptionDetails.isEmpty()) {
            emit operationFailed(QStringLiteral("rename"), {}, {}, QStringLiteral("exception"), task.exceptionDetails);
            m_library->setExternalStatus(QStringLiteral("重新命名時發生錯誤，已寫入診斷記錄。"));
            return;
        }
        if (task.result.status == core::FileOperationStatus::Renamed) {
            m_library->setExternalStatus(QStringLiteral("已重新命名為 %1。").arg(
                QFileInfo(task.result.targetPath.value_or(QString{})).fileName()));
        } else if (task.result.status == core::FileOperationStatus::Skipped) {
            m_library->setExternalStatus(QStringLiteral("重新命名已略過。"));
        } else {
            reportFailure(QStringLiteral("rename"), task.result);
            m_library->setExternalStatus(task.result.message.value_or(
                QStringLiteral("重新命名失敗，已寫入診斷記錄。")));
        }
        finishForFolder(folderPath);
    });
    watcher->setFuture(QtConcurrent::run(&m_workerPool, [rename = m_rename, sourcePath, newFileName, stop] {
        try {
            return RenameTaskResult{
                .result = rename(sourcePath, newFileName, stop->get_token()),
                .exceptionDetails = {},
                .canceled = false,
            };
        } catch (const std::exception &exception) {
            return RenameTaskResult{
                .result = {},
                .exceptionDetails = QString::fromUtf8(exception.what()),
                .canceled = stop->stop_requested(),
            };
        }
    }));
}

void FileOperationController::trashSelected()
{
    if (!canTrash()) {
        return;
    }
    const QStringList paths = m_library->selectedPaths();
    const QString folderPath = m_library->currentFolderPath();
    auto stop = std::make_shared<std::stop_source>();
    m_activeStop = stop;
    setBusy(true);
    m_library->setExternalStatus(QStringLiteral("正在將 %1 張圖片移至回收筒…").arg(paths.size()));

    auto *watcher = new QFutureWatcher<TrashTaskResult>(this);
    connect(watcher, &QFutureWatcher<TrashTaskResult>::finished, this, [this, watcher, folderPath, stop] {
        const TrashTaskResult task = watcher->result();
        watcher->deleteLater();
        if (m_activeStop == stop) {
            m_activeStop.reset();
            setBusy(false);
        }
        if (task.canceled) {
            m_library->setExternalStatus(QStringLiteral("已取消移至回收筒。"));
            return;
        }
        if (!task.exceptionDetails.isEmpty()) {
            emit operationFailed(QStringLiteral("trash"), {}, {}, QStringLiteral("exception"), task.exceptionDetails);
            m_library->setExternalStatus(QStringLiteral("移至回收筒時發生錯誤，已寫入診斷記錄。"));
            return;
        }
        for (const auto &result : task.batch.items) {
            if (result.status == core::FileOperationStatus::Failed) {
                reportFailure(QStringLiteral("trash"), result);
            }
        }
        m_library->setExternalStatus(
            QStringLiteral("移至回收筒：成功 %1，略過 %2，失敗 %3。")
                .arg(task.batch.succeeded())
                .arg(task.batch.skipped())
                .arg(task.batch.failed()));
        finishForFolder(folderPath);
    });
    watcher->setFuture(QtConcurrent::run(&m_workerPool, [trash = m_trash, paths, stop] {
        TrashTaskResult task;
        try {
            for (const QString &path : paths) {
                if (stop->stop_requested()) {
                    task.canceled = true;
                    break;
                }
                task.batch.items.append(trash(path, stop->get_token()));
            }
        } catch (const std::exception &exception) {
            task.exceptionDetails = QString::fromUtf8(exception.what());
            task.canceled = stop->stop_requested();
        }
        return task;
    }));
}

void FileOperationController::reveal(const QString &path)
{
    if (!m_library->containsImagePath(path)) {
        return;
    }
    try {
        m_reveal(path);
        m_library->setExternalStatus(QStringLiteral("已在檔案管理器中顯示圖片。"));
    } catch (const std::exception &exception) {
        emit operationFailed(
            QStringLiteral("reveal"),
            path,
            {},
            QStringLiteral("launch_failed"),
            QString::fromUtf8(exception.what()));
        m_library->setExternalStatus(QStringLiteral("無法開啟檔案管理器，已寫入診斷記錄。"));
    }
}

void FileOperationController::convertVisible()
{
    startBatch(
        QStringLiteral("convert"),
        QStringLiteral("轉換為 JPG"),
        QStringLiteral("正在轉換 %1 張圖片為 JPG…").arg(visibleImageCount()),
        m_convertVisible);
}

void FileOperationController::clearSameBasename()
{
    startBatch(
        QStringLiteral("clear_same_basename"),
        QStringLiteral("清除同名檔案"),
        QStringLiteral("正在清除同名的非 JPG 圖片…"),
        m_clearSameBasename);
}

void FileOperationController::cancel()
{
    if (m_activeStop) {
        m_activeStop->request_stop();
    }
}

void FileOperationController::beginImageDrag(const QString &sourcePath)
{
    if (m_busy || !m_library->containsImagePath(sourcePath)) {
        return;
    }
    const QStringList selected = m_library->selectedPaths();
    const bool sourceSelected = std::any_of(selected.cbegin(), selected.cend(), [&](const QString &path) {
        return core::path_rules::pathEquals(path, sourcePath);
    });
    m_dragSources = sourceSelected ? QVector<QString>(selected.cbegin(), selected.cend())
                                   : QVector<QString>{sourcePath};
    m_dragOriginPath = sourcePath;
    emit dragStateChanged();
}

void FileOperationController::cancelImageDrag()
{
    if (m_dragSources.isEmpty()) {
        return;
    }
    m_dragSources.clear();
    m_dragOriginPath.clear();
    emit dragStateChanged();
}

void FileOperationController::requestDropRenamePreview(const QString &targetPath)
{
    if (m_busy || m_dragSources.isEmpty() || !m_library->containsImagePath(targetPath)
        || core::path_rules::pathEquals(m_dragOriginPath, targetPath)) {
        cancelImageDrag();
        return;
    }
    const QVector<QString> sources = m_dragSources;
    const QString dragOriginPath = m_dragOriginPath;
    m_dragSources.clear();
    m_dragOriginPath.clear();
    emit dragStateChanged();
    try {
        m_dropRenamePlan = core::file_rename_planner::planDropTargetBatchRename(
            sources, targetPath, m_existingPaths(targetPath));
        m_dragSources = sources;
        m_dragOriginPath = dragOriginPath;
        m_dropTargetPath = targetPath;
        if (m_dropRenamePlan.total <= 0) {
            clearDropRenameState();
            m_library->setExternalStatus(QStringLiteral("沒有可拖放重新命名的圖片。"));
            return;
        }
        emit dropRenamePreviewChanged();
        emit dropRenamePreviewReady();
    } catch (const std::exception &exception) {
        clearDropRenameState();
        emit operationFailed(
            QStringLiteral("drop_rename_preview"), {}, targetPath,
            QStringLiteral("exception"), QString::fromUtf8(exception.what()));
        m_library->setExternalStatus(QStringLiteral("建立拖放重新命名預覽時發生錯誤，已寫入診斷記錄。"));
    }
}

void FileOperationController::cancelDropRenamePreview()
{
    if (!dropRenamePreviewVisible() && m_dragSources.isEmpty()) {
        return;
    }
    clearDropRenameState();
    m_library->setExternalStatus(QStringLiteral("已取消拖放重新命名。"));
}

void FileOperationController::confirmDropRename()
{
    if (m_busy || !dropRenamePreviewVisible()) {
        return;
    }
    const QVector<QString> sources = m_dragSources;
    const QString targetPath = m_dropTargetPath;
    const QString folderPath = m_library->currentFolderPath();
    clearDropRenameState();
    auto stop = std::make_shared<std::stop_source>();
    m_activeStop = stop;
    setBusy(true);
    m_library->setExternalStatus(QStringLiteral("正在套用拖放重新命名…"));

    auto *watcher = new QFutureWatcher<BatchTaskResult>(this);
    connect(watcher, &QFutureWatcher<BatchTaskResult>::finished, this,
        [this, watcher, folderPath, stop] {
            const BatchTaskResult task = watcher->result();
            watcher->deleteLater();
            if (m_activeStop == stop) {
                m_activeStop.reset();
                setBusy(false);
            }
            if (task.canceled) {
                m_library->setExternalStatus(QStringLiteral("已取消拖放重新命名。"));
                return;
            }
            if (!task.exceptionDetails.isEmpty()) {
                emit operationFailed(QStringLiteral("drop_rename"), {}, {},
                    QStringLiteral("exception"), task.exceptionDetails);
                m_library->setExternalStatus(QStringLiteral("拖放重新命名時發生錯誤，已寫入診斷記錄。"));
                return;
            }
            for (const auto &result : task.batch.items) {
                if (result.status == core::FileOperationStatus::Failed) {
                    reportFailure(QStringLiteral("drop_rename"), result);
                }
            }
            m_library->setExternalStatus(
                QStringLiteral("拖放重新命名：成功 %1，略過 %2，失敗 %3。")
                    .arg(task.batch.succeeded()).arg(task.batch.skipped()).arg(task.batch.failed()));
            finishForFolder(folderPath);
        });
    watcher->setFuture(QtConcurrent::run(&m_workerPool,
        [dropRename = m_dropRename, sources, targetPath, stop] {
            try {
                return BatchTaskResult{
                    .batch = dropRename(sources, targetPath, stop->get_token()),
                    .exceptionDetails = {}, .canceled = false};
            } catch (const std::exception &exception) {
                return BatchTaskResult{
                    .batch = {}, .exceptionDetails = QString::fromUtf8(exception.what()),
                    .canceled = stop->stop_requested()};
            }
        }));
}

void FileOperationController::clearDropRenameState()
{
    const bool hadDrag = !m_dragSources.isEmpty();
    const bool hadPreview = dropRenamePreviewVisible();
    m_dragSources.clear();
    m_dragOriginPath.clear();
    m_dropTargetPath.clear();
    m_dropRenamePlan = {};
    if (hadDrag) {
        emit dragStateChanged();
    }
    if (hadPreview) {
        emit dropRenamePreviewChanged();
    }
}

void FileOperationController::setBusy(bool busy)
{
    if (m_busy == busy) {
        return;
    }
    m_busy = busy;
    emit busyChanged();
    emit commandAvailabilityChanged();
}

void FileOperationController::finishForFolder(const QString &folderPath)
{
    m_library->clearSelection();
    if (core::path_rules::pathEquals(folderPath, m_library->currentFolderPath())) {
        m_library->refreshAfterFileOperation();
    }
}

void FileOperationController::reportFailure(
    const QString &operation,
    const core::FileOperationResult &result)
{
    emit operationFailed(
        operation,
        result.path,
        result.targetPath.value_or(QString{}),
        result.reason.value_or(QStringLiteral("unknown")),
        result.message.value_or(QString{}));
}

void FileOperationController::startBatch(
    QString operation,
    QString statusName,
    QString inProgressStatus,
    const BatchFunction &function)
{
    if (!canProcessVisible()) {
        m_library->setExternalStatus(QStringLiteral("目前沒有可處理的圖片。"));
        return;
    }
    const QVector<core::ImageListItem> images = m_library->visibleImages();
    const QString folderPath = m_library->currentFolderPath();
    auto stop = std::make_shared<std::stop_source>();
    m_activeStop = stop;
    setBusy(true);
    m_library->setExternalStatus(std::move(inProgressStatus));

    auto *watcher = new QFutureWatcher<BatchTaskResult>(this);
    connect(
        watcher,
        &QFutureWatcher<BatchTaskResult>::finished,
        this,
        [this, watcher, folderPath, stop, operation = std::move(operation), statusName = std::move(statusName)] {
            const BatchTaskResult task = watcher->result();
            watcher->deleteLater();
            if (m_activeStop == stop) {
                m_activeStop.reset();
                setBusy(false);
            }
            if (task.canceled) {
                m_library->setExternalStatus(QStringLiteral("已取消%1。").arg(statusName));
                return;
            }
            if (!task.exceptionDetails.isEmpty()) {
                emit operationFailed(operation, {}, {}, QStringLiteral("exception"), task.exceptionDetails);
                m_library->setExternalStatus(
                    QStringLiteral("%1時發生錯誤，已寫入診斷記錄。").arg(statusName));
                return;
            }
            for (const auto &result : task.batch.items) {
                if (result.status == core::FileOperationStatus::Failed) {
                    reportFailure(operation, result);
                }
            }
            m_library->setExternalStatus(
                QStringLiteral("%1：成功 %2，略過 %3，失敗 %4。")
                    .arg(statusName)
                    .arg(task.batch.succeeded())
                    .arg(task.batch.skipped())
                    .arg(task.batch.failed()));
            finishForFolder(folderPath);
        });
    watcher->setFuture(QtConcurrent::run(
        &m_workerPool,
        [function, images, stop] {
            try {
                return BatchTaskResult{
                    .batch = function(images, stop->get_token()),
                    .exceptionDetails = {},
                    .canceled = false,
                };
            } catch (const std::exception &exception) {
                return BatchTaskResult{
                    .batch = {},
                    .exceptionDetails = QString::fromUtf8(exception.what()),
                    .canceled = stop->stop_requested(),
                };
            }
        }));
}

} // namespace piclens::presentation
