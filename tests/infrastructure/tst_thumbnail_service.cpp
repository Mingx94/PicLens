#include <piclens/infrastructure/app_data_paths.h>
#include <piclens/infrastructure/thumbnail_service.h>

#include <QDir>
#include <QFile>
#include <QImageReader>
#include <QTemporaryDir>
#include <QTest>

#include <algorithm>
#include <stop_token>

using namespace piclens::infrastructure;

namespace {

QString childPath(const QString &directory, const QString &name)
{
    return QDir(directory).filePath(name);
}

void writeFile(const QString &path, const QByteArray &bytes)
{
    QFile file(path);
    QVERIFY2(file.open(QIODevice::WriteOnly), qPrintable(file.errorString()));
    QCOMPARE(file.write(bytes), bytes.size());
}

QByteArray bmp(int width, int height)
{
    constexpr int fileHeaderSize = 14;
    constexpr int dibHeaderSize = 40;
    constexpr int bytesPerPixel = 3;
    const int rowStride = ((width * bytesPerPixel) + 3) & ~3;
    const int pixelArraySize = rowStride * height;
    const int fileSize = fileHeaderSize + dibHeaderSize + pixelArraySize;
    QByteArray bytes(fileSize, '\0');

    auto writeInt16 = [&](int offset, int value) {
        bytes[offset] = static_cast<char>(value);
        bytes[offset + 1] = static_cast<char>(value >> 8);
    };
    auto writeInt32 = [&](int offset, int value) {
        bytes[offset] = static_cast<char>(value);
        bytes[offset + 1] = static_cast<char>(value >> 8);
        bytes[offset + 2] = static_cast<char>(value >> 16);
        bytes[offset + 3] = static_cast<char>(value >> 24);
    };

    bytes[0] = 0x42;
    bytes[1] = 0x4d;
    writeInt32(2, fileSize);
    writeInt32(10, fileHeaderSize + dibHeaderSize);
    writeInt32(14, dibHeaderSize);
    writeInt32(18, width);
    writeInt32(22, height);
    writeInt16(26, 1);
    writeInt16(28, 24);
    writeInt32(34, pixelArraySize);
    for (int y = 0; y < height; ++y) {
        for (int x = 0; x < width; ++x) {
            const int offset = fileHeaderSize + dibHeaderSize + y * rowStride + x * bytesPerPixel;
            bytes[offset] = static_cast<char>(x * 255 / std::max(1, width - 1));
            bytes[offset + 1] = static_cast<char>(y * 255 / std::max(1, height - 1));
            bytes[offset + 2] = static_cast<char>(0x80);
        }
    }
    return bytes;
}

QByteArray animatedGif()
{
    return QByteArray::fromBase64(
        "R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAICRAEAIfkEAQAAAAAsAAAAAAEAAQAAAgJEADs=");
}

void setModificationTime(const QString &path, const QString &isoTimestamp)
{
    QFile file(path);
    QVERIFY(file.open(QIODevice::ReadWrite));
    QVERIFY(file.setFileTime(
        QDateTime::fromString(isoTimestamp, Qt::ISODate),
        QFileDevice::FileModificationTime));
}

} // namespace

class ThumbnailServiceTests final : public QObject
{
    Q_OBJECT

private slots:
    void defaultRootUsesPicLensThumbnailDirectory();
    void generatesBoundedPngAndPreservesSource();
    void cacheKeyTracksMetadataAndRequestedSize();
    void unsupportedMissingAnimatedAndCanceledInputsDoNotCreateCache();
    void corruptCacheEntryIsRegenerated();
    void pruneRemovesOldEntriesButKeepsNewThumbnail();
};

void ThumbnailServiceTests::defaultRootUsesPicLensThumbnailDirectory()
{
    ThumbnailService service;
    QVERIFY(service.cacheRoot().endsWith(
        QDir::toNativeSeparators(QStringLiteral("PicLens/Thumbnails")),
        Qt::CaseInsensitive)
        || service.cacheRoot().endsWith(QStringLiteral("PicLens/Thumbnails"), Qt::CaseInsensitive));
}

void ThumbnailServiceTests::generatesBoundedPngAndPreservesSource()
{
    QTemporaryDir root;
    QVERIFY(root.isValid());
    const QString source = childPath(root.path(), QStringLiteral("source.bmp"));
    const QString cache = childPath(root.path(), QStringLiteral("cache"));
    writeFile(source, bmp(20, 10));
    ThumbnailService service(cache);

    const ThumbnailResult generated = service.getOrCreate(source, 5);

    QCOMPARE(generated.status, ThumbnailStatus::Ready);
    QVERIFY(generated.cachePath.has_value());
    QVERIFY(generated.cachePath->startsWith(cache, Qt::CaseInsensitive));
    QVERIFY(QFileInfo::exists(*generated.cachePath));
    QVERIFY(QFileInfo::exists(source));
    QImageReader reader(*generated.cachePath);
    const QSize dimensions = reader.size();
    QVERIFY(dimensions.width() >= 1 && dimensions.width() <= 5);
    QVERIFY(dimensions.height() >= 1 && dimensions.height() <= 5);
    const QImage memoryImage = service.cachedImage(QFileInfo(*generated.cachePath).fileName());
    QCOMPARE(memoryImage.size(), dimensions);
    QVERIFY(service.cachedImage(QStringLiteral("../outside.png")).isNull());
}

