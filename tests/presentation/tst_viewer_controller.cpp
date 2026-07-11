#include <piclens/presentation/viewer_controller.h>

#include <QSignalSpy>
#include <QTest>

#include <utility>

using namespace piclens::core;
using namespace piclens::presentation;

namespace {

ImageListItem image(QString name, bool animated = false, QString extension = QStringLiteral("png"))
{
    return {
        .path = QStringLiteral("C:/gallery/") + name,
        .name = std::move(name),
        .extension = std::move(extension),
        .modifiedAtMs = 0,
        .sizeBytes = 100,
        .isAnimated = animated,
    };
}

ImageSequenceSnapshot snapshot()
{
    return {
        .sourceFolderPath = QStringLiteral("C:/gallery"),
        .includeSubfolders = false,
        .sort = {},
        .images = {
            image(QStringLiteral("one.png")),
            image(QStringLiteral("motion.gif"), true, QStringLiteral("gif")),
            image(QStringLiteral("three.png")),
        },
        .currentIndex = 0,
    };
}

} // namespace

class ViewerControllerTests final : public QObject
{
    Q_OBJECT

private slots:
    void navigationUsesImmutableSnapshotAndResetsTransform();
    void zoomAndPanUseCoreMathAndVisibilityGates();
    void animatedImageShowsLocalizedUnsupportedState();
    void invalidSnapshotDoesNotOpen();
    void loadFailureExposesLocalizedStateAndSignal();
};

void ViewerControllerTests::navigationUsesImmutableSnapshotAndResetsTransform()
{
    ViewerController viewer;
    ImageSequenceSnapshot source = snapshot();
    viewer.openSnapshot(source);
    source.images.clear();

    QVERIFY(viewer.isOpen());
    QCOMPARE(viewer.imageCount(), 3);
    QCOMPARE(viewer.currentName(), QStringLiteral("one.png"));
    QVERIFY(viewer.canGoNext());
    viewer.zoomIn(800, 600);
    QVERIFY(viewer.zoom() > 1);
    viewer.next();
    QCOMPARE(viewer.currentName(), QStringLiteral("motion.gif"));
    QCOMPARE(viewer.zoom(), 1.0);
    QVERIFY(viewer.canGoPrevious());
    viewer.previous();
    QCOMPARE(viewer.currentIndex(), 0);

    QSignalSpy closed(&viewer, &ViewerController::closed);
    viewer.close();
    QVERIFY(!viewer.isOpen());
    QCOMPARE(closed.count(), 1);
}

void ViewerControllerTests::zoomAndPanUseCoreMathAndVisibilityGates()
{
    ViewerController viewer;
    viewer.openSnapshot(snapshot());
    viewer.zoomAt(600, 300, 1, 800, 600);
    QVERIFY(viewer.zoom() > 1);
    QVERIFY(viewer.offsetX() < 0);
    const double beforeX = viewer.offsetX();
    viewer.panBy(48, -24);
    QCOMPARE(viewer.offsetX(), beforeX + 48);
    viewer.resetZoom();
    QCOMPARE(viewer.zoom(), 1.0);
    QCOMPARE(viewer.offsetX(), 0.0);
    viewer.panBy(50, 50);
    QCOMPARE(viewer.offsetX(), 0.0);
}

void ViewerControllerTests::animatedImageShowsLocalizedUnsupportedState()
{
    ViewerController viewer;
    viewer.openSnapshot(snapshot());
    viewer.next();

    QVERIFY(viewer.unsupportedAnimated());
    QVERIFY(!viewer.imageVisible());
    QVERIFY(viewer.currentSourceUrl().isEmpty());
    QCOMPARE(
        viewer.unsupportedMessage(),
        QStringLiteral("原生檢視器目前尚不支援播放動畫 GIF。"));
    QVERIFY(!viewer.canZoomIn());
}

void ViewerControllerTests::invalidSnapshotDoesNotOpen()
{
    ViewerController viewer;
    ImageSequenceSnapshot invalid = snapshot();
    invalid.currentIndex = 99;
    viewer.openSnapshot(std::move(invalid));
    QVERIFY(!viewer.isOpen());
}

void ViewerControllerTests::loadFailureExposesLocalizedStateAndSignal()
{
    ViewerController viewer;
    viewer.openSnapshot(snapshot());
    QSignalSpy failed(&viewer, &ViewerController::loadFailed);

    viewer.reportLoadFailure(QStringLiteral("decoder failed"));

    QCOMPARE(failed.count(), 1);
    QCOMPARE(failed.at(0).at(0).toString(), QStringLiteral("C:/gallery/one.png"));
    QCOMPARE(failed.at(0).at(1).toString(), QStringLiteral("decoder failed"));
    QVERIFY(viewer.errorMessage().contains(QStringLiteral("無法載入圖片")));
    viewer.next();
    QVERIFY(viewer.errorMessage().isEmpty());
}

QTEST_GUILESS_MAIN(ViewerControllerTests)

#include "tst_viewer_controller.moc"
