#include <piclens/infrastructure/file_operation_service.h>
#include <piclens/infrastructure/platform_file_manager.h>

#include <QDir>
#include <QFile>
#include <QTemporaryDir>
#include <QTest>

#include <algorithm>
#include <optional>
#include <stdexcept>
#include <stop_token>

using namespace piclens::core;
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

ImageListItem image(const QString &path, bool isAnimated = false)
{
    const QFileInfo info(path);
    return {
        .path = path,
        .name = info.fileName(),
        .extension = info.suffix().toLower(),
        .modifiedAtMs = std::nullopt,
        .sizeBytes = info.size(),
        .isAnimated = isAnimated,
    };
}

QByteArray onePixelBmp()
{
    return QByteArray::fromHex(
        "424d3a0000000000000036000000280000000100000001000000010018000000000004000000130b0000130b00000000000000000000ff00");
}

const FileOperationResult *findResult(
    const FileOperationBatchResult &result,
    const QString &path)
{
    const auto item = std::find_if(result.items.cbegin(), result.items.cend(), [&](const auto &candidate) {
        return candidate.path == path;
    });
    return item == result.items.cend() ? nullptr : &*item;
}

} // namespace

class FileOperationTests final : public QObject
{
    Q_OBJECT

private slots:
    void conversionPreservesOriginalsAndSkipsConservatively();
    void defaultEncoderWritesJpegBytes();
    void conversionFailureCleansPartialTargetAndContinues();
    void matchingNonJpgCleanupUsesInjectedTrash();
    void trashReportsMissingAndHandlerFailures();
    void singleRenameHandlesSameNameCollisionAndSuccess();
    void singleRenameRejectsInvalidRequests();
    void dropRenameUsesSelectionOrderAndSequenceGaps();
    void dropRenameContinuesAfterMissingSource();
    void cancellationStopsBeforeSideEffects();
    void revealBuildsPlatformRequestAndUsesLauncher();
};

void FileOperationTests::conversionPreservesOriginalsAndSkipsConservatively()
{
    QTemporaryDir root;
    QVERIFY(root.isValid());
    const QString png = childPath(root.path(), QStringLiteral("a.png"));
    const QString webp = childPath(root.path(), QStringLiteral("b.webp"));
    const QString jpg = childPath(root.path(), QStringLiteral("c.jpg"));
    const QString gif = childPath(root.path(), QStringLiteral("loop.gif"));
    writeFile(png, QByteArrayLiteral("png"));
    writeFile(webp, QByteArrayLiteral("webp"));
    writeFile(jpg, QByteArrayLiteral("jpg"));
    writeFile(gif, QByteArrayLiteral("gif"));
    writeFile(childPath(root.path(), QStringLiteral("b.jpg")), QByteArrayLiteral("existing"));

    QVector<QString> converted;
    FileOperationService service(
        [&](const QString &source, const QString &target, std::stop_token) {
            converted.append(source);
            writeFile(target, QByteArrayLiteral("encoded"));
        },
        [](const QString &, std::stop_token) {});

    const auto result = service.convertVisibleToJpg(
        {image(png), image(webp), image(jpg), image(gif, true)});

    QCOMPARE(result.total(), 4);
    QCOMPARE(result.succeeded(), 1);
    QCOMPARE(result.skipped(), 3);
    QCOMPARE(converted, QVector<QString>{png});
    QVERIFY(QFileInfo::exists(png));
    QVERIFY(QFileInfo::exists(childPath(root.path(), QStringLiteral("a.jpg"))));
    QCOMPARE(findResult(result, webp)->reason, std::optional<QString>{QStringLiteral("target_exists")});
    QCOMPARE(findResult(result, jpg)->reason, std::optional<QString>{QStringLiteral("already_jpg")});
    QCOMPARE(findResult(result, gif)->reason, std::optional<QString>{QStringLiteral("animated_unsupported")});
}

