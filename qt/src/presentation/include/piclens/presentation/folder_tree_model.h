#pragma once

#include <piclens/core/models.h>

#include <QAbstractItemModel>
#include <QThreadPool>

#include <functional>
#include <memory>
#include <stop_token>

namespace piclens::presentation {

class FolderTreeModel final : public QAbstractItemModel
{
    Q_OBJECT
    Q_PROPERTY(QString rootPath READ rootPath NOTIFY rootPathChanged)
    Q_PROPERTY(bool busy READ busy NOTIFY busyChanged)

public:
    struct Node;

    using ScanChildrenFunction = std::function<QVector<core::FolderListItem>(const QString &, std::stop_token)>;

    enum Role {
        NameRole = Qt::UserRole + 1,
        PathRole,
        ReadableRole,
        ExpandedRole,
        SelectedRole,
        LoadingRole,
        ChildrenLoadedRole,
    };
    Q_ENUM(Role)

    explicit FolderTreeModel(ScanChildrenFunction scanChildren, QObject *parent = nullptr);
    ~FolderTreeModel() override;

    [[nodiscard]] QModelIndex index(
        int row,
        int column,
        const QModelIndex &parent = {}) const override;
    [[nodiscard]] QModelIndex parent(const QModelIndex &child) const override;
    [[nodiscard]] int rowCount(const QModelIndex &parent = {}) const override;
    [[nodiscard]] int columnCount(const QModelIndex &parent = {}) const override;
    [[nodiscard]] QVariant data(const QModelIndex &index, int role) const override;
    [[nodiscard]] QHash<int, QByteArray> roleNames() const override;
    [[nodiscard]] bool hasChildren(const QModelIndex &parent = {}) const override;

    [[nodiscard]] QString rootPath() const;
    [[nodiscard]] bool busy() const;

    void setRoot(const QString &rootPath, const QString &selectedPath);
    void selectPath(const QString &selectedPath);
    Q_INVOKABLE void loadChildren(const QModelIndex &parentIndex);
    void clear();

signals:
    void rootPathChanged();
    void busyChanged();
    void loadFailed(const QString &folderPath, const QString &details);

private:
    [[nodiscard]] Node *nodeFromIndex(const QModelIndex &index) const;
    [[nodiscard]] QModelIndex indexForNode(const Node *node) const;
    [[nodiscard]] Node *findNode(const QString &path) const;
    void rebuildRoot(const QString &rootPath, const QString &selectedPath);
    void updateSelection(Node *node, const QString &selectedPath);
    void setBusy(bool busy);

    ScanChildrenFunction m_scanChildren;
    std::unique_ptr<Node> m_root;
    QString m_rootPath;
    QString m_selectedPath;
    bool m_busy = false;
    quint64 m_generation = 0;
    std::shared_ptr<std::stop_source> m_activeStop;
    QThreadPool m_workerPool;
};

} // namespace piclens::presentation
