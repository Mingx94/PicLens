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

struct SortState {
    SortKey key = SortKey::Name;
    SortDirection direction = SortDirection::Asc;

    bool operator==(const SortState &) const = default;
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

} // namespace piclens::core
