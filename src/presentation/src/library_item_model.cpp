#include <piclens/presentation/library_item_model.h>

#include <piclens/core/list_item_sorter.h>
#include <piclens/core/path_rules.h>

#include <QVariant>
#include <QUrl>
#include <QFileInfo>

#include <algorithm>

namespace piclens::presentation {

LibraryItemModel::LibraryItemModel(QObject *parent)
    : QAbstractListModel(parent)
{
}

int LibraryItemModel::rowCount(const QModelIndex &parent) const
{
    return parent.isValid() ? 0 : static_cast<int>(m_items.size());
}

QVariant LibraryItemModel::data(const QModelIndex &index, int role) const
{
    if (!index.isValid() || index.row() < 0 || index.row() >= m_items.size()) {
        return {};
    }

    const core::ListItem &item = m_items.at(index.row());
    const auto *folder = std::get_if<core::FolderListItem>(&item);
    const auto *image = std::get_if<core::ImageListItem>(&item);
    switch (role) {
    case ItemTypeRole:
        return folder ? QStringLiteral("folder") : QStringLiteral("image");
    case PathRole:
        return core::list_item_sorter::itemPath(item);
    case NameRole:
    case Qt::DisplayRole:
        return core::list_item_sorter::itemName(item);
    case ModifiedAtMsRole: {
        const auto modifiedAt = core::list_item_sorter::itemModifiedAtMs(item);
        return modifiedAt.has_value() ? QVariant::fromValue(*modifiedAt) : QVariant{};
    }
    case ExtensionRole:
        return image ? image->extension : QString{};
    case SizeBytesRole:
        return image ? QVariant::fromValue(image->sizeBytes) : QVariant{};
    case AnimatedRole:
        return image && image->isAnimated;
    case SelectedRole:
        return image && m_selectedPathKeys.contains(core::path_rules::pathKey(image->path));
    case ThumbnailPathRole:
        if (image) {
            const auto thumbnail = m_thumbnails.constFind(core::path_rules::pathKey(image->path));
            return thumbnail == m_thumbnails.cend() ? QVariant{} : QVariant{thumbnail->path};
        }
        return {};
    case ThumbnailUrlRole:
        if (image) {
            const auto thumbnail = m_thumbnails.constFind(core::path_rules::pathKey(image->path));
            return thumbnail == m_thumbnails.cend()
                ? QVariant{}
                : QVariant{QUrl(QStringLiteral("image://piclens-thumbnails/")
                    + QFileInfo(thumbnail->path).fileName())};
        }
        return {};
    default:
        return {};
    }
}

QHash<int, QByteArray> LibraryItemModel::roleNames() const
{
    return {
        {ItemTypeRole, QByteArrayLiteral("itemType")},
        {PathRole, QByteArrayLiteral("path")},
        {NameRole, QByteArrayLiteral("name")},
        {ModifiedAtMsRole, QByteArrayLiteral("modifiedAtMs")},
        {ExtensionRole, QByteArrayLiteral("extension")},
        {SizeBytesRole, QByteArrayLiteral("sizeBytes")},
        {AnimatedRole, QByteArrayLiteral("animated")},
        {SelectedRole, QByteArrayLiteral("selected")},
        {ThumbnailPathRole, QByteArrayLiteral("thumbnailPath")},
        {ThumbnailUrlRole, QByteArrayLiteral("thumbnailUrl")},
    };
}

void LibraryItemModel::resetItems(QVector<core::ListItem> items)
{
    beginResetModel();
    m_items = std::move(items);
    m_thumbnails.clear();
    m_rowByPathKey.clear();
    m_rowByPathKey.reserve(m_items.size());
    for (int row = 0; row < m_items.size(); ++row) {
        m_rowByPathKey.insert(core::path_rules::pathKey(
            core::list_item_sorter::itemPath(m_items.at(row))), row);
    }
    endResetModel();
}

void LibraryItemModel::replaceItems(QVector<core::ListItem> items)
{
    for (const core::ListItem &item : std::as_const(items)) {
        const QString key = core::path_rules::pathKey(
            core::list_item_sorter::itemPath(item));
        const auto oldRow = m_rowByPathKey.constFind(key);
        if (oldRow != m_rowByPathKey.cend() && m_items.at(*oldRow) != item) {
            m_thumbnails.remove(key);
        }
    }

    QSet<QString> incomingKeys;
    incomingKeys.reserve(items.size());
    for (const core::ListItem &item : items) {
        incomingKeys.insert(core::path_rules::pathKey(
            core::list_item_sorter::itemPath(item)));
    }

    for (int last = m_items.size() - 1; last >= 0;) {
        const QString key = core::path_rules::pathKey(
            core::list_item_sorter::itemPath(m_items.at(last)));
        if (incomingKeys.contains(key)) {
            --last;
            continue;
        }

        int first = last;
        while (first > 0) {
            const QString previousKey = core::path_rules::pathKey(
                core::list_item_sorter::itemPath(m_items.at(first - 1)));
            if (incomingKeys.contains(previousKey)) {
                break;
            }
            --first;
        }
        beginRemoveRows({}, first, last);
        for (int row = first; row <= last; ++row) {
            m_thumbnails.remove(core::path_rules::pathKey(
                core::list_item_sorter::itemPath(m_items.at(row))));
        }
        m_items.remove(first, last - first + 1);
        endRemoveRows();
        last = first - 1;
    }

    QSet<QString> currentKeys;
    currentKeys.reserve(m_items.size());
    for (const core::ListItem &item : std::as_const(m_items)) {
        currentKeys.insert(core::path_rules::pathKey(
            core::list_item_sorter::itemPath(item)));
    }
    QVector<core::ListItem> additions;
    for (const core::ListItem &item : std::as_const(items)) {
        const QString key = core::path_rules::pathKey(
            core::list_item_sorter::itemPath(item));
        if (!currentKeys.contains(key)) {
            additions.append(item);
        }
    }
    if (!additions.isEmpty()) {
        const int first = m_items.size();
        beginInsertRows({}, first, first + additions.size() - 1);
        m_items.append(std::move(additions));
        endInsertRows();
    }

    bool orderChanged = m_items.size() != items.size();
    for (int row = 0; !orderChanged && row < items.size(); ++row) {
        orderChanged = !core::path_rules::pathEquals(
            core::list_item_sorter::itemPath(m_items.at(row)),
            core::list_item_sorter::itemPath(items.at(row)));
    }

    if (orderChanged) {
        const QModelIndexList oldPersistentIndexes = persistentIndexList();
        QStringList persistentKeys;
        persistentKeys.reserve(oldPersistentIndexes.size());
        for (const QModelIndex &persistentIndex : oldPersistentIndexes) {
            persistentKeys.append(core::path_rules::pathKey(
                core::list_item_sorter::itemPath(m_items.at(persistentIndex.row()))));
        }

        emit layoutAboutToBeChanged();
        m_items = std::move(items);
        QHash<QString, int> newRowsByKey;
        newRowsByKey.reserve(m_items.size());
        for (int row = 0; row < m_items.size(); ++row) {
            newRowsByKey.insert(core::path_rules::pathKey(
                core::list_item_sorter::itemPath(m_items.at(row))), row);
        }
        QModelIndexList newPersistentIndexes;
        newPersistentIndexes.reserve(persistentKeys.size());
        for (const QString &key : std::as_const(persistentKeys)) {
            const auto row = newRowsByKey.constFind(key);
            newPersistentIndexes.append(row == newRowsByKey.cend() ? QModelIndex{} : index(*row));
        }
        changePersistentIndexList(oldPersistentIndexes, newPersistentIndexes);
        emit layoutChanged();
    } else {
        for (int first = 0; first < items.size();) {
            if (m_items.at(first) == items.at(first)) {
                ++first;
                continue;
            }
            int last = first;
            while (last + 1 < items.size() && m_items.at(last + 1) != items.at(last + 1)) {
                ++last;
            }
            for (int row = first; row <= last; ++row) {
                m_items[row] = std::move(items[row]);
            }
            emit dataChanged(
                index(first),
                index(last),
                {ItemTypeRole, PathRole, NameRole, ModifiedAtMsRole,
                 ExtensionRole, SizeBytesRole, AnimatedRole,
                 ThumbnailPathRole, ThumbnailUrlRole});
            first = last + 1;
        }
    }

    m_rowByPathKey.clear();
    m_rowByPathKey.reserve(m_items.size());
    for (int row = 0; row < m_items.size(); ++row) {
        const QString key = core::path_rules::pathKey(
            core::list_item_sorter::itemPath(m_items.at(row)));
        m_rowByPathKey.insert(key, row);
    }
}

void LibraryItemModel::setThumbnailPath(
    const QString &sourcePath,
    const QString &thumbnailPath,
    int requestedSize)
{
    const QString key = core::path_rules::pathKey(sourcePath);
    m_thumbnails.insert(key, {.path = thumbnailPath, .requestedSize = requestedSize});
    const auto row = m_rowByPathKey.constFind(key);
    if (row != m_rowByPathKey.cend()
        && std::holds_alternative<core::ImageListItem>(m_items.at(*row))) {
        const QModelIndex modelIndex = index(*row);
        emit dataChanged(modelIndex, modelIndex, {ThumbnailPathRole, ThumbnailUrlRole});
    }
}

void LibraryItemModel::clearThumbnails()
{
    if (m_thumbnails.isEmpty()) {
        return;
    }
    m_thumbnails.clear();
    if (!m_items.isEmpty()) {
        emit dataChanged(index(0), index(m_items.size() - 1), {ThumbnailPathRole, ThumbnailUrlRole});
    }
}

void LibraryItemModel::setSelectedPathKeys(QSet<QString> selectedPathKeys)
{
    if (m_selectedPathKeys == selectedPathKeys) {
        return;
    }
    QSet<QString> changedKeys = m_selectedPathKeys;
    for (const QString &key : selectedPathKeys) {
        if (!changedKeys.remove(key)) {
            changedKeys.insert(key);
        }
    }
    QVector<int> changedRows;
    changedRows.reserve(changedKeys.size());
    for (const QString &key : changedKeys) {
        const auto row = m_rowByPathKey.constFind(key);
        if (row != m_rowByPathKey.cend()
            && std::holds_alternative<core::ImageListItem>(m_items.at(*row))) {
            changedRows.append(*row);
        }
    }
    m_selectedPathKeys = std::move(selectedPathKeys);
    std::sort(changedRows.begin(), changedRows.end());
    for (int offset = 0; offset < changedRows.size();) {
        int end = offset;
        while (end + 1 < changedRows.size()
               && changedRows.at(end + 1) == changedRows.at(end) + 1) {
            ++end;
        }
        emit dataChanged(index(changedRows.at(offset)), index(changedRows.at(end)), {SelectedRole});
        offset = end + 1;
    }
}

const QVector<core::ListItem> &LibraryItemModel::items() const
{
    return m_items;
}

QStringList LibraryItemModel::imagePaths() const
{
    QStringList paths;
    for (const auto &item : m_items) {
        if (const auto *image = std::get_if<core::ImageListItem>(&item)) {
            paths.append(image->path);
        }
    }
    return paths;
}

} // namespace piclens::presentation
