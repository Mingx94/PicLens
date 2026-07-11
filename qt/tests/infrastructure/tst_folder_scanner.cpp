#include <piclens/core/list_item_sorter.h>
#include <piclens/infrastructure/folder_scanner.h>

#include <QDir>
#include <QFile>
#include <QTemporaryDir>
#include <QTest>

#include <algorithm>
#include <filesystem>
#include <stop_token>

#ifdef Q_OS_WIN
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>
#endif

using namespace piclens::core;
using namespace piclens::infrastructure;

namespace {

QString childPath(const QString &directory, const QString &name)
{
    return QDir(directory).filePath(name);
}

void writeFile(const QString &path, const QByteArray &bytes)
{
    QDir().mkpath(QFileInfo(path).absolutePath());
    QFile file(path);
    QVERIFY2(file.open(QIODevice::WriteOnly), qPrintable(file.errorString()));
    QCOMPARE(file.write(bytes), bytes.size());
}

QByteArray staticGifBytes()
{
    return QByteArray("GIF89a\0\0\0\0\0\x2c", 12);
}

QByteArray animatedGifBytes()
{
    return QByteArray::fromBase64(
        "R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAICRAEAIfkEAQAAAAAsAAAAAAEAAQAAAgJEADs=");
}

QByteArray animatedWebpBytes()
{
    return QByteArray::fromBase64(
        "UklGRp4AAABXRUJQVlA4WAoAAAASAAAAAQAAAQAAQU5JTQYAAAD/////AABBTk1GNgAAAAAAAAAAAAEAAAEAAPQBAAJWUDhMHgAAAC8BQAAAFzD/AoIi/0eb//kPNAsK27ZBYXEQ0f/IA0FOTUY0AAAAAAAAAAAAAQAAAQAA9AEAAFZQOEwcAAAALwFAABAXIBBIYZM//wKCIv9Hm/+AvcEYRPQ/BA==");
}

QStringList itemNames(const QVector<ListItem> &items)
{
    QStringList names;
    for (const auto &item : items) {
        names.append(list_item_sorter::itemName(item));
    }
    return names;
}

QVector<ImageListItem> imagesOnly(const QVector<ListItem> &items)
{
    QVector<ImageListItem> images;
    for (const auto &item : items) {
        if (const auto *image = std::get_if<ImageListItem>(&item)) {
            images.append(*image);
        }
    }
    return images;
}

bool createDirectoryAlias(const QString &aliasPath, const QString &targetPath, QString &errorMessage)
{
#ifdef Q_OS_WIN
    constexpr DWORD AllowUnprivilegedCreate = 0x2;
    const DWORD flags = SYMBOLIC_LINK_FLAG_DIRECTORY | AllowUnprivilegedCreate;
    if (CreateSymbolicLinkW(
            reinterpret_cast<LPCWSTR>(aliasPath.utf16()),
            reinterpret_cast<LPCWSTR>(targetPath.utf16()),
            flags)) {
        return true;
    }
    errorMessage = QStringLiteral("CreateSymbolicLinkW failed with error %1").arg(GetLastError());
    return false;
#else
    std::error_code error;
    std::filesystem::create_directory_symlink(
        QFileInfo(targetPath).filesystemAbsoluteFilePath(),
        QFileInfo(aliasPath).filesystemAbsoluteFilePath(),
        error);
    if (!error) {
        return true;
    }
    errorMessage = QString::fromStdString(error.message());
    return false;
#endif
}

bool removeDirectoryAlias(const QString &aliasPath)
{
#ifdef Q_OS_WIN
    return RemoveDirectoryW(reinterpret_cast<LPCWSTR>(aliasPath.utf16()));
#else
    std::error_code error;
    return std::filesystem::remove(QFileInfo(aliasPath).filesystemAbsoluteFilePath(), error) && !error;
#endif
}

} // namespace

class FolderScannerTests final : public QObject
{
    Q_OBJECT

private slots:
    void directModeReturnsFoldersAndSupportedImagesOnly();
    void recursiveModeReturnsDescendantImagesAndMarksAnimation();
    void invalidGifBytesAreNotMarkedAnimated();
    void lockedFilesRemainListedWithoutAnimation();
    void childFolderScanDoesNotInspectImageBodies();
    void canceledScanThrowsCancellationError();
    void missingDirectoryThrowsDirectoryError();
    void recursiveModeVisitsCanonicalDirectoriesOnce();
};

