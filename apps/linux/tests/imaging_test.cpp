#include "imaging.h"
#include "imageitem.h"
#include <QCoreApplication>
#include <QDataStream>
#include <QElapsedTimer>
#include <QFile>
#include <QImageReader>
#include <QProcess>
#include <QQuickWindow>
#include <QSignalSpy>
#include <QTemporaryDir>
#include <QTest>
#include <QTimer>
#include <algorithm>

using namespace piclens;
class ImagingTest : public QObject {
    Q_OBJECT
private:
    static QString fixture(const QString &directory, QSize size = {53, 37}) {
        QImage image(size, QImage::Format_RGBA8888);
        for (int y = 0; y < size.height(); ++y) for (int x = 0; x < size.width(); ++x)
            image.setPixelColor(x, y, QColor(x % 256, y % 256, (x + y) % 256, (x + y) % 5 ? 255 : 0));
        const auto path = directory + "/來源 image.png";
        return image.save(path) ? path : QString();
    }
    static int worker(const QStringList &arguments) {
        QProcess process; process.start(QString::fromUtf8(PICLENS_WORKER), arguments);
        if (!process.waitForFinished(30000)) { process.kill(); process.waitForFinished(); return -1; }
        return process.exitStatus() == QProcess::NormalExit ? process.exitCode() : -1;
    }
private slots:
    void staticFormats_data() {
        QTest::addColumn<QByteArray>("format");
        for (const auto &format : {"png", "jpg", "jpeg", "bmp", "webp", "gif"}) QTest::newRow(format) << QByteArray(format);
    }
    void staticFormats() {
        QFETCH(QByteArray, format);
        QTemporaryDir dir;
        const auto path = dir.path() + "/sample." + QString::fromLatin1(format);
        if (format == "gif") {
            QFile file(path); QVERIFY(file.open(QIODevice::WriteOnly));
            file.write(QByteArray::fromHex("47494638396101000100800000000000ffffff21f90401000000002c000000000100010000020144003b"));
        } else {
            QImage image(31, 17, QImage::Format_RGB32); image.fill(Qt::green);
            QVERIFY(image.save(path, format.constData()));
        }
        Imaging imaging(QString::fromUtf8(PICLENS_WORKER), dir.path() + "/cache");
        QSignalSpy spy(&imaging, &Imaging::completed);
        QVERIFY(imaging.enqueue(path, 0, "format"));
        QTRY_COMPARE_WITH_TIMEOUT(spy.size(), 1, 10000);
        const auto frame = qvariant_cast<FramePtr>(spy[0][1]);
        QVERIFY2(frame, qPrintable(spy[0][2].toString()));
        QCOMPARE(frame->size, format == "gif" ? QSize(1, 1) : QSize(31, 17));
    }
    void animationAndOversizeRejected() {
        QTemporaryDir dir;
        const auto animation = dir.path() + "/animated.gif";
        QFile gif(animation); QVERIFY(gif.open(QIODevice::WriteOnly));
        gif.write(QByteArray::fromHex("47494638396101000100800000000000ffffff"));
        const auto frame = QByteArray::fromHex("21f90401000000002c00000000010001000002014400");
        gif.write(frame); gif.write(frame); gif.write(";"); gif.close();
        const auto large = dir.path() + "/large.bmp";
        QFile bmp(large); QVERIFY(bmp.open(QIODevice::WriteOnly));
        QDataStream stream(&bmp); stream.setByteOrder(QDataStream::LittleEndian);
        stream.writeRawData("BM", 2);
        stream << quint32(54) << quint16(0) << quint16(0) << quint32(54) << quint32(40)
               << qint32(10000) << qint32(10000) << quint16(1) << quint16(24)
               << quint32(0) << quint32(0) << qint32(0) << qint32(0) << quint32(0) << quint32(0);
        bmp.close();
        Imaging imaging(QString::fromUtf8(PICLENS_WORKER), dir.path() + "/cache");
        QSignalSpy spy(&imaging, &Imaging::completed);
        QVERIFY(imaging.enqueue(animation, 0, "animation")); QVERIFY(imaging.enqueue(large, 0, "oversize"));
        QTRY_COMPARE_WITH_TIMEOUT(spy.size(), 2, 10000);
        for (const auto &result : spy) {
            QVERIFY(!qvariant_cast<FramePtr>(result[1]));
            QVERIFY2(result[2].toString().contains(result[0] == "animation" ? QStringLiteral("動畫") : QStringLiteral("256 MiB")), qPrintable(result[2].toString()));
        }
    }
    void coldWarmAndIdentity() {
        QTemporaryDir dir; QVERIFY(dir.isValid());
        const auto path = fixture(dir.path()); QVERIFY(!path.isEmpty());
        Imaging imaging(QString::fromUtf8(PICLENS_WORKER), dir.path() + "/cache");
        QSignalSpy spy(&imaging, &Imaging::completed);
        QVERIFY(imaging.enqueue(path, 96, "cold"));
        QTRY_COMPARE_WITH_TIMEOUT(spy.size(), 1, 10000);
        QVERIFY2(spy[0][2].toString().isEmpty(), qPrintable(spy[0][2].toString()));
        const auto cold = qvariant_cast<FramePtr>(spy[0][1]); QVERIFY(cold);
        QCOMPARE(cold->image.size(), QSize(53, 37)); QCOMPARE(cold->token, "cold");
        QCOMPARE(QDir(dir.path() + "/cache/thumbnails-v1").entryList({"*.png"}, QDir::Files).size(), 1);
        imaging.clearRecent();
        QVERIFY(imaging.enqueue(path, 96, "warm"));
        QTRY_COMPARE_WITH_TIMEOUT(spy.size(), 2, 10000);
        const auto warm = qvariant_cast<FramePtr>(spy[1][1]); QVERIFY(warm);
        QCOMPARE(warm->image, cold->image);
        QVERIFY(!fixture(dir.path(), {71, 39}).isEmpty());
        QVERIFY(imaging.enqueue(path, 96, "changed"));
        QTRY_COMPARE_WITH_TIMEOUT(spy.size(), 3, 10000);
        const auto changed = qvariant_cast<FramePtr>(spy[2][1]); QVERIFY(changed);
        QCOMPARE(changed->size, QSize(71, 39)); QVERIFY(changed->fileSize != cold->fileSize || changed->mtime != cold->mtime);
        QCOMPARE(imaging.outstanding(), 0);
    }
    void threePreviewsFitActualAllocationBudget() {
        QTemporaryDir dir; const auto source = fixture(dir.path(), {1100, 1100});
        QVERIFY(!source.isEmpty());
        Imaging imaging(QString::fromUtf8(PICLENS_WORKER), dir.path() + "/cache");
        QSignalSpy spy(&imaging, &Imaging::completed);
        QStringList paths;
        for (int i = 0; i < 3; ++i) {
            const auto path = dir.path() + "/preview" + QString::number(i) + ".png";
            QVERIFY(QFile::copy(source, path)); paths.append(path);
        }
        // Verify both cold decoding and warm PNG decoding; distinct sources
        // prevent request deduplication from hiding multiple allocations.
        for (int pass = 0; pass < 2; ++pass) {
            spy.clear();
            for (int i = 0; i < 3; ++i) QVERIFY(imaging.enqueue(paths[i], 1024, QString::number(i)));
            QTRY_COMPARE_WITH_TIMEOUT(spy.size(), 3, 10000);
            qint64 total = 0;
            for (const auto &result : spy) {
                const auto frame = qvariant_cast<FramePtr>(result[1]);
                QVERIFY2(frame, qPrintable(result[2].toString()));
                QCOMPARE(frame->edge, 1024);
                QCOMPARE(frame->size, QSize(1022, 1022));
                QCOMPARE(frame->tiles.size(), 1);
                const auto &pixels = frame->tiles.first().pixels;
                QCOMPARE(pixels.size(), QSize(1024, 1024));
                QVERIFY(frame->image.constBits() == pixels.constBits() + pixels.bytesPerLine() + 4);
                QCOMPARE(frame->image.bytesPerLine(), pixels.bytesPerLine());
                QCOMPARE(frame->byteSize(), pixels.sizeInBytes());
                total += frame->byteSize();
            }
            QCOMPARE(total, 12LL * 1024 * 1024);
        }
    }
    void originalTilesAndBorders() {
        QTemporaryDir dir; const auto path = fixture(dir.path(), {4097, 9}); QVERIFY(!path.isEmpty());
        Imaging imaging(QString::fromUtf8(PICLENS_WORKER), dir.path() + "/cache");
        QSignalSpy spy(&imaging, &Imaging::completed);
        QVERIFY(imaging.enqueue(path, 0, "original"));
        QTRY_COMPARE_WITH_TIMEOUT(spy.size(), 1, 10000);
        const auto frame = qvariant_cast<FramePtr>(spy[0][1]); QVERIFY2(frame, qPrintable(spy[0][2].toString()));
        QCOMPARE(frame->size, QSize(4097, 9)); QCOMPARE(frame->tiles.size(), 3); QVERIFY(frame->image.isNull());
        const QImage source = QImage(path).convertToFormat(QImage::Format_RGBA8888_Premultiplied);
        for (const auto &tile : frame->tiles) {
            QVERIFY(tile.pixels.width() <= 2048); QVERIFY(tile.pixels.height() <= 2048);
            QCOMPARE(tile.pixels.format(), QImage::Format_RGBA8888_Premultiplied);
            for (int y = 0; y < tile.pixels.height(); ++y) for (int x = 0; x < tile.pixels.width(); ++x) {
                const int sx = std::clamp(tile.rect.x() + x - 1, 0, source.width() - 1);
                const int sy = std::clamp(tile.rect.y() + y - 1, 0, source.height() - 1);
                QCOMPARE(tile.pixels.pixel(x, y), source.pixel(sx, sy));
            }
        }
        QCOMPARE(QDir(dir.path() + "/cache/thumbnails-v1").entryList({"*.png"}, QDir::Files).size(), 0);
    }
    void conversionPreservesSourceAndRefusesOverwrite() {
        QTemporaryDir dir; const auto path = fixture(dir.path()); QVERIFY(!path.isEmpty());
        QFile source(path); QVERIFY(source.open(QIODevice::ReadOnly)); const auto before = source.readAll(); source.close();
        const auto target = dir.path() + "/converted.webp";
        QCOMPARE(worker({"--convert", path, target, "webp"}), 0);
        QCOMPARE(QImage(target).convertToFormat(QImage::Format_RGBA8888), QImage(path).convertToFormat(QImage::Format_RGBA8888));
        QCOMPARE(worker({"--convert", path, target, "webp"}), 1);
        QCOMPARE(worker({"--convert", path, path, "jpg"}), 1);
        QCOMPARE(worker({"--encode-temp", path, path, "jpg"}), 1);
        const auto temporary = dir.path() + "/owned.tmp";
        QFile file(temporary); QVERIFY(file.open(QIODevice::WriteOnly | QIODevice::NewOnly)); file.close();
        QCOMPARE(worker({"--encode-temp", path, temporary, "jpg"}), 0);
        QCOMPARE(QImage(temporary).size(), QSize(53, 37));
        QCOMPARE(worker({"--encode-temp", path, temporary, "jpg"}), 1);
        QVERIFY(source.open(QIODevice::ReadOnly)); QCOMPARE(source.readAll(), before);
    }
    void failuresAndCancellationAreTerminal() {
        QTemporaryDir dir;
        QFile broken(dir.path() + "/broken.png"); QVERIFY(broken.open(QIODevice::WriteOnly)); broken.write("broken"); broken.close();
        Imaging imaging(QString::fromUtf8(PICLENS_WORKER), dir.path() + "/cache");
        QSignalSpy spy(&imaging, &Imaging::completed);
        QVERIFY(imaging.enqueue(broken.fileName(), 100, "bad"));
        QVERIFY(imaging.enqueue(broken.fileName(), 100, "cancelled")); imaging.cancel("cancelled");
        QTRY_COMPARE_WITH_TIMEOUT(spy.size(), 2, 10000);
        for (const auto &result : spy) { QVERIFY(!qvariant_cast<FramePtr>(result[1])); QVERIFY(!result[2].toString().isEmpty()); }
        QVERIFY(imaging.enqueue(broken.fileName(), 100, "bad")); // token reused only after terminal result
        imaging.shutdown(); QCOMPARE(spy.size(), 3); QCOMPARE(imaging.outstanding(), 0);
        QVERIFY(!imaging.enqueue(broken.fileName(), 100, "closed"));
    }
    void queueBoundAndShutdownReap() {
        QTemporaryDir dir; const auto path = fixture(dir.path());
        QStringList paths;
        for (int i = 0; i < Imaging::MaxProcesses; ++i) {
            const auto copy = dir.path() + "/" + QString::number(i) + ".png";
            QVERIFY(QFile::copy(path, copy)); paths.append(copy);
        }
        Imaging imaging(QCoreApplication::applicationFilePath(), dir.path() + "/cache");
        QSignalSpy spy(&imaging, &Imaging::completed);
        for (int i = 0; i < Imaging::MaxProcesses + Imaging::MaxQueued; ++i) QVERIFY(imaging.enqueue(paths[i % paths.size()], 0, QString::number(i)));
        QVERIFY(!imaging.enqueue(path, 0, "overflow"));
        QTRY_COMPARE_WITH_TIMEOUT(QDir(dir.path()).entryList({"*.started"}, QDir::Files).size(), Imaging::MaxProcesses, 5000);
        QElapsedTimer elapsed; elapsed.start(); imaging.shutdown();
        QVERIFY(elapsed.elapsed() < 5000);
        QCOMPARE(spy.size(), Imaging::MaxProcesses + Imaging::MaxQueued);
        QCOMPARE(imaging.outstanding(), 0);
        for (const auto &result : spy) QVERIFY(!qvariant_cast<FramePtr>(result[1]));
    }
    void stalledWorkerTimeout() {
        QTemporaryDir dir; const auto path = fixture(dir.path());
        Imaging imaging(QCoreApplication::applicationFilePath(), dir.path() + "/cache");
        QSignalSpy spy(&imaging, &Imaging::completed);
        QVERIFY(imaging.enqueue(path, 0, "stalled"));
        QTRY_COMPARE_WITH_TIMEOUT(spy.size(), 1, 20000);
        QVERIFY(spy[0][2].toString().contains(QStringLiteral("逾時")));
        QCOMPARE(imaging.outstanding(), 0);
    }
    void quiesceReapsAndAllowsResume() {
        QTemporaryDir dir; const auto path = fixture(dir.path());
        Imaging imaging(QCoreApplication::applicationFilePath(), dir.path() + "/cache");
        QSignalSpy completed(&imaging, &Imaging::completed);
        QSignalSpy quiet(&imaging, &Imaging::quiesced);
        for (int i = 0; i < 16; ++i) {
            const auto copy = dir.path() + "/quiesce" + QString::number(i) + ".png";
            QVERIFY(QFile::copy(path, copy)); QVERIFY(imaging.enqueue(copy, 0, QString::number(i)));
        }
        QTRY_COMPARE_WITH_TIMEOUT(QDir(dir.path()).entryList({"*.started"}, QDir::Files).size(), 8, 5000);
        QElapsedTimer elapsed; elapsed.start(); imaging.quiesce();
        QVERIFY(elapsed.elapsed() < 100);
        QVERIFY(!imaging.enqueue(path, 0, "too-early"));
        QTRY_COMPARE_WITH_TIMEOUT(quiet.size(), 1, 5000);
        QCOMPARE(imaging.outstanding(), 0); QCOMPARE(completed.size(), 16);
        for (const auto &result : completed) QVERIFY(!qvariant_cast<FramePtr>(result[1]));
        QVERIFY(imaging.enqueue(path, 0, "resumed"));
        imaging.quiesce();
        QTRY_COMPARE_WITH_TIMEOUT(quiet.size(), 2, 5000);
        QCOMPARE(completed.size(), 17); QCOMPARE(imaging.outstanding(), 0);
    }
    void zoomAndOriginalPaintSignal() {
        QQuickWindow window; window.resize(400, 300);
        auto *item = new ImageItem(window.contentItem()); item->setSize({400, 300});
        item->zoomBy(100); QCOMPARE(item->zoom(), 8.0);
        item->zoomBy(0.00001); QCOMPARE(item->zoom(), 0.1);
        item->reset(); QCOMPARE(item->zoom(), 1.0);
        auto frame = std::make_shared<Frame>(); frame->token = "paint"; frame->size = {20, 20};
        QImage pixels(22, 22, QImage::Format_RGBA8888_Premultiplied); pixels.fill(Qt::red);
        frame->tiles.append({{0, 0, 20, 20}, pixels});
        QSignalSpy spy(item, &ImageItem::framePresented);
        item->setFrame(frame); window.show();
        QTRY_COMPARE_WITH_TIMEOUT(spy.size(), 1, 5000);
        QCOMPARE(spy[0][0].toString(), "paint");
        item->zoomBy(1.2); QTest::qWait(50); QCOMPARE(spy.size(), 1);
        auto preview = std::make_shared<Frame>(*frame); preview->edge = 1024;
        item->setFrame(preview); QTest::qWait(50); QCOMPARE(spy.size(), 1);
    }
};

int main(int argc, char **argv) {
    // This executable doubles as a deliberately stuck decoder. It has no
    // unkillable test threads and is always owned/reaped by the real service.
    if (argc > 1 && QByteArray(argv[1]) == "--decode") {
        QCoreApplication application(argc, argv);
        QFile marker(application.arguments()[2] + ".started");
        if (marker.open(QIODevice::WriteOnly)) marker.write(QByteArray::number(QCoreApplication::applicationPid()));
        marker.close();
        return application.exec();
    }
    QGuiApplication application(argc, argv);
    ImagingTest test; return QTest::qExec(&test, argc, argv);
}
#include "imaging_test.moc"