void FileOperationTests::defaultEncoderWritesJpegBytes()
{
    QTemporaryDir root;
    QVERIFY(root.isValid());
    const QString source = childPath(root.path(), QStringLiteral("source.bmp"));
    writeFile(source, onePixelBmp());

    const auto result = FileOperationService().convertVisibleToJpg({image(source)});

    QCOMPARE(result.succeeded(), 1);
    QFile output(childPath(root.path(), QStringLiteral("source.jpg")));
    QVERIFY(output.open(QIODevice::ReadOnly));
    const QByteArray bytes = output.readAll();
    QVERIFY(bytes.size() > 2);
    QCOMPARE(static_cast<unsigned char>(bytes[0]), static_cast<unsigned char>(0xff));
    QCOMPARE(static_cast<unsigned char>(bytes[1]), static_cast<unsigned char>(0xd8));
    QVERIFY(QFileInfo::exists(source));
}

void FileOperationTests::conversionFailureCleansPartialTargetAndContinues()
{
    QTemporaryDir root;
    QVERIFY(root.isValid());
    const QString first = childPath(root.path(), QStringLiteral("first.png"));
    const QString second = childPath(root.path(), QStringLiteral("second.png"));
    writeFile(first, QByteArrayLiteral("first"));
    writeFile(second, QByteArrayLiteral("second"));

    FileOperationService service(
        [&](const QString &source, const QString &target, std::stop_token) {
            writeFile(target, QByteArrayLiteral("partial"));
            if (source == first) {
                throw std::runtime_error("decoder failed");
            }
        },
        [](const QString &, std::stop_token) {});

    const auto result = service.convertVisibleToJpg({image(first), image(second)});

    QCOMPARE(result.failed(), 1);
    QCOMPARE(result.succeeded(), 1);
    QVERIFY(!QFileInfo::exists(childPath(root.path(), QStringLiteral("first.jpg"))));
    QVERIFY(QFileInfo::exists(childPath(root.path(), QStringLiteral("second.jpg"))));
    QCOMPARE(findResult(result, first)->reason, std::optional<QString>{QStringLiteral("conversion_failed")});
}

void FileOperationTests::matchingNonJpgCleanupUsesInjectedTrash()
{
    QTemporaryDir root;
    QVERIFY(root.isValid());
    const QString jpg = childPath(root.path(), QStringLiteral("a.jpg"));
    const QString png = childPath(root.path(), QStringLiteral("a.png"));
    const QString webp = childPath(root.path(), QStringLiteral("b.webp"));
    writeFile(jpg, QByteArrayLiteral("jpg"));
    writeFile(png, QByteArrayLiteral("png"));
    writeFile(webp, QByteArrayLiteral("webp"));

    QVector<QString> trashed;
    FileOperationService service(
        [](const QString &, const QString &, std::stop_token) {},
        [&](const QString &path, std::stop_token) { trashed.append(path); });

    const auto result = service.trashSameBasenameNonJpg({image(jpg), image(png), image(webp)});

    QCOMPARE(result.total(), 3);
    QCOMPARE(result.succeeded(), 1);
    QCOMPARE(result.skipped(), 2);
    QCOMPARE(trashed, QVector<QString>{png});
    QCOMPARE(findResult(result, jpg)->reason, std::optional<QString>{QStringLiteral("already_jpg")});
    QCOMPARE(findResult(result, webp)->reason, std::optional<QString>{QStringLiteral("no_matching_jpg")});
}

void FileOperationTests::trashReportsMissingAndHandlerFailures()
{
    QTemporaryDir root;
    QVERIFY(root.isValid());
    const QString path = childPath(root.path(), QStringLiteral("photo.jpg"));
    writeFile(path, QByteArrayLiteral("photo"));
    FileOperationService service(
        [](const QString &, const QString &, std::stop_token) {},
        [](const QString &, std::stop_token) { throw std::runtime_error("trash unavailable"); });

    const auto missing = service.trash(childPath(root.path(), QStringLiteral("missing.jpg")));
    const auto failed = service.trash(path);

    QCOMPARE(missing.reason, std::optional<QString>{QStringLiteral("source_missing")});
    QCOMPARE(failed.reason, std::optional<QString>{QStringLiteral("trash_failed")});
    QCOMPARE(failed.message, std::optional<QString>{QStringLiteral("trash unavailable")});
}

