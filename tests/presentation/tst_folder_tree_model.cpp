#include <piclens/presentation/folder_tree_model.h>

#include <QAbstractItemModel>
#include <QSignalSpy>
#include <QTest>

#include <atomic>
#include <condition_variable>
#include <mutex>
#include <stdexcept>

using namespace piclens::core;
using namespace piclens::presentation;

namespace {

FolderListItem folder(const QString &path)
{
    return {
        .path = path,
        .name = path.section(QLatin1Char('/'), -1),
        .modifiedAtMs = 0,
    };
}

QString roleString(const FolderTreeModel &model, const QModelIndex &index, int role)
{
    return model.data(index, role).toString();
}

bool roleBool(const FolderTreeModel &model, const QModelIndex &index, int role)
{
    return model.data(index, role).toBool();
}

} // namespace

class FolderTreeModelTests final : public QObject
{
    Q_OBJECT

private slots:
    void rootBuildPreservesRootAndSelectsCurrentChild();
    void pickerRootReplacementChangesDisplayedRoot();
    void lazyLoadRunsOnceAndAddsChildren();
    void rootSnapshotDoesNotStartDuplicateChildScan();
    void selectingUnloadedDescendantBuildsAncestorPath();
    void childScanFailureKeepsReadableRoot();
    void staleRootBuildCannotReplaceNewRoot();
    void exposesStableRoles();
};

void FolderTreeModelTests::rootBuildPreservesRootAndSelectsCurrentChild()
{
    const QString rootPath = QStringLiteral("workspace");
    const QString childPath = QStringLiteral("workspace/Child");
    FolderTreeModel model(
        [&](const QString &path, std::stop_token) {
            if (path == rootPath) {
                return QVector<FolderListItem>{folder(childPath)};
            }
            return QVector<FolderListItem>{};
        });

    model.setRoot(rootPath, childPath);
    QTRY_VERIFY_WITH_TIMEOUT(!model.busy(), 5000);

    QCOMPARE(model.rootPath(), rootPath);
    QCOMPARE(model.rowCount(), 1);
    const QModelIndex root = model.index(0, 0);
    const QModelIndex child = model.index(0, 0, root);
    QCOMPARE(roleString(model, root, FolderTreeModel::PathRole), rootPath);
    QVERIFY(roleBool(model, root, FolderTreeModel::ExpandedRole));
    QVERIFY(!roleBool(model, root, FolderTreeModel::SelectedRole));
    QCOMPARE(roleString(model, child, FolderTreeModel::PathRole), childPath);
    QVERIFY(roleBool(model, child, FolderTreeModel::ExpandedRole));
    QVERIFY(roleBool(model, child, FolderTreeModel::SelectedRole));

    model.selectPath(rootPath);
    QCOMPARE(model.rootPath(), rootPath);
    QVERIFY(roleBool(model, root, FolderTreeModel::SelectedRole));
    QVERIFY(!roleBool(model, child, FolderTreeModel::SelectedRole));
}

void FolderTreeModelTests::pickerRootReplacementChangesDisplayedRoot()
{
    FolderTreeModel model([](const QString &, std::stop_token) {
        return QVector<FolderListItem>{};
    });

    model.setRoot(QStringLiteral("first"), QStringLiteral("first"));
    QTRY_VERIFY_WITH_TIMEOUT(!model.busy(), 5000);
    model.setRoot(QStringLiteral("second"), QStringLiteral("second"));
    QTRY_VERIFY_WITH_TIMEOUT(!model.busy(), 5000);

    QCOMPARE(model.rootPath(), QStringLiteral("second"));
    QCOMPARE(roleString(model, model.index(0, 0), FolderTreeModel::PathRole), QStringLiteral("second"));
}

void FolderTreeModelTests::lazyLoadRunsOnceAndAddsChildren()
{
    const QString rootPath = QStringLiteral("workspace");
    const QString childPath = QStringLiteral("workspace/Child");
    int childScans = 0;
    FolderTreeModel model(
        [&](const QString &path, std::stop_token) {
            if (path == rootPath) {
                return QVector<FolderListItem>{folder(childPath)};
            }
            if (path == childPath) {
                ++childScans;
                return QVector<FolderListItem>{folder(childPath + QStringLiteral("/Grandchild"))};
            }
            return QVector<FolderListItem>{};
        });

    model.setRoot(rootPath, rootPath);
    QTRY_VERIFY_WITH_TIMEOUT(!model.busy(), 5000);
    const QModelIndex root = model.index(0, 0);
    const QModelIndex child = model.index(0, 0, root);
    QVERIFY(model.hasChildren(child));
    QVERIFY(!roleBool(model, child, FolderTreeModel::ChildrenLoadedRole));

    model.loadChildren(child);
    QTRY_VERIFY_WITH_TIMEOUT(roleBool(model, child, FolderTreeModel::ChildrenLoadedRole), 5000);
    QCOMPARE(model.rowCount(child), 1);
    QCOMPARE(childScans, 1);
    model.loadChildren(child);
    QTest::qWait(50);
    QCOMPARE(childScans, 1);
}

