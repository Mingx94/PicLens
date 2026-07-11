#include <piclens/presentation/folder_tree_model.h>

#include <piclens/core/path_rules.h>

#include <QDir>
#include <QFileInfo>
#include <QFutureWatcher>
#include <QtConcurrentRun>

#include <algorithm>
#include <exception>
#include <stdexcept>
#include <utility>
#include <vector>

namespace piclens::presentation {
namespace {

struct SnapshotNode {
    QString name;
    QString path;
    bool readable = true;
    bool expanded = false;
    bool selected = false;
    bool childrenLoaded = false;
    QVector<SnapshotNode> children;
};

struct SnapshotResult {
    SnapshotNode root;
    QString failedPath;
    QString errorDetails;
    bool canceled = false;
};

struct ChildScanResult {
    QVector<core::FolderListItem> children;
    QString errorDetails;
    bool canceled = false;
};

QString displayName(const QString &path)
{
    const QString name = QFileInfo(path).fileName();
    return name.isEmpty() ? path : name;
}

bool isAncestorOrEqual(const QString &ancestorPath, const QString &childPath)
{
    if (core::path_rules::pathEquals(ancestorPath, childPath)) {
        return true;
    }
    QString prefix = QDir::cleanPath(QFileInfo(ancestorPath).absoluteFilePath());
    const QString child = QDir::cleanPath(QFileInfo(childPath).absoluteFilePath());
    if (!prefix.endsWith(QLatin1Char('/'))) {
        prefix.append(QLatin1Char('/'));
    }
    return child.startsWith(prefix, core::path_rules::pathCaseSensitivity());
}

SnapshotNode buildSnapshotNode(
    const QString &name,
    const QString &path,
    const QString &selectedPath,
    const FolderTreeModel::ScanChildrenFunction &scanChildren,
    std::stop_token stopToken,
    QString &failedPath,
    QString &errorDetails)
{
    SnapshotNode node{
        .name = name,
        .path = path,
        .readable = true,
        .expanded = isAncestorOrEqual(path, selectedPath),
        .selected = core::path_rules::pathEquals(path, selectedPath),
        .childrenLoaded = false,
        .children = {},
    };
    if (stopToken.stop_requested()) {
        throw std::runtime_error("Folder tree load canceled.");
    }

    QVector<core::FolderListItem> folders;
    try {
        folders = scanChildren(path, stopToken);
    } catch (const std::exception &exception) {
        if (stopToken.stop_requested()) {
            throw;
        }
        if (errorDetails.isEmpty()) {
            failedPath = path;
            errorDetails = QString::fromUtf8(exception.what());
        }
        return node;
    }

    node.childrenLoaded = true;
    node.children.reserve(folders.size());
    for (const auto &folder : folders) {
        if (stopToken.stop_requested()) {
            throw std::runtime_error("Folder tree load canceled.");
        }
        const bool expandChild = isAncestorOrEqual(folder.path, selectedPath);
        if (expandChild) {
            node.children.append(buildSnapshotNode(
                folder.name,
                folder.path,
                selectedPath,
                scanChildren,
                stopToken,
                failedPath,
                errorDetails));
        } else {
            node.children.append({
                .name = folder.name,
                .path = folder.path,
                .readable = true,
                .expanded = false,
                .selected = false,
                .childrenLoaded = false,
                .children = {},
            });
        }
    }
    return node;
}

} // namespace

struct FolderTreeModel::Node {
    QString name;
    QString path;
    bool readable = true;
    bool expanded = false;
    bool selected = false;
    bool loading = false;
    bool childrenLoaded = false;
    Node *parent = nullptr;
    std::vector<std::unique_ptr<Node>> children;
};

namespace {

std::unique_ptr<FolderTreeModel::Node> createNode(
    SnapshotNode snapshot,
    FolderTreeModel::Node *parent)
{
    auto node = std::make_unique<FolderTreeModel::Node>();
    node->name = std::move(snapshot.name);
    node->path = std::move(snapshot.path);
    node->readable = snapshot.readable;
    node->expanded = snapshot.expanded;
    node->selected = snapshot.selected;
    node->childrenLoaded = snapshot.childrenLoaded;
    node->parent = parent;
    node->children.reserve(snapshot.children.size());
    for (auto &child : snapshot.children) {
        node->children.push_back(createNode(std::move(child), node.get()));
    }
    return node;
}

FolderTreeModel::Node *findNodeRecursive(
    FolderTreeModel::Node *node,
    const QString &path)
{
    if (!node) {
        return nullptr;
    }
    if (core::path_rules::pathEquals(node->path, path)) {
        return node;
    }
    for (const auto &child : node->children) {
        if (auto *match = findNodeRecursive(child.get(), path)) {
            return match;
        }
    }
    return nullptr;
}

} // namespace

FolderTreeModel::FolderTreeModel(ScanChildrenFunction scanChildren, QObject *parent)
    : QAbstractItemModel(parent)
    , m_scanChildren(std::move(scanChildren))
{
    if (!m_scanChildren) {
        throw std::invalid_argument("Folder tree scanner is required.");
    }
    m_workerPool.setMaxThreadCount(2);
    m_workerPool.setExpiryTimeout(30'000);
}

FolderTreeModel::~FolderTreeModel()
{
    if (m_activeStop) {
        m_activeStop->request_stop();
    }
    m_workerPool.waitForDone();
}

QModelIndex FolderTreeModel::index(
    int row,
    int column,
    const QModelIndex &parentIndex) const
{
    if (column != 0 || row < 0) {
        return {};
    }
    if (!parentIndex.isValid()) {
        return m_root && row == 0 ? createIndex(0, 0, m_root.get()) : QModelIndex{};
    }
    Node *parentNode = nodeFromIndex(parentIndex);
    return parentNode && static_cast<std::size_t>(row) < parentNode->children.size()
        ? createIndex(row, 0, parentNode->children.at(row).get())
        : QModelIndex{};
}

QModelIndex FolderTreeModel::parent(const QModelIndex &childIndex) const
{
    Node *child = nodeFromIndex(childIndex);
    if (!child || !child->parent) {
        return {};
    }
    return indexForNode(child->parent);
}

int FolderTreeModel::rowCount(const QModelIndex &parentIndex) const
{
    if (parentIndex.column() > 0) {
        return 0;
    }
    if (!parentIndex.isValid()) {
        return m_root ? 1 : 0;
    }
    Node *parentNode = nodeFromIndex(parentIndex);
    return parentNode ? static_cast<int>(parentNode->children.size()) : 0;
}

int FolderTreeModel::columnCount(const QModelIndex &) const
{
    return 1;
}

QVariant FolderTreeModel::data(const QModelIndex &modelIndex, int role) const
{
    Node *node = nodeFromIndex(modelIndex);
    if (!node) {
        return {};
    }
    switch (role) {
    case NameRole:
    case Qt::DisplayRole:
        return node->name;
    case PathRole:
        return node->path;
    case ReadableRole:
        return node->readable;
    case ExpandedRole:
        return node->expanded;
    case SelectedRole:
        return node->selected;
    case LoadingRole:
        return node->loading;
    case ChildrenLoadedRole:
        return node->childrenLoaded;
    default:
        return {};
    }
}

QHash<int, QByteArray> FolderTreeModel::roleNames() const
{
    return {
        {NameRole, QByteArrayLiteral("treeLabel")},
        {PathRole, QByteArrayLiteral("path")},
        {ReadableRole, QByteArrayLiteral("readable")},
        {ExpandedRole, QByteArrayLiteral("shouldExpand")},
        {SelectedRole, QByteArrayLiteral("currentFolder")},
        {LoadingRole, QByteArrayLiteral("loading")},
        {ChildrenLoadedRole, QByteArrayLiteral("childrenLoaded")},
    };
}

bool FolderTreeModel::hasChildren(const QModelIndex &parentIndex) const
{
    if (!parentIndex.isValid()) {
        return m_root != nullptr;
    }
    Node *node = nodeFromIndex(parentIndex);
    return node && node->readable && (!node->childrenLoaded || !node->children.empty());
}

QString FolderTreeModel::rootPath() const
{
    return m_rootPath;
}

bool FolderTreeModel::busy() const
{
    return m_busy;
}

void FolderTreeModel::setRoot(const QString &rootPath, const QString &selectedPath)
{
    rebuildRoot(rootPath, selectedPath);
}

void FolderTreeModel::selectPath(const QString &selectedPath)
{
    m_selectedPath = selectedPath;
    if (!m_root) {
        return;
    }
    if (!findNode(selectedPath) && isAncestorOrEqual(m_rootPath, selectedPath)) {
        rebuildRoot(m_rootPath, selectedPath);
        return;
    }
    updateSelection(m_root.get(), selectedPath);
}

void FolderTreeModel::loadChildren(const QModelIndex &parentIndex)
{
    Node *node = nodeFromIndex(parentIndex);
    if (!node || node->loading || node->childrenLoaded || !node->readable) {
        return;
    }
    node->loading = true;
    emit dataChanged(parentIndex, parentIndex, {LoadingRole});

    const quint64 generation = m_generation;
    const QString path = node->path;
    const auto stop = m_activeStop;
    const ScanChildrenFunction scanChildren = m_scanChildren;
    auto *watcher = new QFutureWatcher<ChildScanResult>(this);
    connect(watcher, &QFutureWatcher<ChildScanResult>::finished, this, [this, watcher, generation, path] {
        ChildScanResult result = watcher->result();
        watcher->deleteLater();
        if (generation != m_generation) {
            return;
        }
        Node *current = findNode(path);
        if (!current) {
            return;
        }
        const QModelIndex currentIndex = indexForNode(current);
        current->loading = false;
        if (result.canceled) {
            emit dataChanged(currentIndex, currentIndex, {LoadingRole});
            return;
        }
        if (!result.errorDetails.isEmpty()) {
            emit dataChanged(currentIndex, currentIndex, {LoadingRole});
            emit loadFailed(path, result.errorDetails);
            return;
        }

        if (!result.children.isEmpty()) {
            beginInsertRows(currentIndex, 0, result.children.size() - 1);
            current->children.reserve(result.children.size());
            for (const auto &folder : result.children) {
                auto child = std::make_unique<Node>();
                child->name = folder.name;
                child->path = folder.path;
                child->parent = current;
                current->children.push_back(std::move(child));
            }
            endInsertRows();
        }
        current->childrenLoaded = true;
        emit dataChanged(currentIndex, currentIndex, {LoadingRole, ChildrenLoadedRole});
    });

    watcher->setFuture(QtConcurrent::run(&m_workerPool, [scanChildren, path, stop] {
        try {
            return ChildScanResult{
                .children = scanChildren(path, stop->get_token()),
                .errorDetails = {},
                .canceled = false,
            };
        } catch (const std::exception &exception) {
            return ChildScanResult{
                .children = {},
                .errorDetails = QString::fromUtf8(exception.what()),
                .canceled = stop->stop_requested(),
            };
        }
    }));
}

void FolderTreeModel::clear()
{
    if (m_activeStop) {
        m_activeStop->request_stop();
    }
    ++m_generation;
    beginResetModel();
    m_root.reset();
    m_rootPath.clear();
    m_selectedPath.clear();
    endResetModel();
    setBusy(false);
    emit rootPathChanged();
}

FolderTreeModel::Node *FolderTreeModel::nodeFromIndex(const QModelIndex &modelIndex) const
{
    return modelIndex.isValid() ? static_cast<Node *>(modelIndex.internalPointer()) : nullptr;
}

QModelIndex FolderTreeModel::indexForNode(const Node *node) const
{
    if (!node) {
        return {};
    }
    if (!node->parent) {
        return createIndex(0, 0, const_cast<Node *>(node));
    }
    const auto &siblings = node->parent->children;
    const auto iterator = std::find_if(siblings.cbegin(), siblings.cend(), [&](const auto &candidate) {
        return candidate.get() == node;
    });
    return iterator == siblings.cend()
        ? QModelIndex{}
        : createIndex(static_cast<int>(std::distance(siblings.cbegin(), iterator)), 0, const_cast<Node *>(node));
}

FolderTreeModel::Node *FolderTreeModel::findNode(const QString &path) const
{
    return findNodeRecursive(m_root.get(), path);
}

void FolderTreeModel::rebuildRoot(const QString &rootPath, const QString &selectedPath)
{
    if (m_activeStop) {
        m_activeStop->request_stop();
    }
    const quint64 generation = ++m_generation;
    auto stop = std::make_shared<std::stop_source>();
    m_activeStop = stop;
    m_rootPath = rootPath;
    m_selectedPath = selectedPath;

    beginResetModel();
    m_root = std::make_unique<Node>();
    m_root->name = displayName(rootPath);
    m_root->path = rootPath;
    m_root->expanded = true;
    m_root->selected = core::path_rules::pathEquals(rootPath, selectedPath);
    endResetModel();
    emit rootPathChanged();
    setBusy(true);

    const ScanChildrenFunction scanChildren = m_scanChildren;
    auto *watcher = new QFutureWatcher<SnapshotResult>(this);
    connect(watcher, &QFutureWatcher<SnapshotResult>::finished, this, [this, watcher, generation] {
        SnapshotResult result = watcher->result();
        watcher->deleteLater();
        if (generation != m_generation) {
            return;
        }
        setBusy(false);
        if (result.canceled) {
            return;
        }
        beginResetModel();
        m_root = createNode(std::move(result.root), nullptr);
        endResetModel();
        if (!result.errorDetails.isEmpty()) {
            emit loadFailed(result.failedPath, result.errorDetails);
        }
    });

    watcher->setFuture(QtConcurrent::run(&m_workerPool, [scanChildren, rootPath, selectedPath, stop] {
        QString failedPath;
        QString errorDetails;
        try {
            SnapshotNode root = buildSnapshotNode(
                displayName(rootPath),
                rootPath,
                selectedPath,
                scanChildren,
                stop->get_token(),
                failedPath,
                errorDetails);
            root.expanded = true;
            return SnapshotResult{
                .root = std::move(root),
                .failedPath = std::move(failedPath),
                .errorDetails = std::move(errorDetails),
                .canceled = false,
            };
        } catch (const std::exception &exception) {
            return SnapshotResult{
                .root = {
                    .name = displayName(rootPath),
                    .path = rootPath,
                    .readable = true,
                    .expanded = true,
                    .selected = core::path_rules::pathEquals(rootPath, selectedPath),
                    .childrenLoaded = false,
                    .children = {},
                },
                .failedPath = rootPath,
                .errorDetails = QString::fromUtf8(exception.what()),
                .canceled = stop->stop_requested(),
            };
        }
    }));
}

void FolderTreeModel::updateSelection(Node *node, const QString &selectedPath)
{
    if (!node) {
        return;
    }
    const bool selected = core::path_rules::pathEquals(node->path, selectedPath);
    const bool expand = isAncestorOrEqual(node->path, selectedPath);
    const bool changed = node->selected != selected || (expand && !node->expanded);
    node->selected = selected;
    if (expand) {
        node->expanded = true;
    }
    if (changed) {
        const QModelIndex modelIndex = indexForNode(node);
        emit dataChanged(modelIndex, modelIndex, {SelectedRole, ExpandedRole});
    }
    for (const auto &child : node->children) {
        updateSelection(child.get(), selectedPath);
    }
}

void FolderTreeModel::setBusy(bool busy)
{
    if (m_busy == busy) {
        return;
    }
    m_busy = busy;
    emit busyChanged();
}

} // namespace piclens::presentation
