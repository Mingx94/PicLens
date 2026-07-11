#include <piclens/core/file_rename_planner.h>

#include <piclens/core/image_format_rules.h>
#include <piclens/core/path_rules.h>

#include <QDir>
#include <QFileInfo>
#include <QSet>

#include <algorithm>
#include <stdexcept>

namespace piclens::core::file_rename_planner {
namespace {

bool containsReservedFileNameCharacter(const QString &fileName)
{
    static const QSet<QChar> reservedCharacters{
        QLatin1Char('<'),
        QLatin1Char('>'),
        QLatin1Char(':'),
        QLatin1Char('"'),
        QLatin1Char('/'),
        QLatin1Char('\\'),
        QLatin1Char('|'),
        QLatin1Char('?'),
        QLatin1Char('*'),
    };

    return std::any_of(fileName.cbegin(), fileName.cend(), [&](QChar character) {
        return reservedCharacters.contains(character)
            || character.category() == QChar::Other_Control;
    });
}

std::optional<int> tryExtractSequenceNumber(
    const QString &targetPath,
    const QString &targetBaseName)
{
    const QString targetName = QFileInfo(targetPath).completeBaseName();
    const QString prefix = targetBaseName + QLatin1Char('-');
    if (!targetName.startsWith(prefix, path_rules::pathCaseSensitivity())) {
        return std::nullopt;
    }

    const QString suffix = targetName.sliced(prefix.size());
    if (suffix.isEmpty()
        || !std::all_of(suffix.cbegin(), suffix.cend(), [](QChar character) {
               return character.isDigit();
           })) {
        return std::nullopt;
    }

    bool parsed = false;
    const int sequenceNumber = suffix.toInt(&parsed);
    return parsed ? std::optional<int>{sequenceNumber} : std::nullopt;
}

int extractSequenceNumber(const QString &targetPath, const QString &targetBaseName)
{
    const auto value = tryExtractSequenceNumber(targetPath, targetBaseName);
    if (!value.has_value()) {
        throw std::invalid_argument("Target path must include a target sequence number.");
    }
    return *value;
}

QString createSequenceTargetPath(
    const QString &sourcePath,
    const QString &targetDirectory,
    const QString &targetBaseName,
    int sequenceNumber)
{
    const QString sourceSuffix = QFileInfo(sourcePath).suffix();
    const QString extension = sourceSuffix.isEmpty()
        ? QString()
        : QLatin1Char('.') + sourceSuffix;
    const QString fileName = QStringLiteral("%1-%2%3")
                                 .arg(targetBaseName)
                                 .arg(sequenceNumber, 2, 10, QLatin1Char('0'))
                                 .arg(extension);
    return QDir(targetDirectory).filePath(fileName);
}

QString nextAvailableSequenceTargetPath(
    const QString &sourcePath,
    const QString &targetDirectory,
    const QString &targetBaseName,
    int sequenceNumber,
    const QVector<QString> &existingPaths)
{
    int candidateSequence = sequenceNumber;
    while (true) {
        const QString candidatePath = createSequenceTargetPath(
            sourcePath,
            targetDirectory,
            targetBaseName,
            candidateSequence);
        if (!path_rules::targetNameExists(existingPaths, candidatePath, sourcePath)) {
            return candidatePath;
        }
        ++candidateSequence;
    }
}

DropTargetBatchRenamePlanItem createPlanItem(
    const QString &sourcePath,
    const QString &targetDirectory,
    const QString &targetBaseName,
    int sequenceNumber,
    const QVector<QString> &existingPaths)
{
    const auto sourceSequenceNumber = tryExtractSequenceNumber(sourcePath, targetBaseName);
    if (sourceSequenceNumber.has_value()
        && *sourceSequenceNumber < sequenceNumber
        && !path_rules::targetNameExists(existingPaths, sourcePath, sourcePath)) {
        return {
            .sourcePath = sourcePath,
            .targetPath = sourcePath,
            .shouldSkip = true,
            .reason = AlreadyTargetSequenceReason,
        };
    }

    const QString nextTargetPath = nextAvailableSequenceTargetPath(
        sourcePath,
        targetDirectory,
        targetBaseName,
        sequenceNumber,
        existingPaths);
    if (path_rules::pathEquals(sourcePath, nextTargetPath)) {
        return {
            .sourcePath = sourcePath,
            .targetPath = nextTargetPath,
            .shouldSkip = true,
            .reason = AlreadyTargetSequenceReason,
        };
    }

    return {
        .sourcePath = sourcePath,
        .targetPath = nextTargetPath,
        .shouldSkip = false,
        .reason = std::nullopt,
    };
}

} // namespace

FileNameValidationResult validateImageFileName(const QString &fileName)
{
    if (fileName.trimmed().isEmpty()) {
        return {.reason = QStringLiteral("empty_name")};
    }

    if (containsReservedFileNameCharacter(fileName)) {
        return {.reason = QStringLiteral("invalid_file_name")};
    }

    if (!image_format_rules::supportedImageExtension(fileName).has_value()) {
        return {.reason = QStringLiteral("unsupported_extension")};
    }

    return {.isValid = true, .reason = std::nullopt};
}

DropTargetBatchRenamePlan planDropTargetBatchRename(
    const QVector<QString> &sourcePaths,
    const QString &targetPath,
    const QVector<QString> &existingPaths)
{
    const QFileInfo targetInfo(targetPath);
    const QString targetDirectory = targetInfo.absolutePath();
    const QString targetBaseName = targetInfo.completeBaseName();

    QVector<DropTargetBatchRenamePlanItem> items;
    int sequenceNumber = 1;

    for (const QString &sourcePath : sourcePaths) {
        if (path_rules::pathEquals(sourcePath, targetPath)) {
            continue;
        }

        DropTargetBatchRenamePlanItem item = createPlanItem(
            sourcePath,
            targetDirectory,
            targetBaseName,
            sequenceNumber,
            existingPaths);
        sequenceNumber = std::max(
            sequenceNumber,
            extractSequenceNumber(item.targetPath, targetBaseName) + 1);
        items.append(std::move(item));
    }

    return {
        .total = static_cast<int>(items.size()),
        .items = std::move(items),
    };
}

} // namespace piclens::core::file_rename_planner
