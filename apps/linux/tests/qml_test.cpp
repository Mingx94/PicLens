#include "controller.h"
#include "imageitem.h"

#include <QAbstractItemModel>
#include <QAccessible>
#include <QColor>
#include <QDir>
#include <QFileInfo>
#include <QQmlApplicationEngine>
#include <QQmlContext>
#include <QQuickItem>
#include <QQuickStyle>
#include <QQuickWindow>
#include <QSignalSpy>
#include <QTemporaryDir>
#include <QTest>
#include <QUrl>
#include <functional>

using namespace piclens;

namespace {

template <typename Predicate>
QObject *findObject(QObject *root, Predicate predicate)
{
    if (predicate(root))
        return root;
    for (QObject *object : root->findChildren<QObject *>())
        if (predicate(object))
            return object;
    return nullptr;
}

template <typename Predicate>
QQuickItem *findItem(QObject *root, Predicate predicate)
{
    return qobject_cast<QQuickItem *>(findObject(root, [predicate](QObject *object) {
        auto *item = qobject_cast<QQuickItem *>(object);
        return item && predicate(item);
    }));
}

template <typename Predicate>
QQuickItem *findVisualItem(QQuickItem *root, Predicate predicate)
{
    if (predicate(root))
        return root;
    for (QQuickItem *child : root->childItems()) {
        if (auto *result = findVisualItem(child, predicate))
            return result;
    }
    return nullptr;
}

QObject *findTextObject(QObject *root, const QString &text)
{
    return findObject(root, [&text](QObject *object) {
        return object->property("text").toString() == text;
    });
}

QVariant modelProperty(const QQuickItem *item)
{
    return item ? item->property("model") : QVariant();
}

QQuickItem *findGrid(QObject *root, QObject *model)
{
    return findItem(root, [model](QQuickItem *item) {
        const auto value = modelProperty(item);
        return item->property("reuseItems").isValid()
            && item->property("cacheBuffer").isValid()
            && value.canConvert<QObject *>()
            && value.value<QObject *>() == model;
    });
}

QQuickItem *findListView(QObject *root, QObject *model)
{
    return findItem(root, [model](QQuickItem *item) {
        const auto value = modelProperty(item);
        return item->property("contentY").isValid()
            && value.canConvert<QObject *>()
            && value.value<QObject *>() == model;
    });
}

QQuickItem *findGallery(QObject *root)
{
    return findItem(root, [](QQuickItem *item) {
        return item->property("materialized").isValid()
            && item->property("dragging").isValid();
    });
}

QQuickItem *findSearchField(QObject *root)
{
    return findItem(root, [](QQuickItem *item) {
        return item->property("placeholderText").toString() == QStringLiteral("搜尋檔名…");
    });
}

QQuickItem *findDelegate(QObject *root, const QString &path)
{
    auto *window = qobject_cast<QQuickWindow *>(root);
    return window ? findVisualItem(window->contentItem(), [&path](QQuickItem *item) {
        return item->property("pooled").isValid()
            && item->property("path").toString() == path;
    }) : nullptr;
}

int delegateCount(QObject *root)
{
    auto *window = qobject_cast<QQuickWindow *>(root);
    if (!window)
        return 0;
    int count = 0;
    std::function<void(QQuickItem *)> visit = [&count, &visit](QQuickItem *item) {
        if (item->property("pooled").isValid() && item->property("path").isValid())
            ++count;
        for (QQuickItem *child : item->childItems())
            visit(child);
    };
    visit(window->contentItem());
    return count;
}

QPoint itemCenter(QQuickItem *item, QQuickWindow *window)
{
    const auto point = item->mapToItem(window->contentItem(),
                                      QPointF(item->width() / 2.0, item->height() / 2.0));
    return point.toPoint();
}

class Harness final {
public:
    explicit Harness(bool components = false, int extraImages = 0)
        : engine(), provider(new ThumbProvider),
          controller(temp.filePath("profile"), QString::fromUtf8(PICLENS_WORKER), provider)
    {
        library = temp.filePath("library");
        ready_ = QDir().mkpath(library + "/子資料夾") && writeFixtures(extraImages);

        engine.addImageProvider("thumb", provider);
        engine.rootContext()->setContextProperty("app", &controller);
        engine.rootContext()->setContextProperty("launchWidth", 900);
        engine.rootContext()->setContextProperty("launchHeight", 700);
        engine.rootContext()->setContextProperty("showComponents", components);
    }

    ~Harness() { controller.shutdown(); }

