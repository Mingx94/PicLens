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
    static QString animatedWebpProbe(const QString &directory) {
        const auto source = directory + "/static.webp";
        QImage image(9, 7, QImage::Format_RGBA8888); image.fill(QColor(30, 60, 90, 255));
        if (!image.save(source, "webp")) return {};
        QFile input(source); if (!input.open(QIODevice::ReadOnly)) return {};
        QByteArray bytes = input.readAll(); input.close();
        if (bytes.size() < 12 || bytes.left(4) != "RIFF" || bytes.mid(8, 4) != "WEBP") return {};
        QByteArray frameType, framePayload;
        for (qsizetype offset = 12; offset + 8 <= bytes.size();) {
            const auto type = bytes.mid(offset, 4); quint32 size = 0;
            for (int i = 0; i < 4; ++i) size |= quint32(quint8(bytes[offset + 4 + i])) << (8 * i);
            if ((type == "VP8 " || type == "VP8L") && offset + 8 + size <= bytes.size()) {
                frameType = type; framePayload = bytes.mid(offset + 8, size); break;
            }
            offset += 8 + size + (size & 1);
        }
        if (frameType.isEmpty() || framePayload.isEmpty()) return {};
        auto chunk = [](const QByteArray &type, const QByteArray &payload) {
            QByteArray result = type; const quint32 size = quint32(payload.size());
            for (int i = 0; i < 4; ++i) result.append(char((size >> (8 * i)) & 0xff));
            result += payload; if (size & 1) result.append(char(0)); return result;
        };
        auto frame = [&](quint32 duration) {
            QByteArray header(16, '\0');
            auto set24 = [&](int offset, quint32 value) { for (int i = 0; i < 3; ++i) header[offset + i] = char((value >> (8 * i)) & 0xff); };
            set24(6, 8); set24(9, 6); set24(12, duration);
            return chunk("ANMF", header + chunk(frameType, framePayload));
        };
        QByteArray extended(10, '\0'); extended[0] = char(0x02); extended[4] = char(8); extended[7] = char(6);
        QByteArray container = chunk("VP8X", extended) + chunk("ANIM", QByteArray(6, '\0')) + frame(50) + frame(50);
        QByteArray outputBytes = "RIFF"; const quint32 riffSize = quint32(container.size() + 4);
        for (int i = 0; i < 4; ++i) outputBytes.append(char((riffSize >> (8 * i)) & 0xff));
        outputBytes += "WEBP" + container;
        const auto animated = directory + "/animated.webp";
        QFile output(animated); if (!output.open(QIODevice::WriteOnly)) return {};
        return output.write(outputBytes) == outputBytes.size() ? animated : QString();
    }
    static void makeOwnedCacheFiles(const QString &cacheDirectory, int count) {
        QVERIFY(QDir().mkpath(cacheDirectory + "/thumbnails-v1"));
        for (int i = 0; i < count; ++i) {
            const auto name = QStringLiteral("%1.png").arg(i, 64, 16, QLatin1Char('0'));
            QFile file(cacheDirectory + "/thumbnails-v1/" + name);
            QVERIFY(file.open(QIODevice::WriteOnly));
            file.close();
        }
    }
    static int cacheCount(const QString &cacheDirectory) {
        return QDir(cacheDirectory + "/thumbnails-v1").entryList({"*.png"}, QDir::Files | QDir::NoSymLinks).size();
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
        const auto animatedWebp = animatedWebpProbe(dir.path()); QVERIFY(!animatedWebp.isEmpty());
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
        QVERIFY(imaging.enqueue(animation, 0, "animation")); QVERIFY(imaging.enqueue(animatedWebp, 0, "webp-animation")); QVERIFY(imaging.enqueue(large, 0, "oversize"));
        QTRY_COMPARE_WITH_TIMEOUT(spy.size(), 3, 10000);
        for (const auto &result : spy) {
            QVERIFY(!qvariant_cast<FramePtr>(result[1]));
            const auto expected = result[0] == "oversize" ? QStringLiteral("256 MiB") : QStringLiteral("動畫");
            QVERIFY2(result[2].toString().contains(expected), qPrintable(result[2].toString()));
        }
    }
    void animatedWebpAlphaAndCorruptionAreRecognized() {
        QTemporaryDir dir; QVERIFY(dir.isValid());
        const auto animated = animatedWebpProbe(dir.path()); QVERIFY(!animated.isEmpty());
        QImageReader animatedReader(animated); QVERIFY(animatedReader.canRead()); QVERIFY(animatedReader.supportsAnimation()); QCOMPARE(animatedReader.imageCount(), 2);
        const auto staticImage = dir.path() + "/plain.webp";
        QImage plain(9, 7, QImage::Format_RGB32); plain.fill(Qt::yellow); QVERIFY(plain.save(staticImage, "webp"));
        QImageReader staticReader(staticImage); QVERIFY(staticReader.canRead()); QVERIFY(!staticReader.supportsAnimation() || staticReader.imageCount() == 1);
        const auto transparent = dir.path() + "/transparent.png";
        QImage alpha(11, 13, QImage::Format_RGBA8888); alpha.fill(QColor(12, 34, 56, 0)); alpha.setPixelColor(10, 12, QColor(12, 34, 56, 127)); QVERIFY(alpha.save(transparent));
        QImageReader reader(transparent); QVERIFY(reader.canRead()); QCOMPARE(reader.size(), QSize(11, 13));
        const auto decoded = reader.read(); QVERIFY(!decoded.isNull()); QCOMPARE(decoded.pixelColor(0, 0).alpha(), 0); QCOMPARE(decoded.pixelColor(10, 12).alpha(), 127);
        const auto corrupt = dir.path() + "/corrupt.png"; QFile broken(corrupt); QVERIFY(broken.open(QIODevice::WriteOnly)); broken.write("not an image"); broken.close();
        QImageReader brokenReader(corrupt); QVERIFY(!brokenReader.canRead());
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
    void sameInputRequestsHaveIndependentResults() {
        QTemporaryDir dir; QVERIFY(dir.isValid()); const auto path = fixture(dir.path()); QVERIFY(!path.isEmpty());
        Imaging imaging(QString::fromUtf8(PICLENS_WORKER), dir.path() + "/cache"); QSignalSpy spy(&imaging, &Imaging::completed);
        QVERIFY(imaging.enqueue(path, 96, "same-a")); QVERIFY(imaging.enqueue(path, 96, "same-b"));
        QTRY_COMPARE_WITH_TIMEOUT(spy.size(), 2, 10000); QCOMPARE(imaging.outstanding(), 0);
        const auto first = qvariant_cast<FramePtr>(spy[0][1]); const auto second = qvariant_cast<FramePtr>(spy[1][1]); QVERIFY(first); QVERIFY(second);
        QCOMPARE(first->path, second->path); QCOMPARE(first->size, second->size); QCOMPARE(first->image, second->image);
    }
    void recentCacheEvictsOldEntriesPast32MiB() {
        QTemporaryDir dir; QVERIFY(dir.isValid()); const auto source = fixture(dir.path(), {1000, 1000}); QVERIFY(!source.isEmpty());
        QStringList paths;
        for (int i = 0; i < 9; ++i) { const auto path = dir.path() + "/thumb" + QString::number(i) + ".png"; QVERIFY(QFile::copy(source, path)); paths.append(path); }
        const auto cache = dir.path() + "/cache"; const auto marker = dir.path() + "/disable-worker"; const auto wrapper = dir.path() + "/worker-wrapper";
        QFile launcher(wrapper); QVERIFY(launcher.open(QIODevice::WriteOnly));
        const QByteArray script = "#!/bin/sh\nif [ -e \"" + marker.toUtf8() + "\" ]; then exit 91; fi\nexec \"" + QString::fromUtf8(PICLENS_WORKER).toUtf8() + "\" \"$@\"\n";
        QVERIFY(launcher.write(script) == script.size()); launcher.close();
        QVERIFY(QFile::setPermissions(wrapper, QFileDevice::ReadOwner | QFileDevice::WriteOwner | QFileDevice::ExeOwner));
        Imaging imaging(wrapper, cache); QSignalSpy spy(&imaging, &Imaging::completed);
        for (int i = 0; i < paths.size(); ++i) {
            QVERIFY(imaging.enqueue(paths[i], 1000, QStringLiteral("lru-%1").arg(i)));
            QTRY_COMPARE_WITH_TIMEOUT(spy.size(), i + 1, 10000);
        }
        for (const auto &file : QDir(cache + "/thumbnails-v1").entryInfoList({"*.png"}, QDir::Files)) QVERIFY(QFile::remove(file.absoluteFilePath()));
        QFile disabled(marker); QVERIFY(disabled.open(QIODevice::WriteOnly)); disabled.close();
        QVERIFY(imaging.enqueue(paths.last(), 1000, "recent")); QVERIFY(imaging.enqueue(paths.first(), 1000, "evicted"));
        QTRY_COMPARE_WITH_TIMEOUT(spy.size(), 11, 10000);
        FramePtr recent, evicted; QString evictedError;
        for (const auto &result : spy) {
            if (result[0].toString() == "recent") recent = qvariant_cast<FramePtr>(result[1]);
            if (result[0].toString() == "evicted") { evicted = qvariant_cast<FramePtr>(result[1]); evictedError = result[2].toString(); }
        }
        QVERIFY(recent); QVERIFY(!evicted); QVERIFY(!evictedError.isEmpty()); QCOMPARE(imaging.outstanding(), 0);
        imaging.shutdown(); QVERIFY(QFile::remove(marker));
        const auto smallSource = fixture(dir.path(), {20, 20}); QVERIFY(!smallSource.isEmpty()); QStringList smallPaths;
        for (int i = 0; i < 257; ++i) { const auto path = dir.path() + "/small" + QString::number(i) + ".png"; QVERIFY(QFile::copy(smallSource, path)); smallPaths.append(path); }
        const auto smallCache = dir.path() + "/small-cache"; Imaging capacity(wrapper, smallCache); QSignalSpy capacitySpy(&capacity, &Imaging::completed);
        for (int i = 0; i < smallPaths.size(); ++i) {
            QVERIFY(capacity.enqueue(smallPaths[i], 16, QStringLiteral("capacity-%1").arg(i)));
            QTRY_COMPARE_WITH_TIMEOUT(capacitySpy.size(), i + 1, 10000);
        }
        for (const auto &file : QDir(smallCache + "/thumbnails-v1").entryInfoList({"*.png"}, QDir::Files)) QVERIFY(QFile::remove(file.absoluteFilePath()));
        QFile disabledAgain(marker); QVERIFY(disabledAgain.open(QIODevice::WriteOnly)); disabledAgain.close();
        QVERIFY(capacity.enqueue(smallPaths.last(), 16, "capacity-recent")); QVERIFY(capacity.enqueue(smallPaths.first(), 16, "capacity-evicted"));
        QTRY_COMPARE_WITH_TIMEOUT(capacitySpy.size(), 259, 10000);
        FramePtr capacityRecent, capacityEvicted; QString capacityError;
        for (const auto &result : capacitySpy) {
            if (result[0].toString() == "capacity-recent") capacityRecent = qvariant_cast<FramePtr>(result[1]);
            if (result[0].toString() == "capacity-evicted") { capacityEvicted = qvariant_cast<FramePtr>(result[1]); capacityError = result[2].toString(); }
        }
        QVERIFY(capacityRecent); QVERIFY(!capacityEvicted); QVERIFY(!capacityError.isEmpty()); QCOMPARE(capacity.outstanding(), 0);
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
    void startupAndDirtyCachePruneKeepsTwoThousandOwnedEntries() {
        QTemporaryDir dir; QVERIFY(dir.isValid()); const auto path = fixture(dir.path()); QVERIFY(!path.isEmpty());
        const auto cache = dir.path() + "/cache"; makeOwnedCacheFiles(cache, 2000); QCOMPARE(cacheCount(cache), 2000);
        Imaging imaging(QString::fromUtf8(PICLENS_WORKER), cache); QSignalSpy spy(&imaging, &Imaging::completed);
        QVERIFY(imaging.enqueue(path, 96, "dirty-cache")); QTRY_COMPARE_WITH_TIMEOUT(spy.size(), 1, 10000); QVERIFY(qvariant_cast<FramePtr>(spy[0][1]));
        QTRY_COMPARE_WITH_TIMEOUT(cacheCount(cache), 2001, 3000);
        QTRY_COMPARE_WITH_TIMEOUT(cacheCount(cache), 2000, 8000);
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
