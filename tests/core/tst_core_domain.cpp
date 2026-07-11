#include <piclens/core/file_rename_planner.h>
#include <piclens/core/drag_interaction_rules.h>
#include <piclens/core/image_format_rules.h>
#include <piclens/core/list_item_sorter.h>
#include <piclens/core/models.h>
#include <piclens/core/path_rules.h>
#include <piclens/core/settings_rules.h>
#include <piclens/core/zoom_math.h>

#include <QDir>
#include <QFileInfo>
#include <QTemporaryDir>
#include <QTest>

#include <cmath>
#include <limits>

using namespace piclens::core;

namespace {

QString childPath(const QString &directory, const QString &name)
{
    return QDir(directory).filePath(name);
}

FolderListItem folder(const QString &name, qint64 modifiedAtMs)
{
    return {
        .path = childPath(QStringLiteral("C:/Images"), name),
        .name = name,
        .modifiedAtMs = modifiedAtMs,
    };
}

ImageListItem image(const QString &name, qint64 modifiedAtMs)
{
    return {
        .path = childPath(QStringLiteral("C:/Images"), name),
        .name = name,
        .extension = QFileInfo(name).suffix(),
        .modifiedAtMs = modifiedAtMs,
        .sizeBytes = 100,
    };
}

QStringList namesOf(const QVector<ListItem> &items)
{
    QStringList names;
    names.reserve(items.size());
    for (const ListItem &item : items) {
        names.append(list_item_sorter::itemName(item));
    }
    return names;
}

} // namespace

class CoreDomainTests final : public QObject
{
    Q_OBJECT

private slots:
    void supportedImageExtension_data();
    void supportedImageExtension();
    void sortKeepsFoldersFirstAndUsesNumericNameOrder();
    void sortMatchesExplorerLeadingZeroOrder();
    void sortByModifiedTimeDescending();
    void pathRulesDetectSameBasenameTargetConflicts();
    void pathRulesFollowCurrentOsCaseSensitivity();
    void settingsPatchMergesValues();
    void settingsPatchNormalizesThumbnailSize_data();
    void settingsPatchNormalizesThumbnailSize();
    void settingsNormalizationHandlesNonFiniteValues();
    void zoomMathClampsAndKeepsPointerAnchor();
    void imageSequenceSnapshotUsesValueSemantics();
    void fileOperationBatchCountsStatuses();
    void validateImageFileNameRejectsUnsafeNames_data();
    void validateImageFileNameRejectsUnsafeNames();
    void validateImageFileNameAcceptsSupportedLeafName();
    void renamePlanPreservesExtensionsAndReservesBasenames();
    void renamePlanAdvancesPastExistingSequenceBasenames();
    void renamePlanCompactsExistingSequenceIntoFirstGap();
    void renamePlanSkipsExistingSequenceWhenNoEarlierGapExists();
    void dragAutoScrollScalesAtEdgesAndIsCapped();
};

void CoreDomainTests::dragAutoScrollScalesAtEdgesAndIsCapped()
{
    using piclens::core::drag_interaction_rules::calculateAutoScrollDelta;
    QCOMPARE(calculateAutoScrollDelta(200, 400), 0.0);
    QCOMPARE(calculateAutoScrollDelta(36, 400), -24.0);
    QCOMPARE(calculateAutoScrollDelta(364, 400), 24.0);
    QCOMPARE(calculateAutoScrollDelta(-100, 400), -48.0);
    QCOMPARE(calculateAutoScrollDelta(500, 400), 48.0);
    QCOMPARE(calculateAutoScrollDelta(10, 0), 0.0);
    QCOMPARE(calculateAutoScrollDelta(std::numeric_limits<double>::quiet_NaN(), 400), 0.0);
}

void CoreDomainTests::supportedImageExtension_data()
{
    QTest::addColumn<QString>("path");
    QTest::addColumn<QString>("expected");

    QTest::addRow("jpg uppercase") << QStringLiteral("C:\\Images\\photo.JPG") << QStringLiteral("jpg");
    QTest::addRow("jpeg") << QStringLiteral("C:\\Images\\photo.jpeg") << QStringLiteral("jpeg");
    QTest::addRow("png") << QStringLiteral("C:\\Images\\photo.png") << QStringLiteral("png");
    QTest::addRow("bmp") << QStringLiteral("C:\\Images\\photo.bmp") << QStringLiteral("bmp");
    QTest::addRow("webp") << QStringLiteral("C:\\Images\\photo.webp") << QStringLiteral("webp");
    QTest::addRow("gif") << QStringLiteral("C:\\Images\\photo.gif") << QStringLiteral("gif");
    QTest::addRow("unsupported") << QStringLiteral("C:\\Images\\photo.avif") << QString();
    QTest::addRow("no extension") << QStringLiteral("C:\\Images\\README") << QString();
}