    bool load()
    {
        if (!ready_)
            return false;
        engine.load(QUrl(QStringLiteral("qrc:/qml/Main.qml")));
        if (engine.rootObjects().isEmpty())
            return false;
        window = qobject_cast<QQuickWindow *>(engine.rootObjects().constFirst());
        if (!window)
            return false;
        window->show();
        QCoreApplication::processEvents();
        return true;
    }

    QString path(const QString &name) const { return library + "/" + name; }

    int row(const QString &filePath)
    {
        auto *model = qobject_cast<Rows *>(controller.library());
        if (!model)
            return -1;
        for (int i = 0; i < model->rowCount(); ++i)
            if (model->get(i).value("path").toString() == filePath)
                return i;
        return -1;
    }

    bool filesReady() const { return ready_; }

    QTemporaryDir temp;
    ThumbProvider *provider = nullptr;
    Controller controller;
    QQmlApplicationEngine engine;
    QQuickWindow *window = nullptr;
    QString library;

private:
    bool writeImage(const QString &name, const QColor &a, const QColor &b,
                    const char *format = "PNG")
    {
        QImage image(120, 90, QImage::Format_RGBA8888);
        image.fill(a);
        image.setPixelColor(90, 20, b);
        image.setPixelColor(91, 20, b);
        return image.save(path(name), format);
    }

    bool writeFixtures(int extraImages)
    {
        bool ok = true;
        ok = writeImage("alpha.png", QColor("#e11d48"), QColor("#22c55e")) && ok;
        ok = writeImage("beta.png", QColor("#2563eb"), QColor("#f59e0b")) && ok;
        ok = writeImage("gamma.png", QColor("#7c3aed"), QColor("#14b8a6")) && ok;
        ok = writeImage("source.png", QColor("#be123c"), QColor("#84cc16")) && ok;
        ok = writeImage("target.png", QColor("#1d4ed8"), QColor("#facc15")) && ok;
        ok = writeImage("target-01.jpg", QColor("#0f766e"), QColor("#fb7185"), "JPG") && ok;
        ok = writeImage("target-02.png", QColor("#9333ea"), QColor("#38bdf8")) && ok;
        ok = writeImage("子資料夾/nested.png", QColor("#475569"), QColor("#f97316")) && ok;
        for (int i = 0; i < extraImages; ++i)
            ok = writeImage(QStringLiteral("zz-scroll-%1.png").arg(i, 2, 10, QLatin1Char('0')),
                            QColor("#334155"), QColor("#a3e635")) && ok;
        return ok;
    }

    bool ready_ = false;
};

void waitForGallery(Harness &h, int expectedCount)
{
    QTRY_COMPARE_WITH_TIMEOUT(h.controller.count(), expectedCount, 10000);
    QTRY_VERIFY_WITH_TIMEOUT(findGrid(h.window, h.controller.library()) != nullptr, 5000);
}

} // namespace

class QmlIntegrationTest final : public QObject {
    Q_OBJECT

private slots:
    void initTestCase()
    {
        QQuickStyle::setStyle(QStringLiteral("Basic"));
        qmlRegisterType<ImageItem>("PicLens.Native", 1, 0, "ImageItem");
    }

    void entrySettingsAndNarrowLayout()
    {
        Harness h;
        QVERIFY(h.filesReady());
        QVERIFY(h.load());
        QVERIFY(h.window->minimumWidth() == 800);
        QVERIFY(h.window->minimumHeight() == 600);
        QCOMPARE(h.window->width(), 900);
        QCOMPARE(h.window->height(), 700);
        auto *openFolder = findObject(h.window, [](QObject *object) {
            return object->objectName() == QStringLiteral("openFolderButton");
        });
        QVERIFY(openFolder);
        QVERIFY(findTextObject(h.window, QStringLiteral("開啟資料夾")) != nullptr);
        if (auto *accessible = QAccessible::queryAccessibleInterface(openFolder)) {
            QCOMPARE(accessible->role(), QAccessible::PushButton);
            QVERIFY(!accessible->text(QAccessible::Name).isEmpty());
            QVERIFY(!accessible->state().disabled);
        }

        h.controller.start(h.library);
        waitForGallery(h, 8);

        auto *grid = findGrid(h.window, h.controller.library());
        auto *tree = findListView(h.window, h.controller.tree());
        QVERIFY(grid);
        QVERIFY(tree);
        QVERIFY(grid->property("reuseItems").toBool());
        QVERIFY(grid->property("cacheBuffer").toReal() >= 0);
        QVERIFY(grid->property("cacheBuffer").toReal() <= 600);

        h.controller.setThumbnailSize(240);
        QTRY_VERIFY_WITH_TIMEOUT(qFuzzyCompare(grid->property("cellWidth").toReal(), 256.0), 2000);
        h.controller.setThumbnailSize(120);
        QTRY_VERIFY_WITH_TIMEOUT(qFuzzyCompare(grid->property("cellWidth").toReal(), 136.0), 2000);

        h.controller.setSidebarCollapsed(true);
        QTRY_VERIFY_WITH_TIMEOUT(!tree->isVisible(), 2000);
        h.controller.setSidebarCollapsed(false);
        QTRY_VERIFY_WITH_TIMEOUT(tree->isVisible(), 2000);

        h.window->resize(800, 600);
        QTRY_COMPARE(h.window->width(), 800);
        QTRY_COMPARE(h.window->height(), 600);
        QVERIFY(grid->width() > 0);
        QVERIFY(grid->height() > 0);
    }

