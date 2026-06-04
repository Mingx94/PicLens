using System.Globalization;
using System.Text.RegularExpressions;
using ImageViewerWin.Core.Models;
using CoreSortKey = ImageViewerWin.Core.Models.SortKey;

namespace ImageViewerWin.Core.Domain;

public sealed record SortOptions(bool KeepFoldersFirst);

public static partial class ListItemSorter
{
    public static IReadOnlyList<ListItem> Sort(
        IEnumerable<ListItem> items,
        SortState sort,
        SortOptions options)
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

            if (options.KeepFoldersFirst && left.GetType() != right.GetType())
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
        var leftParts = NumberRegex().Split(left);
        var rightParts = NumberRegex().Split(right);
        var length = Math.Min(leftParts.Length, rightParts.Length);

        for (var index = 0; index < length; index += 1)
        {
            var leftPart = leftParts[index];
            var rightPart = rightParts[index];
            var comparison = ComparePart(leftPart, rightPart);

            if (comparison != 0)
            {
                return comparison;
            }
        }

        return leftParts.Length.CompareTo(rightParts.Length);
    }

    private static int ComparePart(string left, string right)
    {
        var leftIsNumber = long.TryParse(left, NumberStyles.None, CultureInfo.InvariantCulture, out var leftNumber);
        var rightIsNumber = long.TryParse(right, NumberStyles.None, CultureInfo.InvariantCulture, out var rightNumber);

        if (leftIsNumber && rightIsNumber)
        {
            return leftNumber.CompareTo(rightNumber);
        }

        return string.Compare(left, right, CultureInfo.CurrentCulture, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace);
    }

    [GeneratedRegex("(\\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex NumberRegex();
}