void CoreDomainTests::supportedImageExtension()
{
    QFETCH(QString, path);
    QFETCH(QString, expected);

    const auto extension = image_format_rules::supportedImageExtension(path);
    QCOMPARE(extension.value_or(QString()), expected);
    QCOMPARE(extension.has_value(), !expected.isNull());
}

void CoreDomainTests::sortKeepsFoldersFirstAndUsesNumericNameOrder()
{
    const QVector<ListItem> items{
        image(QStringLiteral("b10.jpg"), 10),
        folder(QStringLiteral("z-folder"), 5),
        image(QStringLiteral("b2.jpg"), 20),
        image(QStringLiteral("b1.jpg"), 30),
    };

    const auto sorted = list_item_sorter::sort(
        items,
        {.key = SortKey::Name, .direction = SortDirection::Asc},
        true);

    QCOMPARE(
        namesOf(sorted),
        QStringList({QStringLiteral("z-folder"), QStringLiteral("b1.jpg"), QStringLiteral("b2.jpg"), QStringLiteral("b10.jpg")}));
}

void CoreDomainTests::sortMatchesExplorerLeadingZeroOrder()
{
    const QVector<ListItem> items{
        image(QStringLiteral("img2.jpg"), 20),
        image(QStringLiteral("img02.jpg"), 30),
        image(QStringLiteral("img002.jpg"), 40),
        image(QStringLiteral("img10.jpg"), 50),
        image(QStringLiteral("img1.jpg"), 10),
    };

    const auto sorted = list_item_sorter::sort(
        items,
        {.key = SortKey::Name, .direction = SortDirection::Asc},
        false);

    QCOMPARE(
        namesOf(sorted),
        QStringList({QStringLiteral("img1.jpg"), QStringLiteral("img002.jpg"), QStringLiteral("img02.jpg"), QStringLiteral("img2.jpg"), QStringLiteral("img10.jpg")}));
}

void CoreDomainTests::sortByModifiedTimeDescending()
{
    const QVector<ListItem> items{
        image(QStringLiteral("old.jpg"), 1),
        image(QStringLiteral("new.jpg"), 20),
        image(QStringLiteral("middle.jpg"), 10),
    };

    const auto sorted = list_item_sorter::sort(
        items,
        {.key = SortKey::ModifiedAt, .direction = SortDirection::Desc},
        false);
    QCOMPARE(
        namesOf(sorted),
        QStringList({QStringLiteral("new.jpg"), QStringLiteral("middle.jpg"), QStringLiteral("old.jpg")}));
}

void CoreDomainTests::pathRulesDetectSameBasenameTargetConflicts()
{
    QTemporaryDir root;
    QTemporaryDir otherRoot;
    QVERIFY(root.isValid());
    QVERIFY(otherRoot.isValid());

    const QVector<QString> existingPaths{
        childPath(root.path(), QStringLiteral("target-01.png")),
        childPath(root.path(), QStringLiteral("source.jpg")),
        childPath(otherRoot.path(), QStringLiteral("target-01.webp")),
    };

    QVERIFY(path_rules::targetNameExists(
        existingPaths,
        childPath(root.path(), QStringLiteral("target-01.jpg")),
        childPath(root.path(), QStringLiteral("source.jpg"))));
    QVERIFY(!path_rules::targetNameExists(
        existingPaths,
        childPath(root.path(), QStringLiteral("source.webp")),
        childPath(root.path(), QStringLiteral("source.jpg"))));
    QVERIFY(!path_rules::targetNameExists(
        existingPaths,
        childPath(root.path(), QStringLiteral("target-02.jpg")),
        childPath(root.path(), QStringLiteral("source.jpg"))));
}

void CoreDomainTests::pathRulesFollowCurrentOsCaseSensitivity()
{
    QTemporaryDir root;
    QVERIFY(root.isValid());
    const QString lower = childPath(root.path(), QStringLiteral("photo.jpg"));
    const QString upper = childPath(root.path(), QStringLiteral("PHOTO.jpg"));

#ifdef Q_OS_WIN
    QVERIFY(path_rules::pathEquals(lower, upper));
#else
    QVERIFY(!path_rules::pathEquals(lower, upper));
#endif
}

