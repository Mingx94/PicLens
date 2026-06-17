using System.Runtime.InteropServices;
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

    private static int NaturalCompare(string left, string right) => StrCmpLogicalW(left, right);

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int StrCmpLogicalW(string psz1, string psz2);
}
