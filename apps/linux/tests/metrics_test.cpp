#include "controller.h"
#include "imageitem.h"
#include <QtTest>
#include <QDir>
#include <QFile>
#include <QJsonArray>
#include <QJsonDocument>
#include <QJsonObject>
#include <QProcess>
#include <QQuickWindow>
#include <QTemporaryDir>

using namespace piclens;

namespace {
QString makeImage(const QString &directory, const QString &name, const QSize size = {40, 30}) {
    QImage image(size, QImage::Format_ARGB32);
    image.fill(Qt::green);
    const QString path = QDir(directory).filePath(name);
    if (!image.save(path)) return {};
    return path;
}

QJsonObject sampleAt(const QJsonArray &samples, int index) {
    return samples.at(index).toObject();
}
}

class MetricsTest final : public QObject {
    Q_OBJECT
private slots:
    void unavailableMeasurementsAreNull() {
        QTemporaryDir dir;
        QVERIFY(dir.isValid());
        ThumbProvider provider;
        Controller controller(dir.filePath("profile"), QString::fromUtf8(PICLENS_WORKER), &provider);

        const QJsonObject metrics = controller.metrics();
        QCOMPARE(metrics.value("schemaVersion").toInt(), 1);
        QVERIFY(metrics.contains("frontEnd"));
        QVERIFY(metrics.contains("buildProfile"));
        QVERIFY(metrics.contains("fullPaintSamples"));
        QVERIFY(metrics.value("libraryMilliseconds").isDouble());
        QVERIFY(metrics.value("searchMilliseconds").isDouble());
        QCOMPARE(metrics.value("libraryMilliseconds").toDouble(), 0.0);
        QCOMPARE(metrics.value("searchMilliseconds").toDouble(), 0.0);
        QVERIFY(metrics.value("firstThumbnailReadyMilliseconds").isNull());
        QVERIFY(metrics.value("viewerPreviewReadyMilliseconds").isNull());
        QVERIFY(metrics.value("viewerSharpPaintMilliseconds").isNull());
        QVERIFY(metrics.value("viewerSharpPaintMaximumMilliseconds").isNull());
        QVERIFY(metrics.value("viewerPreviewSamples").isArray());
        QCOMPARE(metrics.value("viewerPreviewSamples").toArray().size(), 0);
        QCOMPARE(metrics.value("viewerSharpPaintCount").toInt(), 0);
        QCOMPARE(metrics.value("viewerSharpTargetMisses").toInt(), 0);
        QCOMPARE(metrics.value("viewerSharpTargetMilliseconds").toInt(), 500);
        QCOMPARE(metrics.value("unpaintedSelections").toInt(), 0);
        QVERIFY(metrics.contains("lastCompletedBatch"));
        QVERIFY(metrics.value("lastCompletedBatch").isNull());
        QVERIFY(!metrics.contains("childProcessLimit"));
        QVERIFY(!metrics.contains("gpuSingleTextureEdgePixels"));
        QVERIFY(!metrics.contains("viewerOriginalRgbaLimitBytes"));
        QVERIFY(!metrics.contains("viewerPreviewRgbaLimitBytes"));

        QVERIFY(metrics.value("metricsTimestampUtc").isString());
        QVERIFY(!metrics.value("metricsTimestampUtc").toString().isEmpty());
#ifdef Q_OS_LINUX
        QVERIFY(metrics.value("processCpuMilliseconds").isDouble());
        QVERIFY(metrics.value("averageCpuUtilizationPercent").isDouble() || metrics.value("averageCpuUtilizationPercent").isNull());
        const QByteArray dump = QJsonDocument(metrics).toJson(QJsonDocument::Compact);
        QVERIFY2(!metrics.value("rssBytes").isNull(), dump.constData());
        QVERIFY2(!metrics.value("peakRssBytes").isNull(), dump.constData());
        QCOMPARE(metrics.value("cpuNormalizedByLogicalProcessors").toBool(), true);
        QCOMPARE(metrics.value("processScope").toString(), QStringLiteral("self"));
        QCOMPARE(metrics.value("childProcessesIncluded").toBool(), false);
        QVERIFY(metrics.value("gpuMemoryBytes").isNull());
#else
        QVERIFY(metrics.value("processCpuMilliseconds").isNull());
        QVERIFY(metrics.value("averageCpuUtilizationPercent").isNull());
        QVERIFY(metrics.value("rssBytes").isNull());
        QVERIFY(metrics.value("peakRssBytes").isNull());
#endif
        controller.shutdown();
    }