    void largeModelUsesBoundedRealDelegates()
    {
        Harness h;
        QVERIFY(h.filesReady());
        QVERIFY(h.load());
        auto *model = h.controller.library();
        QSignalSpy resets(model, &QAbstractItemModel::modelReset);

        h.controller.diagnose(10000);
        QTRY_COMPARE_WITH_TIMEOUT(h.controller.count(), 10000, 5000);
        QCOMPARE(resets.count(), 1);
        auto *grid = findGrid(h.window, model);
        QVERIFY(grid);
        QTRY_VERIFY_WITH_TIMEOUT(delegateCount(h.window) > 0, 5000);
        QVERIFY(delegateCount(h.window) < 250);
        QVERIFY(grid->property("contentHeight").toReal() > grid->height());

        grid->setProperty("contentY", grid->property("contentHeight").toReal() - grid->height());
        QTest::qWait(180);
        h.window->resize(800, 600);
        QTest::qWait(180);
        grid = findGrid(h.window, model);
        QVERIFY(grid);
        QVERIFY(grid->property("cacheBuffer").toReal() <= 600);
        QVERIFY(delegateCount(h.window) > 0);
        QVERIFY(delegateCount(h.window) < 250);
        QVERIFY(h.controller.metrics().value("maxMaterialized").toInt() < 250);
    }

    void searchSelectionAndContextMenuUseActualDelegates()
    {
        Harness h;
        QVERIFY(h.filesReady());
        QVERIFY(h.load());
        h.controller.start(h.library);
        waitForGallery(h, 8);
        auto *grid = findGrid(h.window, h.controller.library());
        auto *search = findSearchField(h.window);
        QVERIFY(grid);
        QVERIFY(search);

        h.controller.setSearch(QStringLiteral("alpha"));
        grid->forceActiveFocus();
        QTest::keyClick(h.window, Qt::Key_F, Qt::ControlModifier);
        QTRY_VERIFY_WITH_TIMEOUT(search->hasActiveFocus(), 2000);
        QTRY_COMPARE(search->property("selectedText").toString(), QStringLiteral("alpha"));
        for (const auto ch : QStringLiteral("beta"))
            QTest::keyClick(h.window, ch.toLatin1());
        QTRY_COMPARE(h.controller.search(), QStringLiteral("beta"));

        const int treeRows = h.controller.tree()->rowCount();
        h.controller.setSearch(h.path("alpha.png"));
        QTRY_COMPARE(h.controller.count(), 1);
        QCOMPARE(h.controller.rootPath(), h.library);
        QCOMPARE(h.controller.tree()->rowCount(), treeRows);
        search->forceActiveFocus();
        h.controller.setSearch(QString());
        QTRY_COMPARE(h.controller.count(), 8);
        QVERIFY(search->hasActiveFocus());

        const auto alpha = h.path("alpha.png");
        const auto beta = h.path("beta.png");
        const auto gamma = h.path("gamma.png");
        const auto folder = h.path("子資料夾");
        QTRY_VERIFY_WITH_TIMEOUT(findDelegate(h.window, alpha)
                                 && findDelegate(h.window, beta)
                                 && findDelegate(h.window, gamma)
                                 && findDelegate(h.window, folder), 3000);
        QTest::mouseClick(h.window, Qt::LeftButton, Qt::NoModifier,
                          itemCenter(findDelegate(h.window, alpha), h.window));
        QTRY_COMPARE(h.controller.selectionCount(), 1);
        QTest::mouseClick(h.window, Qt::LeftButton, Qt::ControlModifier,
                          itemCenter(findDelegate(h.window, beta), h.window));
        QTRY_COMPARE(h.controller.selectionCount(), 2);

        QTest::mouseClick(h.window, Qt::RightButton, Qt::NoModifier,
                          itemCenter(findDelegate(h.window, beta), h.window));
        QTRY_COMPARE(h.controller.selectionCount(), 2);
        QTest::keyClick(h.window, Qt::Key_Escape);
        QTest::mouseClick(h.window, Qt::RightButton, Qt::NoModifier,
                          itemCenter(findDelegate(h.window, gamma), h.window));
        QTRY_COMPARE(h.controller.selectionCount(), 1);
        QVERIFY(qobject_cast<Rows *>(h.controller.library())->get(h.row(gamma))["selected"].toBool());
        QTest::keyClick(h.window, Qt::Key_Escape);

        QTest::mouseClick(h.window, Qt::RightButton, Qt::NoModifier,
                          itemCenter(findDelegate(h.window, folder), h.window));
        QTRY_COMPARE(h.controller.selectionCount(), 1);
        auto *rename = findTextObject(h.window, QStringLiteral("重新命名"));
        auto *trash = findTextObject(h.window, QStringLiteral("移到回收筒"));
        QVERIFY(rename);
        QVERIFY(trash);
        QVERIFY(!rename->property("enabled").toBool());
        QVERIFY(!trash->property("enabled").toBool());
    }

