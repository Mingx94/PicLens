#include <piclens/core/list_item_sorter.h>

#include <QStringView>

#include <algorithm>

namespace piclens::core::list_item_sorter {
namespace {

int firstSignificantDigit(const QString &value, int start, int end)
{
    int index = start;
    while (index < end - 1 && value.at(index) == QLatin1Char('0')) {
        ++index;
    }
    return index;
}

int compareNumberRuns(
    const QString &left,
    int &leftIndex,
    const QString &right,
    int &rightIndex)
{
    const int leftStart = leftIndex;
    while (leftIndex < left.size() && left.at(leftIndex).isDigit()) {
        ++leftIndex;
    }

    const int rightStart = rightIndex;
    while (rightIndex < right.size() && right.at(rightIndex).isDigit()) {
        ++rightIndex;
    }

    const int leftSignificant = firstSignificantDigit(left, leftStart, leftIndex);
    const int rightSignificant = firstSignificantDigit(right, rightStart, rightIndex);
    const int leftSignificantLength = leftIndex - leftSignificant;
    const int rightSignificantLength = rightIndex - rightSignificant;

    if (leftSignificantLength != rightSignificantLength) {
        return leftSignificantLength < rightSignificantLength ? -1 : 1;
    }

    for (int index = 0; index < leftSignificantLength; ++index) {
        const ushort leftDigit = left.at(leftSignificant + index).unicode();
        const ushort rightDigit = right.at(rightSignificant + index).unicode();
        if (leftDigit != rightDigit) {
            return leftDigit < rightDigit ? -1 : 1;
        }
    }

    const int leftRunLength = leftIndex - leftStart;
    const int rightRunLength = rightIndex - rightStart;
    if (leftRunLength != rightRunLength) {
        return rightRunLength < leftRunLength ? -1 : 1;
    }

    return QStringView(left).sliced(leftStart, leftRunLength)
        .compare(QStringView(right).sliced(rightStart, rightRunLength), Qt::CaseSensitive);
}

int naturalCompare(const QString &left, const QString &right)
{
    int leftIndex = 0;
    int rightIndex = 0;

    while (leftIndex < left.size() && rightIndex < right.size()) {
        const QChar leftCharacter = left.at(leftIndex);
        const QChar rightCharacter = right.at(rightIndex);

        if (leftCharacter.isDigit() && rightCharacter.isDigit()) {
            const int result = compareNumberRuns(left, leftIndex, right, rightIndex);
            if (result != 0) {
                return result;
            }
            continue;
        }

        const ushort leftUpper = leftCharacter.toUpper().unicode();
        const ushort rightUpper = rightCharacter.toUpper().unicode();
        if (leftUpper != rightUpper) {
            return leftUpper < rightUpper ? -1 : 1;
        }

        if (leftCharacter != rightCharacter) {
            return leftCharacter.unicode() < rightCharacter.unicode() ? -1 : 1;
        }

        ++leftIndex;
        ++rightIndex;
    }

    const int leftRemaining = left.size() - leftIndex;
    const int rightRemaining = right.size() - rightIndex;
    if (leftRemaining == rightRemaining) {
        return 0;
    }
    return leftRemaining < rightRemaining ? -1 : 1;
}

} // namespace

QVector<ListItem> sort(
    const QVector<ListItem> &items,
    SortState sortState,
    bool keepFoldersFirst)
{
    QVector<ListItem> sorted = items;
    std::sort(sorted.begin(), sorted.end(), [&](const ListItem &left, const ListItem &right) {
        if (keepFoldersFirst && isFolder(left) != isFolder(right)) {
            return isFolder(left);
        }

        int result = 0;
        if (sortState.key == SortKey::Name) {
            result = naturalCompare(itemName(left), itemName(right));
        } else {
            const qint64 leftModified = itemModifiedAtMs(left).value_or(0);
            const qint64 rightModified = itemModifiedAtMs(right).value_or(0);
            result = leftModified < rightModified ? -1 : leftModified > rightModified ? 1 : 0;
        }

        return sortState.direction == SortDirection::Asc ? result < 0 : result > 0;
    });
    return sorted;
}

QString itemName(const ListItem &item)
{
    return std::visit([](const auto &value) { return value.name; }, item);
}

QString itemPath(const ListItem &item)
{
    return std::visit([](const auto &value) { return value.path; }, item);
}

std::optional<qint64> itemModifiedAtMs(const ListItem &item)
{
    return std::visit([](const auto &value) { return value.modifiedAtMs; }, item);
}

bool isFolder(const ListItem &item)
{
    return std::holds_alternative<FolderListItem>(item);
}

} // namespace piclens::core::list_item_sorter