    void realViewerKeepsPreviewAndSharpSelectionIdentity() {
        QTemporaryDir dir;
        QVERIFY(dir.isValid());
        const QString library = dir.filePath("images");
        QVERIFY(QDir().mkpath(library));
        const QString a = makeImage(library, "a.png");
        const QString b = makeImage(library, "b.png");
        QVERIFY(!a.isEmpty());
        QVERIFY(!b.isEmpty());

        ThumbProvider provider;
        Controller controller(dir.filePath("profile"), QString::fromUtf8(PICLENS_WORKER), &provider);
        QQuickWindow window;
        window.resize(400, 300);
        auto *item = new ImageItem(window.contentItem());
        item->setSize({400, 300});
        controller.attachItem(item);
        window.show();

        controller.start(library);
        QTRY_COMPARE_WITH_TIMEOUT(controller.count(), 2, 5000);
        auto *model = qobject_cast<Rows *>(controller.library());
        QVERIFY(model);
        controller.setVisible({a, b});
        QTRY_VERIFY_WITH_TIMEOUT(!model->get(0).value("imageKey").toString().isEmpty(), 10000);
        QVERIFY(!controller.metrics().value("firstThumbnailReadyMilliseconds").isNull());

        controller.openViewer(0);
        QTRY_COMPARE_WITH_TIMEOUT(controller.metrics().value("viewerSharpPaintCount").toInt(), 1, 10000);
        QTRY_COMPARE_WITH_TIMEOUT(controller.metrics().value("viewerPreviewSamples").toArray().size(), 1, 10000);
        controller.viewerStep(1);
        QTRY_COMPARE_WITH_TIMEOUT(controller.metrics().value("viewerSharpPaintCount").toInt(), 2, 10000);
        controller.viewerStep(-1);
        QTRY_COMPARE_WITH_TIMEOUT(controller.metrics().value("viewerSharpPaintCount").toInt(), 3, 10000);
        controller.closeViewer();
        controller.openViewer(0);
        QTRY_COMPARE_WITH_TIMEOUT(controller.metrics().value("viewerSharpPaintCount").toInt(), 4, 10000);
        QTest::qWait(100);

        const QJsonObject metrics = controller.metrics();
        QCOMPARE(metrics.value("viewerSelections").toInt(), 4);
        QCOMPARE(metrics.value("viewerSharpPaintCount").toInt(), 4);
        QCOMPARE(metrics.value("viewerSharpPaintMilliseconds").toArray().size(), 4);
        QCOMPARE(metrics.value("viewerPreviewReadyMilliseconds").toArray().size(), 4);
        QVERIFY(!metrics.value("viewerSharpPaintMaximumMilliseconds").isNull());
        QCOMPARE(metrics.value("unpaintedSelections").toInt(), 0);

        const QJsonArray previewSamples = metrics.value("viewerPreviewSamples").toArray();
        QCOMPARE(previewSamples.size(), 4);
        QSet<QString> selectionIds;
        QSet<QString> sessions;
        for (const auto &value : previewSamples) {
            const auto sample = value.toObject();
            QVERIFY(!sample.value("selectionId").toString().isEmpty());
            QVERIFY(!sample.value("viewerSessionId").toString().isEmpty());
            QVERIFY(sample.value("ready").toBool());
            QVERIFY(sample.value("milliseconds").isDouble());
            selectionIds.insert(sample.value("selectionId").toString());
            sessions.insert(sample.value("viewerSessionId").toString());
        }
        QCOMPARE(selectionIds.size(), 4);
        QCOMPARE(sessions.size(), 2);

        const QJsonArray sharpSamples = metrics.value("fullPaintSamples").toArray();
        QCOMPARE(sharpSamples.size(), 4);
        QCOMPARE(sampleAt(sharpSamples, 0).value("path").toString(), a);
        QCOMPARE(sampleAt(sharpSamples, 1).value("path").toString(), b);
        QCOMPARE(sampleAt(sharpSamples, 2).value("path").toString(), a);
        QCOMPARE(sampleAt(sharpSamples, 3).value("path").toString(), a);
        const double sharpTargetMilliseconds = metrics.value("viewerSharpTargetMilliseconds").toDouble();
        int expectedTargetMisses = 0;
        double expectedMaximumMilliseconds = 0.0;
        for (const auto &value : sharpSamples) {
            const auto sample = value.toObject();
            QVERIFY(sample.value("milliseconds").isDouble());
            const double milliseconds = sample.value("milliseconds").toDouble();
            if (milliseconds > sharpTargetMilliseconds) ++expectedTargetMisses;
            expectedMaximumMilliseconds = qMax(expectedMaximumMilliseconds, milliseconds);
        }
        QCOMPARE(metrics.value("viewerSharpTargetMisses").toInt(), expectedTargetMisses);
        QCOMPARE(metrics.value("viewerSharpPaintMaximumMilliseconds").toDouble(), expectedMaximumMilliseconds);
        for (const auto &value : sharpSamples) {
            const auto sample = value.toObject();
            QVERIFY(!sample.value("selectionId").toString().isEmpty());
            QVERIFY(!sample.value("viewerSessionId").toString().isEmpty());
            QCOMPARE(sample.value("fullResolution").toBool(), true);
        }
        controller.shutdown();
    }

