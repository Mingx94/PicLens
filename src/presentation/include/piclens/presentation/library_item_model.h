#pragma once

#include <piclens/core/models.h>

#include <QAbstractListModel>
#include <QHash>
#include <QSet>

namespace piclens::presentation {

class LibraryItemModel : public QAbstractListModel
{
    Q_OBJECT

public:
    enum Role {
        ItemTypeRole = Qt::UserRole + 1,
        PathRole,
        NameRole,
        ModifiedAtMsRole,
        ExtensionRole,
        SizeBytesRole,
        AnimatedRole,
        SelectedRole,
        ThumbnailPathRole,
        ThumbnailUrlRole,
    };
    Q_ENUM(Role)

    explicit LibraryItemModel(QObject *parent = nullptr);

    [[nodiscard]] int rowCount(const QModelIndex &parent = {}) const override;
    [[nodiscard]] QVariant data(const QModelIndex &index, int role) const override;
    [[nodiscard]] QHash<int, QByteArray> roleNames() const override;

    void replaceItems(QVector<core::ListItem> items, bool preserveThumbnails = false);
    void setSelectedPathKeys(QSet<QString> selectedPathKeys);
    void setThumbnailPath(const QString &sourcePath, const QString &thumbnailPath, int requestedSize);
    void clearThumbnails();

    [[nodiscard]] const QVector<core::ListItem> &items() const;
    [[nodiscard]] QStringList imagePaths() const;

private:
    QVector<core::ListItem> m_items;
    QHash<QString, int> m_rowByPathKey;
    QSet<QString> m_selectedPathKeys;
    struct ThumbnailEntry {
        QString path;
        int requestedSize = 0;
    };
    QHash<QString, ThumbnailEntry> m_thumbnails;
};

} // namespace piclens::presentation