void ThumbnailServiceTests::cacheKeyTracksMetadataAndRequestedSize()
{
    QTemporaryDir root;
    QVERIFY(root.isValid());
    const QString source = childPath(root.path(), QStringLiteral("source.bmp"));
    writeFile(source, bmp(20, 10));
    setModificationTime(source, QStringLiteral("2026-01-01T00:00:00Z"));
    ThumbnailService service(childPath(root.path(), QStringLiteral("cache")));

    const ThumbnailResult first = service.getOrCreate(source, 8);
    const ThumbnailResult repeated = service.getOrCreate(source, 8);
    const ThumbnailResult differentSize = service.getOrCreate(source, 12);
    setModificationTime(source, QStringLiteral("2026-01-01T00:00:02Z"));
    const ThumbnailResult changed = service.getOrCreate(source, 8);

    QVERIFY(first.cachePath.has_value());
    QCOMPARE(repeated.cachePath, first.cachePath);
    QVERIFY(repeated.cacheHit);
    QVERIFY(differentSize.cachePath != first.cachePath);
    QVERIFY(changed.cachePath != first.cachePath);
}

void ThumbnailServiceTests::unsupportedMissingAnimatedAndCanceledInputsDoNotCreateCache()
{
    QTemporaryDir root;
    QVERIFY(root.isValid());
    const QString text = childPath(root.path(), QStringLiteral("source.txt"));
    const QString gif = childPath(root.path(), QStringLiteral("loop.gif"));
    const QString cache = childPath(root.path(), QStringLiteral("cache"));
    writeFile(text, QByteArrayLiteral("text"));
    writeFile(gif, animatedGif());
    ThumbnailService service(cache);
    std::stop_source canceled;
    canceled.request_stop();

    QCOMPARE(service.getOrCreate(text, 8).status, ThumbnailStatus::Unsupported);
    QCOMPARE(
        service.getOrCreate(childPath(root.path(), QStringLiteral("missing.bmp")), 8).status,
        ThumbnailStatus::SourceMissing);
    QCOMPARE(service.getOrCreate(gif, 8).status, ThumbnailStatus::Animated);
    QCOMPARE(service.getOrCreate(gif, 8, canceled.get_token()).status, ThumbnailStatus::Canceled);
    QVERIFY(!QDir(cache).exists());
}

void ThumbnailServiceTests::corruptCacheEntryIsRegenerated()
{
    QTemporaryDir root;
    QVERIFY(root.isValid());
    const QString source = childPath(root.path(), QStringLiteral("source.bmp"));
    writeFile(source, bmp(20, 10));
    ThumbnailService service(childPath(root.path(), QStringLiteral("cache")));
    const ThumbnailResult first = service.getOrCreate(source, 8);
    QVERIFY(first.cachePath.has_value());
    writeFile(*first.cachePath, QByteArrayLiteral("not-png"));

    const ThumbnailResult regenerated = service.getOrCreate(source, 8);

    QCOMPARE(regenerated.status, ThumbnailStatus::Ready);
    QVERIFY(!regenerated.cacheHit);
    QImageReader reader(*regenerated.cachePath);
    QVERIFY(reader.canRead());
}

void ThumbnailServiceTests::pruneRemovesOldEntriesButKeepsNewThumbnail()
{
    QTemporaryDir root;
    QVERIFY(root.isValid());
    const QString source = childPath(root.path(), QStringLiteral("source.bmp"));
    const QString cache = childPath(root.path(), QStringLiteral("cache"));
    QVERIFY(QDir().mkpath(cache));
    const QString old = childPath(cache, QStringLiteral("old.png"));
    writeFile(source, bmp(20, 10));
    writeFile(old, QByteArray(4096, 'x'));
    setModificationTime(old, QStringLiteral("2026-01-01T00:00:00Z"));
    ThumbnailService service(cache, 1024);

    const ThumbnailResult generated = service.getOrCreate(source, 5);

    QCOMPARE(generated.status, ThumbnailStatus::Ready);
    QVERIFY(QFileInfo::exists(*generated.cachePath));
    QVERIFY(!QFileInfo::exists(old));
}

QTEST_GUILESS_MAIN(ThumbnailServiceTests)

#include "tst_thumbnail_service.moc"
