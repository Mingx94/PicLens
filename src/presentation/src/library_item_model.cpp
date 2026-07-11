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

void LibraryItemModel::replaceItems(QVector<core::ListItem> items, bool preserveThumbnails)
{
    beginResetModel();
    m_items = std::move(items);
    m_rowByPathKey.clear();
    m_rowByPathKey.reserve(m_items.size());
    for (int row = 0; row < m_items.size(); ++row) {
        const QString key = core::path_rules::pathKey(
            core::list_item_sorter::itemPath(m_items.at(row)));
        m_rowByPathKey.insert(key, row);
    }
    if (!preserveThumbnails) {
        m_thumbnails.clear();
    }
    endResetModel();
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
