using static PicLens.ViewModels.ViewModelPathRules;
using static PicLens.Core.Domain.PathRules;

namespace PicLens.ViewModels;

internal sealed class FolderNavigationHistory
{
    private readonly List<Entry> entries = [];
    private int index = -1;

    public bool CanBack => index > 0;

    public bool CanForward => index >= 0 && index < entries.Count - 1;

    public void Record(Entry entry, bool replace)
    {
        if (replace)
        {
            entries.Clear();
            entries.Add(entry);
            index = 0;
            return;
        }

        if (index >= 0 && EntryEquals(entries.ElementAtOrDefault(index), entry))
        {
            return;
        }

        if (index >= 0 && index < entries.Count - 1)
        {
            entries.RemoveRange(index + 1, entries.Count - index - 1);
        }

        entries.Add(entry);
        index = entries.Count - 1;
    }

    public Entry? Back()
    {
        if (!CanBack)
        {
            return null;
        }

        index -= 1;
        return entries[index];
    }

    public Entry? Forward()
    {
        if (!CanForward)
        {
            return null;
        }

        index += 1;
        return entries[index];
    }

    private static bool EntryEquals(Entry? left, Entry right) =>
        left is not null
        && PathEquals(left.FolderPath, right.FolderPath)
        && PathEquals(left.FolderTreeRootPath, right.FolderTreeRootPath);

    public sealed record Entry(string FolderPath, string FolderTreeRootPath);
}
