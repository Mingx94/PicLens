#pragma once

#include <piclens/core/models.h>

namespace piclens::core::list_item_sorter {

[[nodiscard]] QVector<ListItem> sort(
    const QVector<ListItem> &items,
    SortState sortState,
    bool keepFoldersFirst);

[[nodiscard]] QString itemName(const ListItem &item);
[[nodiscard]] QString itemPath(const ListItem &item);
[[nodiscard]] std::optional<qint64> itemModifiedAtMs(const ListItem &item);
[[nodiscard]] bool isFolder(const ListItem &item);

} // namespace piclens::core::list_item_sorter