void FileOperationTests::singleRenameHandlesSameNameCollisionAndSuccess()
{
    QTemporaryDir root;
    QVERIFY(root.isValid());
    const QString source = childPath(root.path(), QStringLiteral("old.png"));
    writeFile(source, QByteArrayLiteral("source"));
    writeFile(childPath(root.path(), QStringLiteral("taken.png")), QByteArrayLiteral("taken"));
    FileOperationService service(
        [](const QString &, const QString &, std::stop_token) {},
        [](const QString &, std::stop_token) {});

    const auto same = service.rename(source, QStringLiteral("old.png"));
    const auto collision = service.rename(source, QStringLiteral("taken.png"));
    const auto renamed = service.rename(source, QStringLiteral("new.png"));

    QCOMPARE(same.status, FileOperationStatus::Skipped);
    QCOMPARE(same.reason, std::optional<QString>{QStringLiteral("same_name")});
    QCOMPARE(collision.status, FileOperationStatus::Failed);
    QCOMPARE(collision.reason, std::optional<QString>{QStringLiteral("invalid_request")});
    QCOMPARE(collision.message, std::optional<QString>{QStringLiteral("已有相同名稱的檔案。")});
    QCOMPARE(renamed.status, FileOperationStatus::Renamed);
    QVERIFY(QFileInfo::exists(childPath(root.path(), QStringLiteral("new.png"))));
    QVERIFY(!QFileInfo::exists(source));
}

void FileOperationTests::singleRenameRejectsInvalidRequests()
{
    QTemporaryDir root;
    QVERIFY(root.isValid());
    const QString source = childPath(root.path(), QStringLiteral("old.png"));
    const QString unsupported = childPath(root.path(), QStringLiteral("notes.txt"));
    writeFile(source, QByteArrayLiteral("source"));
    writeFile(unsupported, QByteArrayLiteral("notes"));
    FileOperationService service(
        [](const QString &, const QString &, std::stop_token) {},
        [](const QString &, std::stop_token) {});

    const auto separator = service.rename(source, QStringLiteral("folder/new.png"));
    const auto badExtension = service.rename(source, QStringLiteral("new.txt"));
    const auto badSource = service.rename(unsupported, QStringLiteral("new.png"));

    QCOMPARE(separator.reason, std::optional<QString>{QStringLiteral("invalid_request")});
    QCOMPARE(badExtension.message, std::optional<QString>{QStringLiteral("檔名必須使用支援的圖片副檔名。")});
    QCOMPARE(badSource.message, std::optional<QString>{QStringLiteral("路徑必須指向支援的圖片檔案。")});
}

void FileOperationTests::dropRenameUsesSelectionOrderAndSequenceGaps()
{
    QTemporaryDir root;
    QVERIFY(root.isValid());
    const QString target = childPath(root.path(), QStringLiteral("Album.jpg"));
    const QString first = childPath(root.path(), QStringLiteral("first.png"));
    const QString already = childPath(root.path(), QStringLiteral("Album-03.gif"));
    const QString second = childPath(root.path(), QStringLiteral("second.webp"));
    writeFile(target, QByteArrayLiteral("target"));
    writeFile(first, QByteArrayLiteral("first"));
    writeFile(already, QByteArrayLiteral("already"));
    writeFile(second, QByteArrayLiteral("second"));
    writeFile(childPath(root.path(), QStringLiteral("Album-01.jpg")), QByteArrayLiteral("occupied"));
    FileOperationService service(
        [](const QString &, const QString &, std::stop_token) {},
        [](const QString &, std::stop_token) {});

    const auto result = service.renameByDropTarget({first, target, already, second}, target);

    QCOMPARE(result.total(), 3);
    QCOMPARE(result.succeeded(), 2);
    QCOMPARE(result.skipped(), 1);
    QVERIFY(QFileInfo::exists(childPath(root.path(), QStringLiteral("Album-02.png"))));
    QVERIFY(QFileInfo::exists(childPath(root.path(), QStringLiteral("Album-04.webp"))));
    QVERIFY(QFileInfo::exists(already));
    QCOMPARE(findResult(result, already)->reason, std::optional<QString>{QStringLiteral("already_target_sequence")});
}

