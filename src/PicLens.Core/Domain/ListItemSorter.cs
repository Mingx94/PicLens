using PicLens.Core.Models;
using CoreSortKey = PicLens.Core.Models.SortKey;

namespace PicLens.Core.Domain;

public static class ListItemSorter
{
    public static IReadOnlyList<ListItem> Sort(
        IEnumerable<ListItem> items,
        SortState sort,
        bool keepFoldersFirst)
    {
        var sorted = items.ToList();
        sorted.Sort((left, right) =>
        {
            if (left is null)
            {
                return right is null ? 0 : -1;
            }

            if (right is null)
            {
                return 1;
            }

            if (keepFoldersFirst && left.GetType() != right.GetType())
            {
                return left is FolderListItem ? -1 : 1;
            }

            var result = sort.Key == CoreSortKey.Name
                ? NaturalCompare(left.Name, right.Name)
                : (left.ModifiedAtMs ?? 0).CompareTo(right.ModifiedAtMs ?? 0);

            return sort.Direction == SortDirection.Asc ? result : -result;
        });

        return sorted;
    }

    private static int NaturalCompare(string left, string right)
    {
        var leftIndex = 0;
        var rightIndex = 0;

        while (leftIndex < left.Length && rightIndex < right.Length)
        {
            var leftChar = left[leftIndex];
            var rightChar = right[rightIndex];

            if (char.IsDigit(leftChar) && char.IsDigit(rightChar))
            {
                var result = CompareNumberRuns(left, ref leftIndex, right, ref rightIndex);
                if (result != 0)
                {
                    return result;
                }

                continue;
            }

            var charResult = char.ToUpperInvariant(leftChar).CompareTo(char.ToUpperInvariant(rightChar));
            if (charResult != 0)
            {
                return charResult;
            }

            charResult = leftChar.CompareTo(rightChar);
            if (charResult != 0)
            {
                return charResult;
            }

            leftIndex++;
            rightIndex++;
        }

        return (left.Length - leftIndex).CompareTo(right.Length - rightIndex);
    }

    private static int CompareNumberRuns(string left, ref int leftIndex, string right, ref int rightIndex)
    {
        var leftStart = leftIndex;
        while (leftIndex < left.Length && char.IsDigit(left[leftIndex]))
        {
            leftIndex++;
        }

        var rightStart = rightIndex;
        while (rightIndex < right.Length && char.IsDigit(right[rightIndex]))
        {
            rightIndex++;
        }

        var leftSignificant = FirstSignificantDigit(left, leftStart, leftIndex);
        var rightSignificant = FirstSignificantDigit(right, rightStart, rightIndex);
        var leftSignificantLength = leftIndex - leftSignificant;
        var rightSignificantLength = rightIndex - rightSignificant;

        var lengthResult = leftSignificantLength.CompareTo(rightSignificantLength);
        if (lengthResult != 0)
        {
            return lengthResult;
        }

        for (var i = 0; i < leftSignificantLength; i++)
        {
            var digitResult = left[leftSignificant + i].CompareTo(right[rightSignificant + i]);
            if (digitResult != 0)
            {
                return digitResult;
            }
        }

        var runLengthResult = (rightIndex - rightStart).CompareTo(leftIndex - leftStart);
        if (runLengthResult != 0)
        {
            return runLengthResult;
        }

        return left.AsSpan(leftStart, leftIndex - leftStart)
            .SequenceCompareTo(right.AsSpan(rightStart, rightIndex - rightStart));
    }

    private static int FirstSignificantDigit(string value, int start, int end)
    {
        var index = start;
        while (index < end - 1 && value[index] == '0')
        {
            index++;
        }

        return index;
    }
}