    void canceledConfirmationDoesNotCreateBatchResult() {
        QTemporaryDir dir;
        QVERIFY(dir.isValid());
        const QString library = dir.filePath("images");
        QVERIFY(QDir().mkpath(library));
        const QString path = makeImage(library, "same.png");
        QVERIFY(!path.isEmpty());

        ThumbProvider provider;
        Controller controller(dir.filePath("profile"), QString::fromUtf8(PICLENS_WORKER), &provider);
        controller.start(library);
        QTRY_COMPARE_WITH_TIMEOUT(controller.count(), 1, 5000);
        controller.select(0, 0);
        controller.requestOperation("rename");
        QTRY_VERIFY_WITH_TIMEOUT(controller.renameOpen(), 5000);
        controller.confirm(false);

        const QJsonObject metrics = controller.metrics();
        QVERIFY(metrics.contains("lastCompletedBatch"));
        QVERIFY(metrics.value("lastCompletedBatch").isNull());
        controller.shutdown();
    }

    void completedBatchRecordsCountsAndDuration() {
        QTemporaryDir dir;
        QVERIFY(dir.isValid());
        const QString library = dir.filePath("images");
        QVERIFY(QDir().mkpath(library));
        const QString path = makeImage(library, "same.png");
        QVERIFY(!path.isEmpty());

        ThumbProvider provider;
        Controller controller(dir.filePath("profile"), QString::fromUtf8(PICLENS_WORKER), &provider);
        controller.start(library);
        QTRY_COMPARE_WITH_TIMEOUT(controller.count(), 1, 5000);
        controller.select(0, 0);
        controller.requestOperation("rename", "same");

        QTRY_VERIFY_WITH_TIMEOUT(!controller.metrics().value("lastCompletedBatch").isNull(), 10000);
        const QJsonObject batch = controller.metrics().value("lastCompletedBatch").toObject();
        QCOMPARE(batch.value("total").toInt(), 1);
        QCOMPARE(batch.value("succeeded").toInt(), 0);
        QCOMPARE(batch.value("skipped").toInt(), 1);
        QCOMPARE(batch.value("canceled").toInt(), 0);
        QCOMPARE(batch.value("failed").toInt(), 0);
        QCOMPARE(batch.value("unknown").toInt(), 0);
        QVERIFY(batch.value("durationMilliseconds").isDouble());
        QVERIFY(batch.value("durationMilliseconds").toDouble() >= 0.0);
        QVERIFY(QFile::exists(path));
        controller.shutdown();
    }

