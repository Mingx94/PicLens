#pragma once

#include <QString>
#include <QVector>
#include <QtTypes>

#include <optional>
#include <variant>

namespace piclens::core {

enum class SortKey {
    Name,
    ModifiedAt,
};

enum class SortDirection {
    Asc,
    Desc,
};

enum class FileOperationStatus {
    Converted,
    Trashed,
    Renamed,
    Skipped,
    Failed,
};

struct SortState {
    SortKey key = SortKey::Name;
    SortDirection direction = SortDirection::Asc;

    bool operator==(const SortState &) const = default;
};

struct AppSettings {
    std::optional<QString> lastFolderPath;
    SortState sort;
    bool includeSubfolders = false;
    int thumbnailSize = 160;

    static AppSettings createDefault();
    bool operator==(const AppSettings &) const = default;
};

struct AppSettingsPatch {
    std::optional<QString> lastFolderPath;
    bool hasLastFolderPath = false;
    std::optional<SortState> sort;
    std::optional<bool> includeSubfolders;
    std::optional<int> thumbnailSize;
};

struct FolderListItem {
    QString path;
    QString name;
    std::optional<qint64> modifiedAtMs;

    bool operator==(const FolderListItem &) const = default;
};

struct ImageListItem {
    QString path;
    QString name;
    QString extension;
    std::optional<qint64> modifiedAtMs;
    qint64 sizeBytes = 0;
    bool isAnimated = false;

    bool operator==(const ImageListItem &) const = default;
};

using ListItem = std::variant<FolderListItem, ImageListItem>;

struct ListQuery {
    QString folderPath;
    bool includeSubfolders = false;
    SortState sort;
};

struct ImageSequenceSnapshot {
    QString sourceFolderPath;
    bool includeSubfolders = false;
    SortState sort;
    QVector<ImageListItem> images;
    int currentIndex = -1;
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

struct Point {
    double x = 0;
    double y = 0;

    bool operator==(const Point &) const = default;
};

struct ZoomState {
    double zoom = 1;
    Point offset;

    bool operator==(const ZoomState &) const = default;
};

} // namespace piclens::core