    void resetClearsSelectionAndThumbnailBindings()
    {
        Harness h(false, 40);
        QVERIFY(h.filesReady());
        QVERIFY(h.load());
        h.controller.start(h.library);
        waitForGallery(h, 48);
        auto *model = qobject_cast<Rows *>(h.controller.library());
        QVERIFY(model);
        const auto alpha = h.path("alpha.png");
        const int alphaRow = h.row(alpha);
        QVERIFY(alphaRow >= 0);

        h.controller.setVisible({alpha});
        QTRY_VERIFY_WITH_TIMEOUT(!model->get(alphaRow).value("imageKey").toString().isEmpty(), 10000);
        h.controller.select(alphaRow, 0);
        QCOMPARE(h.controller.selectionCount(), 1);
        auto *grid = findGrid(h.window, h.controller.library());
        QVERIFY(grid);
        grid->setProperty("contentY", grid->property("contentHeight").toReal() - grid->height());
        QTRY_VERIFY_WITH_TIMEOUT(findDelegate(h.window, alpha) == nullptr, 3000);
        QTest::qWait(180);
        QVERIFY(model->get(alphaRow).value("imageKey").toString().isEmpty());
        h.controller.setVisible({});
        QTRY_VERIFY_WITH_TIMEOUT(model->get(alphaRow).value("imageKey").toString().isEmpty(), 2000);

        h.controller.setSearch(QStringLiteral("alpha"));
        QTRY_COMPARE(h.controller.count(), 1);
        QCOMPARE(h.controller.selectionCount(), 0);
        QVERIFY(model->get(0).value("imageKey").toString().isEmpty());
        h.controller.setSearch(QString());
        QTRY_COMPARE(h.controller.count(), 48);
        for (int row = 0; row < model->rowCount(); ++row)
            QVERIFY(model->get(row).value("selected").toBool() == false);
    }

