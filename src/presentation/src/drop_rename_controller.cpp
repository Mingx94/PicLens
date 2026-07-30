#include <piclens/presentation/drop_rename_controller.h>

#include <piclens/core/path_rules.h>
#include <piclens/presentation/library_controller.h>

#include <QFileInfo>

#include <algorithm>
#include <stdexcept>
#include <utility>

namespace piclens::presentation {
namespace {

QString reasonText(const std::optional<QString> &reason)
{
    return reason == core::file_rename_planner::AlreadyTargetSequenceReason
        ? QStringLiteral("已符合目標序列")
        : QStringLiteral("略過");
}

} // namespace

DropRenameController::DropRenameController(
    LibraryController *library,
    ExistingPathsFunction existingPaths,
    QObject *parent)
    : QObject(parent)
    , m_library(library)
    , m_existingPaths(std::move(existingPaths))
{
    if (!m_library) {
        throw std::invalid_argument("Drop rename controller requires a library.");
    }
    if (!m_existingPaths) {
        m_existingPaths = [](const QString &) { return QVector<QString>{}; };
    }
}

bool DropRenameController::dragActive() const
{
    return !m_dragSources.isEmpty();
}

int DropRenameController::dragSourceCount() const
{
    return m_dragSources.size();
}

bool DropRenameController::previewVisible() const
{
    return !m_dropTargetPath.isEmpty() && m_plan.total > 0;
}

int DropRenameController::renameCount() const
{
    return static_cast<int>(std::count_if(
        m_plan.items.cbegin(),
        m_plan.items.cend(),
        [](const auto &item) { return !item.shouldSkip; }));
}

int DropRenameController::skippedCount() const
{
    return m_plan.items.size() - renameCount();
}

QString DropRenameController::previewText() const
{
    QStringList lines;
    const int previewItemCount = std::min(12, static_cast<int>(m_plan.items.size()));
    for (int index = 0; index < previewItemCount; ++index) {
        const auto &item = m_plan.items.at(index);
        const QString sourceName = QFileInfo(item.sourcePath).fileName();
        lines.append(item.shouldSkip
            ? QStringLiteral("%1：%2").arg(sourceName, reasonText(item.reason))
            : QStringLiteral("%1 → %2").arg(sourceName, QFileInfo(item.targetPath).fileName()));
    }
    if (m_plan.items.size() > previewItemCount) {
        lines.append(QStringLiteral("另有 %1 個項目…").arg(m_plan.items.size() - previewItemCount));
    }
    return lines.join(QLatin1Char('\n'));
}

void DropRenameController::setOperationBusy(bool busy)
{
    m_operationBusy = busy;
    if (busy && (dragActive() || previewVisible())) {
        clearState();
    }
}

void DropRenameController::beginImageDrag(const QString &sourcePath)
{
    if (m_operationBusy || !m_library->containsImagePath(sourcePath)) {
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

void DropRenameController::cancelImageDrag()
{
    if (!dragActive()) {
        return;
    }
    m_dragSources.clear();
    m_dragOriginPath.clear();
    emit dragStateChanged();
}

void DropRenameController::requestPreview(const QString &targetPath)
{
    if (m_operationBusy || !dragActive() || !m_library->containsImagePath(targetPath)
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
        m_plan = core::file_rename_planner::planDropTargetBatchRename(
            sources,
            targetPath,
            m_existingPaths(targetPath));
        m_dragSources = sources;
        m_dragOriginPath = dragOriginPath;
        m_dropTargetPath = targetPath;
        if (m_plan.total <= 0) {
            clearState();
            m_library->setExternalStatus(QStringLiteral("沒有可拖放重新命名的圖片。"));
            return;
        }
        emit previewChanged();
        emit previewReady();
    } catch (const std::exception &exception) {
        clearState();
        emit previewFailed(targetPath, QString::fromUtf8(exception.what()));
        m_library->setExternalStatus(QStringLiteral("建立拖放重新命名預覽時發生錯誤，已寫入診斷記錄。"));
    }
}

void DropRenameController::confirm()
{
    if (m_operationBusy || !previewVisible()) {
        return;
    }
    const QVector<QString> sources = m_dragSources;
    const QString targetPath = m_dropTargetPath;
    clearState();
    emit executionRequested(sources, targetPath);
}

void DropRenameController::cancelPreview()
{
    if (!previewVisible() && !dragActive()) {
        return;
    }
    clearState();
    m_library->setExternalStatus(QStringLiteral("已取消拖放重新命名。"));
}

void DropRenameController::clearState()
{
    const bool hadDrag = dragActive();
    const bool hadPreview = previewVisible();
    m_dragSources.clear();
    m_dragOriginPath.clear();
    m_dropTargetPath.clear();
    m_plan = {};
    if (hadDrag) {
        emit dragStateChanged();
    }
    if (hadPreview) {
        emit previewChanged();
    }
}

} // namespace piclens::presentation