void CoreDomainTests::settingsPatchMergesValues()
{
    AppSettingsPatch patch;
    patch.lastFolderPath = QStringLiteral("D:\\Manual");
    patch.hasLastFolderPath = true;
    patch.sort = SortState{.key = SortKey::ModifiedAt, .direction = SortDirection::Desc};
    patch.includeSubfolders = true;

    const AppSettings merged = settings_rules::mergeSettingsPatch(AppSettings::createDefault(), patch);

    QCOMPARE(merged.lastFolderPath.value_or(QString()), QStringLiteral("D:\\Manual"));
    QCOMPARE(merged.sort, SortState({.key = SortKey::ModifiedAt, .direction = SortDirection::Desc}));
    QVERIFY(merged.includeSubfolders);
    QCOMPARE(merged.thumbnailSize, settings_rules::DefaultThumbnailSize);
}

void CoreDomainTests::settingsPatchNormalizesThumbnailSize_data()
{
    QTest::addColumn<int>("requested");
    QTest::addColumn<int>("expected");

    QTest::addRow("minimum") << 64 << 120;
    QTest::addRow("round down") << 188 << 180;
    QTest::addRow("round down high") << 226 << 220;
    QTest::addRow("maximum") << 625 << 240;
}

void CoreDomainTests::settingsPatchNormalizesThumbnailSize()
{
    QFETCH(int, requested);
    QFETCH(int, expected);

    AppSettingsPatch patch;
    patch.thumbnailSize = requested;
    const AppSettings merged = settings_rules::mergeSettingsPatch(AppSettings::createDefault(), patch);
    QCOMPARE(merged.thumbnailSize, expected);
}

void CoreDomainTests::settingsNormalizationHandlesNonFiniteValues()
{
    QCOMPARE(
        settings_rules::normalizeThumbnailSize(std::numeric_limits<double>::quiet_NaN()),
        settings_rules::DefaultThumbnailSize);
    QCOMPARE(
        settings_rules::normalizeThumbnailSize(std::numeric_limits<double>::infinity()),
        settings_rules::DefaultThumbnailSize);
}

void CoreDomainTests::zoomMathClampsAndKeepsPointerAnchor()
{
    QCOMPARE(zoom_math::clampZoom(0.01), 0.1);
    QCOMPARE(zoom_math::clampZoom(80), 8.0);

    const ZoomState next = zoom_math::zoomAtPoint(
        1,
        {},
        {.x = 100, .y = 100},
        {.x = 120, .y = 100},
        1);

    QVERIFY(std::abs(next.zoom - 1.2) < 1e-10);
    QVERIFY(std::abs(next.offset.x - -4.0) < 1e-10);
    QVERIFY(std::abs(next.offset.y) < 1e-10);
}

void CoreDomainTests::imageSequenceSnapshotUsesValueSemantics()
{
    QVector<ImageListItem> images{image(QStringLiteral("one.jpg"), 1)};
    const ImageSequenceSnapshot snapshot{
        .sourceFolderPath = QStringLiteral("C:/Images"),
        .includeSubfolders = false,
        .sort = {},
        .images = images,
        .currentIndex = 0,
    };

    images.append(image(QStringLiteral("two.jpg"), 2));
    QCOMPARE(snapshot.images.size(), 1);
    QCOMPARE(snapshot.images.constFirst().name, QStringLiteral("one.jpg"));
}

void CoreDomainTests::fileOperationBatchCountsStatuses()
{
    const auto result = [](const QString &path, FileOperationStatus status) {
        return FileOperationResult{
            .path = path,
            .status = status,
            .targetPath = std::nullopt,
            .reason = std::nullopt,
            .message = std::nullopt,
        };
    };
    const FileOperationBatchResult batch{{
        result(QStringLiteral("a"), FileOperationStatus::Converted),
        result(QStringLiteral("b"), FileOperationStatus::Trashed),
        result(QStringLiteral("c"), FileOperationStatus::Renamed),
        result(QStringLiteral("d"), FileOperationStatus::Skipped),
        result(QStringLiteral("e"), FileOperationStatus::Failed),
    }};

    QCOMPARE(batch.total(), 5);
    QCOMPARE(batch.succeeded(), 3);
    QCOMPARE(batch.skipped(), 1);
    QCOMPARE(batch.failed(), 1);
}

void CoreDomainTests::validateImageFileNameRejectsUnsafeNames_data()
{
    QTest::addColumn<QString>("fileName");

    QTest::addRow("empty") << QString();
    QTest::addRow("whitespace") << QStringLiteral("   ");
    QTest::addRow("backslash") << QStringLiteral("nested\\photo.jpg");
    QTest::addRow("slash") << QStringLiteral("nested/photo.jpg");
    QTest::addRow("colon") << QStringLiteral("bad:name.jpg");
    QTest::addRow("unsupported") << QStringLiteral("photo.txt");
}