    void dragDropUsesThresholdPreviewAndCancelWithoutMutation()
    {
        Harness h(false, 24);
        QVERIFY(h.filesReady());
        QVERIFY(h.load());
        h.controller.start(h.library);
        waitForGallery(h, 32);
        auto *grid = findGrid(h.window, h.controller.library());
        auto *gallery = findGallery(h.window);
        QVERIFY(grid);
        QVERIFY(gallery);

        const auto source = h.path("source.png");
        const auto target = h.path("target.png");
        const auto alpha = h.path("alpha.png");
        grid->setProperty("contentY", 0);
        QTest::qWait(100);
        auto *alphaDelegate = findDelegate(h.window, alpha);
        QVERIFY(alphaDelegate);
        QTest::mouseClick(h.window, Qt::LeftButton, Qt::NoModifier,
                          itemCenter(alphaDelegate, h.window));
        QTRY_COMPARE(h.controller.selectionCount(), 1);

        grid->setProperty("contentY", 140);
        QTest::qWait(100);
        auto *sourceDelegate = findDelegate(h.window, source);
        QVERIFY(sourceDelegate);
        auto *targetDelegate = findDelegate(h.window, target);
        QVERIFY(targetDelegate);
        const auto sourcePoint = itemCenter(sourceDelegate, h.window);
        const auto targetPoint = itemCenter(targetDelegate, h.window);

        QTest::mouseClick(h.window, Qt::LeftButton, Qt::ControlModifier, sourcePoint);
        QTRY_COMPARE(h.controller.selectionCount(), 2);
        QTest::mousePress(h.window, Qt::LeftButton, Qt::NoModifier, sourcePoint);
        QTest::mouseMove(h.window, sourcePoint + QPoint(32, 32));
        QTRY_VERIFY_WITH_TIMEOUT(gallery->property("dragging").toBool(), 1000);
        QTest::mouseMove(h.window, targetPoint);
        QTest::qWait(50);
        auto *dropArea = findVisualItem(targetDelegate, [](QQuickItem *item) {
            return item->property("containsDrag").isValid();
        });
        QVERIFY(dropArea);
        QVERIFY(dropArea->property("enabled").toBool());
        QVERIFY(QMetaObject::invokeMethod(gallery, "endDrag", Qt::DirectConnection,
                                           Q_ARG(QVariant, QVariant(false))));
        QTest::mouseRelease(h.window, Qt::LeftButton, Qt::NoModifier, targetPoint);
        h.controller.dropRename(target);

        QTRY_VERIFY_WITH_TIMEOUT(h.controller.confirmOpen(), 5000);
        QVERIFY(h.controller.confirmText().contains(source));
        QVERIFY(h.controller.confirmText().contains(h.path("target-03.png")));
        QVERIFY(h.controller.confirmText().contains(h.path("alpha.png")));
        QVERIFY(h.controller.confirmText().contains(h.path("target-04.png")));
        QVERIFY(QFileInfo::exists(source));
        QVERIFY(QFileInfo::exists(target));
        h.controller.confirm(false);
        QVERIFY(!h.controller.confirmOpen());
        QVERIFY(QFileInfo::exists(source));
        QVERIFY(QFileInfo::exists(target));
        QVERIFY(!gallery->property("dragging").toBool());

        grid->setProperty("contentY", 0);
        QTest::qWait(100);
        sourceDelegate = findDelegate(h.window, source);
        QVERIFY(sourceDelegate);
        const auto topSourcePoint = itemCenter(sourceDelegate, h.window);
        const qreal beforeScroll = grid->property("contentY").toReal();
        QTest::mousePress(h.window, Qt::LeftButton, Qt::NoModifier, topSourcePoint);
        QTest::mouseMove(h.window, topSourcePoint + QPoint(32, 32));
        QTRY_VERIFY_WITH_TIMEOUT(gallery->property("dragging").toBool(), 1000);
        QTest::mouseMove(h.window, QPoint(h.window->width() / 2, h.window->height() - 4));
        QTRY_VERIFY_WITH_TIMEOUT(grid->property("contentY").toReal() > beforeScroll, 1200);
        QVERIFY(QMetaObject::invokeMethod(gallery, "endDrag", Qt::DirectConnection,
                                           Q_ARG(QVariant, QVariant(false))));
        QTest::mouseRelease(h.window, Qt::LeftButton, Qt::NoModifier,
                            QPoint(h.window->width() / 2, h.window->height() - 4));
        QVERIFY(!gallery->property("dragging").toBool());
        QVERIFY(!h.controller.confirmOpen());
    }