    void missingSelectionIsNotAZeroDurationPaint() {
        QTemporaryDir dir;
        QVERIFY(dir.isValid());
        ThumbProvider provider;
        Controller controller(dir.filePath("profile"), QString::fromUtf8(PICLENS_WORKER), &provider);
        controller.diagnose(1);
        controller.openViewer(0);

        const QJsonObject metrics = controller.metrics();
        QCOMPARE(metrics.value("viewerSelections").toInt(), 1);
        QCOMPARE(metrics.value("viewerSharpPaintCount").toInt(), 0);
        QCOMPARE(metrics.value("viewerSharpTargetMisses").toInt(), 0);
        QCOMPARE(metrics.value("unpaintedSelections").toInt(), 1);
        QVERIFY(metrics.value("viewerPreviewReadyMilliseconds").isNull());
        QVERIFY(metrics.value("viewerSharpPaintMilliseconds").isNull());
        const QJsonArray samples = metrics.value("viewerPreviewSamples").toArray();
        QCOMPARE(samples.size(), 1);
        const QJsonObject sample = samples.first().toObject();
        QCOMPARE(sample.value("ready").toBool(), false);
        QVERIFY(sample.value("milliseconds").isNull());
        controller.shutdown();
    }

    void delayedRealPaintIsASeparateTargetMiss() {
        QTemporaryDir dir;
        QVERIFY(dir.isValid());
        const QString library = dir.filePath("images");
        QVERIFY(QDir().mkpath(library));
        const QString path = makeImage(library, "delayed.png");
        QVERIFY(!path.isEmpty());

        ThumbProvider provider;
        Controller controller(dir.filePath("profile"), QString::fromUtf8(PICLENS_WORKER), &provider);
        QQuickWindow window;
        window.resize(400, 300);
        auto *item = new ImageItem(window.contentItem());
        item->setSize({400, 300});
        controller.attachItem(item);
        controller.start(library);
        QTRY_COMPARE_WITH_TIMEOUT(controller.count(), 1, 5000);
        controller.openViewer(0);

        // Wait for the real full frame to reach ImageItem, then intentionally
        // hold the native window closed beyond the 500 ms paint target.
        QTRY_VERIFY_WITH_TIMEOUT(item->frame() && item->frame()->edge == 0, 10000);
        QTest::qWait(550);
        window.show();
        QTRY_COMPARE_WITH_TIMEOUT(controller.metrics().value("viewerSharpPaintCount").toInt(), 1, 5000);

        const QJsonObject metrics = controller.metrics();
        QCOMPARE(metrics.value("viewerSharpTargetMisses").toInt(), 1);
        QCOMPARE(metrics.value("unpaintedSelections").toInt(), 0);
        const QJsonArray milliseconds = metrics.value("viewerSharpPaintMilliseconds").toArray();
        QCOMPARE(milliseconds.size(), 1);
        QVERIFY(milliseconds.first().toDouble() > 500.0);
        controller.shutdown();
    }

    void metricsOutputFailureReturnsThree() {
        QTemporaryDir dir;
        QVERIFY(dir.isValid());
        const QString blocked = dir.filePath("metrics-directory");
        QVERIFY(QDir().mkpath(blocked));
        QProcess process;
        process.setProgram(QString::fromUtf8(PICLENS_APP));
        process.setArguments({"--data-root", dir.filePath("data"), "--metrics", blocked, "--smoke-ms", "1"});
        auto environment = QProcessEnvironment::systemEnvironment();
        environment.insert("QT_QPA_PLATFORM", "offscreen");
        environment.insert("QT_QPA_PLATFORMTHEME", {});
        process.setProcessEnvironment(environment);
        process.start();
        QVERIFY(process.waitForFinished(10000));
        QCOMPARE(process.exitStatus(), QProcess::NormalExit);
        QCOMPARE(process.exitCode(), 3);

#ifdef Q_OS_LINUX
        process.setArguments({"--data-root", dir.filePath("data-full"), "--metrics", "/dev/full", "--smoke-ms", "1"});
        process.start();
        QVERIFY(process.waitForFinished(10000));
        QCOMPARE(process.exitStatus(), QProcess::NormalExit);
        QCOMPARE(process.exitCode(), 3);
#endif
    }
};

QTEST_MAIN(MetricsTest)
#include "metrics_test.moc"