void FolderScannerTests::directModeReturnsFoldersAndSupportedImagesOnly()
{
    QTemporaryDir root;
    QVERIFY(root.isValid());
    QDir().mkpath(childPath(root.path(), QStringLiteral("Nested")));
    writeFile(childPath(root.path(), QStringLiteral("b10.jpg")), QByteArray("\x01\x02\x03", 3));
    writeFile(childPath(root.path(), QStringLiteral("b2.txt")), QByteArray("\x01\x02\x03", 3));
    writeFile(childPath(root.path(), QStringLiteral("loop.gif")), staticGifBytes());
    writeFile(childPath(root.path(), QStringLiteral("Nested/deep.png")), QByteArray("\x01\x02\x03", 3));

    const auto items = FolderScanner().scan({
        .folderPath = root.path(),
        .includeSubfolders = false,
        .sort = {.key = SortKey::Name, .direction = SortDirection::Asc},
    });

    QCOMPARE(
        itemNames(items),
        QStringList({QStringLiteral("Nested"), QStringLiteral("b10.jpg"), QStringLiteral("loop.gif")}));
    const auto images = imagesOnly(items);
    QCOMPARE(images.size(), 2);
    QVERIFY(!images.constLast().isAnimated);
    QVERIFY(!itemNames(items).contains(QStringLiteral("deep.png")));
}

void FolderScannerTests::recursiveModeReturnsDescendantImagesAndMarksAnimation()
{
    QTemporaryDir root;
    QVERIFY(root.isValid());
    QDir().mkpath(childPath(root.path(), QStringLiteral("Nested")));
    writeFile(childPath(root.path(), QStringLiteral("cover.gif")), animatedGifBytes());
    writeFile(childPath(root.path(), QStringLiteral("motion.webp")), animatedWebpBytes());
    writeFile(childPath(root.path(), QStringLiteral("Nested/z.png")), QByteArray("\x01\x02\x03", 3));
    writeFile(childPath(root.path(), QStringLiteral("Nested/ignored.txt")), QByteArray("\x01\x02\x03", 3));

    const auto items = FolderScanner().scan({
        .folderPath = root.path(),
        .includeSubfolders = true,
        .sort = {.key = SortKey::Name, .direction = SortDirection::Asc},
    });

    QCOMPARE(
        itemNames(items),
        QStringList({QStringLiteral("cover.gif"), QStringLiteral("motion.webp"), QStringLiteral("z.png")}));
    const auto images = imagesOnly(items);
    QCOMPARE(images.size(), items.size());
    const auto animatedGif = std::find_if(images.cbegin(), images.cend(), [](const ImageListItem &image) {
        return image.name == QStringLiteral("cover.gif");
    });
    const auto animatedWebp = std::find_if(images.cbegin(), images.cend(), [](const ImageListItem &image) {
        return image.name == QStringLiteral("motion.webp");
    });
    QVERIFY(animatedGif != images.cend());
    QVERIFY(animatedGif->isAnimated);
    QVERIFY(animatedWebp != images.cend());
    QVERIFY(animatedWebp->isAnimated);
}

void FolderScannerTests::invalidGifBytesAreNotMarkedAnimated()
{
    QTemporaryDir root;
    QVERIFY(root.isValid());
    writeFile(childPath(root.path(), QStringLiteral("not-a-gif.gif")), QByteArray::fromHex("002c012c02"));

    const auto items = FolderScanner().scan({
        .folderPath = root.path(),
        .includeSubfolders = false,
        .sort = {},
    });
    const auto images = imagesOnly(items);
    QCOMPARE(images.size(), 1);
    QVERIFY(!images.constFirst().isAnimated);
}