void FileOperationTests::dropRenameContinuesAfterMissingSource()
{
    QTemporaryDir root;
    QVERIFY(root.isValid());
    const QString target = childPath(root.path(), QStringLiteral("Album.jpg"));
    const QString missing = childPath(root.path(), QStringLiteral("missing.png"));
    const QString valid = childPath(root.path(), QStringLiteral("valid.webp"));
    writeFile(target, QByteArrayLiteral("target"));
    writeFile(valid, QByteArrayLiteral("valid"));
    FileOperationService service(
        [](const QString &, const QString &, std::stop_token) {},
        [](const QString &, std::stop_token) {});

    const auto result = service.renameByDropTarget({missing, valid}, target);

    QCOMPARE(result.failed(), 1);
    QCOMPARE(result.succeeded(), 1);
    QCOMPARE(findResult(result, missing)->reason, std::optional<QString>{QStringLiteral("source_missing")});
    QVERIFY(QFileInfo::exists(childPath(root.path(), QStringLiteral("Album-02.webp"))));
}

void FileOperationTests::cancellationStopsBeforeSideEffects()
{
    QTemporaryDir root;
    QVERIFY(root.isValid());
    const QString source = childPath(root.path(), QStringLiteral("photo.png"));
    writeFile(source, QByteArrayLiteral("photo"));
    int calls = 0;
    FileOperationService service(
        [&](const QString &, const QString &, std::stop_token) { ++calls; },
        [&](const QString &, std::stop_token) { ++calls; });
    std::stop_source cancellation;
    cancellation.request_stop();

    QVERIFY_EXCEPTION_THROWN(
        static_cast<void>(service.convertVisibleToJpg({image(source)}, cancellation.get_token())),
        FileOperationCanceledError);
    QCOMPARE(calls, 0);
}

void FileOperationTests::revealBuildsPlatformRequestAndUsesLauncher()
{
    QTemporaryDir root;
    QVERIFY(root.isValid());
    const QString folderWithSpaces = childPath(root.path(), QStringLiteral("folder with spaces"));
    QVERIFY(QDir().mkpath(folderWithSpaces));
    const QString source = childPath(folderWithSpaces, QStringLiteral("photo with spaces.jpg"));
    writeFile(source, QByteArrayLiteral("photo"));
    std::optional<ProcessLaunchRequest> launched;
    PlatformFileManager manager([&](const ProcessLaunchRequest &request) {
        launched = request;
        return true;
    });

    manager.reveal(source);

    QVERIFY(launched.has_value());
#ifdef Q_OS_WIN
    QCOMPARE(launched->program, QStringLiteral("explorer.exe"));
    QCOMPARE(launched->arguments, QStringList({
        QStringLiteral("/select,"),
        QDir::toNativeSeparators(source),
    }));
#elif defined(Q_OS_LINUX)
    QCOMPARE(launched->program, QStringLiteral("xdg-open"));
    QCOMPARE(launched->arguments, QStringList{folderWithSpaces});
#endif
    QVERIFY_EXCEPTION_THROWN(
        manager.reveal(childPath(root.path(), QStringLiteral("missing.jpg"))),
        std::runtime_error);
}

QTEST_GUILESS_MAIN(FileOperationTests)

#include "tst_file_operations.moc"