void FolderTreeModelTests::rootSnapshotDoesNotStartDuplicateChildScan()
{
    const QString rootPath = QStringLiteral("workspace");
    const QString childPath = QStringLiteral("workspace/Child");
    std::atomic_int rootScans = 0;
    FolderTreeModel model(
        [&](const QString &path, std::stop_token) {
            if (path == rootPath) {
                ++rootScans;
                QTest::qSleep(25);
                return QVector<FolderListItem>{folder(childPath)};
            }
            return QVector<FolderListItem>{};
        });

    model.setRoot(rootPath, rootPath);
    const QModelIndex placeholderRoot = model.index(0, 0);
    QVERIFY(roleBool(model, placeholderRoot, FolderTreeModel::LoadingRole));
    model.loadChildren(placeholderRoot);

    QTRY_VERIFY_WITH_TIMEOUT(!model.busy(), 5000);
    QTest::qWait(50);
    QCOMPARE(rootScans.load(), 1);
    const QModelIndex root = model.index(0, 0);
    QCOMPARE(model.rowCount(root), 1);
    QCOMPARE(roleString(model, model.index(0, 0, root), FolderTreeModel::PathRole), childPath);
}

void FolderTreeModelTests::selectingUnloadedDescendantBuildsAncestorPath()
{
    const QString rootPath = QStringLiteral("workspace");
    const QString childPath = QStringLiteral("workspace/Child");
    const QString grandchildPath = QStringLiteral("workspace/Child/Grandchild");
    FolderTreeModel model(
        [&](const QString &path, std::stop_token) {
            if (path == rootPath) {
                return QVector<FolderListItem>{folder(childPath)};
            }
            if (path == childPath) {
                return QVector<FolderListItem>{folder(grandchildPath)};
            }
            return QVector<FolderListItem>{};
        });

    model.setRoot(rootPath, rootPath);
    QTRY_VERIFY_WITH_TIMEOUT(!model.busy(), 5000);
    model.selectPath(grandchildPath);
    QTRY_VERIFY_WITH_TIMEOUT(!model.busy(), 5000);

    const QModelIndex root = model.index(0, 0);
    const QModelIndex child = model.index(0, 0, root);
    const QModelIndex grandchild = model.index(0, 0, child);
    QCOMPARE(roleString(model, grandchild, FolderTreeModel::PathRole), grandchildPath);
    QVERIFY(roleBool(model, child, FolderTreeModel::ExpandedRole));
    QVERIFY(roleBool(model, grandchild, FolderTreeModel::SelectedRole));
}

void FolderTreeModelTests::childScanFailureKeepsReadableRoot()
{
    FolderTreeModel model([](const QString &, std::stop_token) -> QVector<FolderListItem> {
        throw std::runtime_error("blocked");
    });
    QSignalSpy failures(&model, &FolderTreeModel::loadFailed);

    model.setRoot(QStringLiteral("workspace"), QStringLiteral("workspace"));
    QTRY_VERIFY_WITH_TIMEOUT(!model.busy(), 5000);

    QCOMPARE(model.rowCount(), 1);
    const QModelIndex root = model.index(0, 0);
    QCOMPARE(roleString(model, root, FolderTreeModel::PathRole), QStringLiteral("workspace"));
    QVERIFY(roleBool(model, root, FolderTreeModel::ReadableRole));
    QCOMPARE(failures.count(), 1);
    QCOMPARE(failures.first().at(0).toString(), QStringLiteral("workspace"));
    QCOMPARE(failures.first().at(1).toString(), QStringLiteral("blocked"));
}

void FolderTreeModelTests::staleRootBuildCannotReplaceNewRoot()
{
    struct State {
        std::mutex mutex;
        std::condition_variable condition;
        bool firstStarted = false;
        bool releaseFirst = false;
        bool firstFinished = false;
    } state;
    FolderTreeModel model(
        [&](const QString &path, std::stop_token) {
            if (path == QStringLiteral("first")) {
                std::unique_lock lock(state.mutex);
                state.firstStarted = true;
                state.condition.notify_all();
                state.condition.wait(lock, [&] { return state.releaseFirst; });
                state.firstFinished = true;
                return QVector<FolderListItem>{folder(QStringLiteral("first/Child"))};
            }
            return QVector<FolderListItem>{};
        });

    model.setRoot(QStringLiteral("first"), QStringLiteral("first"));
    QTRY_VERIFY_WITH_TIMEOUT(([&] {
        const std::scoped_lock lock(state.mutex);
        return state.firstStarted;
    })(), 5000);
    model.setRoot(QStringLiteral("second"), QStringLiteral("second"));
    QTRY_VERIFY_WITH_TIMEOUT(!model.busy(), 5000);
    QCOMPARE(model.rootPath(), QStringLiteral("second"));

    {
        const std::scoped_lock lock(state.mutex);
        state.releaseFirst = true;
    }
    state.condition.notify_all();
    QTRY_VERIFY_WITH_TIMEOUT(([&] {
        const std::scoped_lock lock(state.mutex);
        return state.firstFinished;
    })(), 5000);
    QTest::qWait(50);

    QCOMPARE(model.rootPath(), QStringLiteral("second"));
    QCOMPARE(roleString(model, model.index(0, 0), FolderTreeModel::PathRole), QStringLiteral("second"));
}

void FolderTreeModelTests::exposesStableRoles()
{
    FolderTreeModel model([](const QString &, std::stop_token) {
        return QVector<FolderListItem>{};
    });
    const auto roles = model.roleNames();

    QCOMPARE(roles.value(FolderTreeModel::NameRole), QByteArrayLiteral("treeLabel"));
    QCOMPARE(roles.value(FolderTreeModel::PathRole), QByteArrayLiteral("path"));
    QCOMPARE(roles.value(FolderTreeModel::ExpandedRole), QByteArrayLiteral("shouldExpand"));
    QCOMPARE(roles.value(FolderTreeModel::SelectedRole), QByteArrayLiteral("currentFolder"));
    QCOMPARE(roles.value(FolderTreeModel::ChildrenLoadedRole), QByteArrayLiteral("childrenLoaded"));
}

QTEST_GUILESS_MAIN(FolderTreeModelTests)

#include "tst_folder_tree_model.moc"