    void viewerUsesSnapshotZoomInputAndReturnsFocus()
    {
        Harness h;
        QVERIFY(h.filesReady());
        QVERIFY(h.load());
        h.controller.start(h.library);
        waitForGallery(h, 8);
        const auto alpha = h.path("alpha.png");
        const auto beta = h.path("beta.png");
        h.controller.select(h.row(alpha), 0);
        h.controller.select(h.row(beta), Qt::ControlModifier);
        h.controller.openViewer();
        QTRY_VERIFY_WITH_TIMEOUT(h.controller.viewerOpen(), 2000);
        QCOMPARE(h.controller.viewerName(), QStringLiteral("alpha.png"));
        const int snapshotCount = h.controller.viewerCount();
        auto *image = h.window->findChild<ImageItem *>();
        QVERIFY(image);
        QTRY_VERIFY_WITH_TIMEOUT(image->frame() && image->frame()->edge == 0, 10000);
        QVERIFY(image->acceptedMouseButtons() & Qt::LeftButton);
        QVERIFY(image->keepMouseGrab());

        const QPoint imageCenter = itemCenter(image, h.window);
        QTest::wheelEvent(h.window, imageCenter + QPoint(80, -40), QPoint(0, 120));
        QTRY_VERIFY_WITH_TIMEOUT(qFuzzyCompare(h.controller.zoom(), 1.2), 2000);
        const int selectedBeforePan = h.controller.selectionCount();
        QTest::mousePress(h.window, Qt::LeftButton, Qt::NoModifier, imageCenter);
        QTest::mouseMove(h.window, imageCenter + QPoint(30, 20));
        QTest::mouseRelease(h.window, Qt::LeftButton, Qt::NoModifier, imageCenter + QPoint(30, 20));
        QCOMPARE(h.controller.selectionCount(), selectedBeforePan);

        for (int i = 0; i < 40; ++i)
            h.controller.zoomBy(1.2);
        QVERIFY(h.controller.zoom() <= 8.0);
        for (int i = 0; i < 120; ++i)
            h.controller.zoomBy(1.0 / 1.2);
        QVERIFY(h.controller.zoom() >= 0.1);
        h.controller.resetZoom();
        QCOMPARE(h.controller.viewerIndex(), 0);
        QTest::keyClick(h.window, Qt::Key_Right);
        QTRY_COMPARE(h.controller.viewerIndex(), 1);

        h.controller.setSearch(QStringLiteral("target"));
        QTRY_COMPARE(h.controller.viewerCount(), snapshotCount);
        QCOMPARE(h.controller.viewerName(), QStringLiteral("beta.png"));
        QTest::keyClick(h.window, Qt::Key_Escape);
        QTRY_VERIFY_WITH_TIMEOUT(!h.controller.viewerOpen(), 2000);
        auto *grid = findGrid(h.window, h.controller.library());
        QVERIFY(grid);
        QVERIFY(grid->hasActiveFocus());
    }

    void toastDetailsAndComponentPanelAreRealQml()
    {
        Harness h;
        QVERIFY(h.filesReady());
        QVERIFY(h.load());
        h.controller.start(h.library);
        waitForGallery(h, 8);
        const auto source = h.path("source.png");
        h.controller.select(h.row(source), 0);
        h.controller.requestOperation(QStringLiteral("rename"), QStringLiteral("renamed"));
        QTRY_VERIFY_WITH_TIMEOUT(h.controller.property("toastOpen").toBool(), 8000);
        QVERIFY(h.controller.property("toastText").toString().contains(QStringLiteral("成功 1")));
        QCOMPARE(h.controller.results().size(), 1);
        const auto result = h.controller.results().constFirst().toMap();
        QCOMPARE(result.value("source").toString(), source);
        QCOMPARE(result.value("status").toString(), QStringLiteral("成功"));
        QVERIFY(QFileInfo::exists(h.path("renamed.png")));
        QVERIFY(findObject(h.window, [](QObject *object) {
            return object->property("interval").toInt() == 6000;
        }));

        h.controller.setProperty("resultsOpen", true);
        QTRY_VERIFY_WITH_TIMEOUT(findTextObject(h.window, QStringLiteral("作業結果")) != nullptr, 2000);
        QVERIFY(h.controller.property("toastOpen").toBool());

        Harness components(true);
        QVERIFY(components.filesReady());
        QVERIFY(components.load());
        QVERIFY(components.window->property("componentsMode").toBool());
        QVERIFY(findTextObject(components.window, QStringLiteral("元件展示")) != nullptr);
        QVERIFY(findTextObject(components.window, QStringLiteral("主要操作")) != nullptr);
        QVERIFY(findTextObject(components.window, QStringLiteral("很長的繁體中文檔名與 Unicode 圖片名稱.png")) != nullptr);
        auto *theme = findObject(components.window, [](QObject *object) {
            return object->property("dark").isValid() && object->property("background").isValid();
        });
        QVERIFY(theme);
        const auto lightBackground = theme->property("background").value<QColor>();
        components.controller.setProperty("dark", true);
        QTRY_VERIFY_WITH_TIMEOUT(theme->property("background").value<QColor>() != lightBackground, 2000);
        QCOMPARE(components.window->minimumWidth(), 800);
        QCOMPARE(components.window->minimumHeight(), 600);
        components.window->resize(800, 600);
        QTRY_COMPARE(components.window->width(), 800);
        QTRY_COMPARE(components.window->height(), 600);
    }
};

QTEST_MAIN(QmlIntegrationTest)
#include "qml_test.moc"
