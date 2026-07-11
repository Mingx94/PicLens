#pragma once

#include <QString>
#include <QVector>

#include <optional>

namespace piclens::core {

struct FileNameValidationResult {
    bool isValid = false;
    std::optional<QString> reason;
};

struct DropTargetBatchRenamePlanItem {
    QString sourcePath;
    QString targetPath;
    bool shouldSkip = false;
    std::optional<QString> reason;
};

struct DropTargetBatchRenamePlan {
    int total = 0;
    QVector<DropTargetBatchRenamePlanItem> items;
};

namespace file_rename_planner {

inline const QString AlreadyTargetSequenceReason = QStringLiteral("already_target_sequence");

[[nodiscard]] FileNameValidationResult validateImageFileName(const QString &fileName);
[[nodiscard]] DropTargetBatchRenamePlan planDropTargetBatchRename(
    const QVector<QString> &sourcePaths,
    const QString &targetPath,
    const QVector<QString> &existingPaths);

} // namespace file_rename_planner
} // namespace piclens::core