void FolderScannerTests::lockedFilesRemainListedWithoutAnimation()
{
#ifdef Q_OS_WIN
    QTemporaryDir root;
    QVERIFY(root.isValid());
    const QString gifPath = childPath(root.path(), QStringLiteral("locked.gif"));
    const QString jpgPath = childPath(root.path(), QStringLiteral("photo.jpg"));
    writeFile(gifPath, animatedGifBytes());
    writeFile(jpgPath, QByteArray("\x01\x02\x03", 3));

    const HANDLE gifHandle = CreateFileW(
        reinterpret_cast<LPCWSTR>(gifPath.utf16()),
        GENERIC_READ,
        0,
        nullptr,
        OPEN_EXISTING,
        FILE_ATTRIBUTE_NORMAL,
        nullptr);
    const HANDLE jpgHandle = CreateFileW(
        reinterpret_cast<LPCWSTR>(jpgPath.utf16()),
        GENERIC_READ,
        0,
        nullptr,
        OPEN_EXISTING,
        FILE_ATTRIBUTE_NORMAL,
        nullptr);
    QVERIFY(gifHandle != INVALID_HANDLE_VALUE);
    QVERIFY(jpgHandle != INVALID_HANDLE_VALUE);

    const auto items = FolderScanner().scan({
        .folderPath = root.path(),
        .includeSubfolders = false,
        .sort = {.key = SortKey::Name, .direction = SortDirection::Asc},
    });
    CloseHandle(gifHandle);
    CloseHandle(jpgHandle);

    QCOMPARE(itemNames(items), QStringList({QStringLiteral("locked.gif"), QStringLiteral("photo.jpg")}));
    const auto images = imagesOnly(items);
    QCOMPARE(images.size(), 2);
    QVERIFY(!images.constFirst().isAnimated);
#else
    QSKIP("Windows sharing-mode parity is covered only on Windows.");
#endif
}

void FolderScannerTests::childFolderScanDoesNotInspectImageBodies()
{
    QTemporaryDir root;
    QVERIFY(root.isValid());
    QDir().mkpath(childPath(root.path(), QStringLiteral("Nested")));
    writeFile(childPath(root.path(), QStringLiteral("invalid.gif")), QByteArray::fromHex("000102"));

    const auto folders = FolderScanner().scanChildFolders(root.path());
    QCOMPARE(folders.size(), 1);
    QCOMPARE(folders.constFirst().name, QStringLiteral("Nested"));
}

void FolderScannerTests::canceledScanThrowsCancellationError()
{
    QTemporaryDir root;
    QVERIFY(root.isValid());
    writeFile(childPath(root.path(), QStringLiteral("photo.jpg")), QByteArray("\x01\x02\x03", 3));
    std::stop_source cancellation;
    cancellation.request_stop();

    const auto canceledScan = [&] {
        (void)FolderScanner().scan(
            {.folderPath = root.path(), .includeSubfolders = false, .sort = {}},
            cancellation.get_token());
    };
    QVERIFY_EXCEPTION_THROWN(canceledScan(), ScanCanceledError);
}

void FolderScannerTests::missingDirectoryThrowsDirectoryError()
{
    QTemporaryDir root;
    QVERIFY(root.isValid());
    const QString missing = childPath(root.path(), QStringLiteral("missing"));
    const auto missingScan = [&] {
        (void)FolderScanner().scan({
            .folderPath = missing,
            .includeSubfolders = false,
            .sort = {},
        });
    };
    QVERIFY_EXCEPTION_THROWN(missingScan(), DirectoryNotFoundError);
}

void FolderScannerTests::recursiveModeVisitsCanonicalDirectoriesOnce()
{
    QTemporaryDir root;
    QVERIFY(root.isValid());
    const QString realFolder = childPath(root.path(), QStringLiteral("Real"));
    const QString aliasFolder = childPath(root.path(), QStringLiteral("Alias"));
    QDir().mkpath(realFolder);
    writeFile(childPath(realFolder, QStringLiteral("photo.jpg")), QByteArray("\x01\x02\x03", 3));

    QString aliasError;
    QVERIFY2(createDirectoryAlias(aliasFolder, realFolder, aliasError), qPrintable(aliasError));

    const auto items = FolderScanner().scan({
        .folderPath = root.path(),
        .includeSubfolders = true,
        .sort = {},
    });
    QVERIFY(removeDirectoryAlias(aliasFolder));
    QCOMPARE(itemNames(items), QStringList({QStringLiteral("photo.jpg")}));
}

QTEST_GUILESS_MAIN(FolderScannerTests)

#include "tst_folder_scanner.moc"
