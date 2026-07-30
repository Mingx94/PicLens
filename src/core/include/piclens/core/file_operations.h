#pragma once

#include <QString>
#include <QVector>

#include <optional>

namespace piclens::core {

enum class FileOperationStatus {
    Converted,
    Trashed,
    Renamed,
    Skipped,
    Failed,
};

struct FileOperationResult {
    QString path;
    FileOperationStatus status = FileOperationStatus::Failed;
    std::optional<QString> targetPath;
    std::optional<QString> reason;
    std::optional<QString> message;
};

struct FileOperationBatchResult {
    QVector<FileOperationResult> items;

    [[nodiscard]] int total() const;
    [[nodiscard]] int succeeded() const;
    [[nodiscard]] int skipped() const;
    [[nodiscard]] int failed() const;
};

} // namespace piclens::core