void CoreDomainTests::validateImageFileNameRejectsUnsafeNames()
{
    QFETCH(QString, fileName);
    const auto result = file_rename_planner::validateImageFileName(fileName);
    QVERIFY(!result.isValid);
    QVERIFY(result.reason.has_value());
}

void CoreDomainTests::validateImageFileNameAcceptsSupportedLeafName()
{
    const auto result = file_rename_planner::validateImageFileName(QStringLiteral("Renamed Photo.webp"));
    QVERIFY(result.isValid);
    QVERIFY(!result.reason.has_value());
}

void CoreDomainTests::renamePlanPreservesExtensionsAndReservesBasenames()
{
    QTemporaryDir root;
    QVERIFY(root.isValid());
    const QString target = childPath(root.path(), QStringLiteral("Album.jpg"));
    const QVector<QString> sources{
        childPath(root.path(), QStringLiteral("first.png")),
        target,
        childPath(root.path(), QStringLiteral("Album-02.gif")),
        childPath(root.path(), QStringLiteral("third.webp")),
    };
    const QVector<QString> existingPaths{
        sources.at(0),
        sources.at(2),
        sources.at(3),
        childPath(root.path(), QStringLiteral("Album-03.webp")),
    };

    const auto plan = file_rename_planner::planDropTargetBatchRename(sources, target, existingPaths);

    QCOMPARE(plan.total, 3);
    QCOMPARE(plan.items.at(0).sourcePath, sources.at(0));
    QCOMPARE(plan.items.at(0).targetPath, childPath(root.path(), QStringLiteral("Album-01.png")));
    QVERIFY(!plan.items.at(0).shouldSkip);
    QCOMPARE(plan.items.at(1).targetPath, sources.at(2));
    QVERIFY(plan.items.at(1).shouldSkip);
    QCOMPARE(plan.items.at(1).reason.value_or(QString()), file_rename_planner::AlreadyTargetSequenceReason);
    QCOMPARE(plan.items.at(2).targetPath, childPath(root.path(), QStringLiteral("Album-04.webp")));
    QVERIFY(!plan.items.at(2).shouldSkip);
}

void CoreDomainTests::renamePlanAdvancesPastExistingSequenceBasenames()
{
    QTemporaryDir root;
    QVERIFY(root.isValid());
    const QString target = childPath(root.path(), QStringLiteral("Album.jpg"));
    const QVector<QString> sources{
        childPath(root.path(), QStringLiteral("first.png")),
        childPath(root.path(), QStringLiteral("second.webp")),
    };
    const QVector<QString> existingPaths{
        childPath(root.path(), QStringLiteral("Album-01.jpg")),
        childPath(root.path(), QStringLiteral("Album-03.webp")),
    };

    const auto plan = file_rename_planner::planDropTargetBatchRename(sources, target, existingPaths);
    QCOMPARE(plan.items.at(0).targetPath, childPath(root.path(), QStringLiteral("Album-02.png")));
    QCOMPARE(plan.items.at(1).targetPath, childPath(root.path(), QStringLiteral("Album-04.webp")));
}

void CoreDomainTests::renamePlanCompactsExistingSequenceIntoFirstGap()
{
    QTemporaryDir root;
    QVERIFY(root.isValid());
    const QString target = childPath(root.path(), QStringLiteral("Album.jpg"));
    const QString source = childPath(root.path(), QStringLiteral("Album-03.jpg"));

    const auto plan = file_rename_planner::planDropTargetBatchRename({source}, target, {source});
    QCOMPARE(plan.items.size(), 1);
    QCOMPARE(plan.items.constFirst().targetPath, childPath(root.path(), QStringLiteral("Album-01.jpg")));
    QVERIFY(!plan.items.constFirst().shouldSkip);
}

void CoreDomainTests::renamePlanSkipsExistingSequenceWhenNoEarlierGapExists()
{
    QTemporaryDir root;
    QVERIFY(root.isValid());
    const QString target = childPath(root.path(), QStringLiteral("Album.jpg"));
    const QString source = childPath(root.path(), QStringLiteral("Album-03.jpg"));
    const QVector<QString> existingPaths{
        childPath(root.path(), QStringLiteral("Album-01.png")),
        childPath(root.path(), QStringLiteral("Album-02.webp")),
        source,
    };

    const auto plan = file_rename_planner::planDropTargetBatchRename({source}, target, existingPaths);
    QCOMPARE(plan.items.size(), 1);
    QVERIFY(plan.items.constFirst().shouldSkip);
    QCOMPARE(plan.items.constFirst().reason.value_or(QString()), file_rename_planner::AlreadyTargetSequenceReason);
}

QTEST_GUILESS_MAIN(CoreDomainTests)

#include "tst_core_domain.moc"
