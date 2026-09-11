using System.ComponentModel;
using System.Runtime.CompilerServices;
namespace PicLens.Core;

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; Changed(name); return true;
    }
}
public sealed record LibraryEntry(string Path, string Name, bool IsFolder, long Modified, long Size, bool Animated = false)
{
    public string Extension => System.IO.Path.GetExtension(Path).ToLowerInvariant();
    public string SearchText { get; } = (Name + "\n" + Path).ToUpperInvariant();
}
public static class Formats
{
    public static readonly string[] Extensions = [".jpg", ".jpeg", ".png", ".bmp", ".webp", ".gif"];
    public static bool Supported(string path) => Extensions.Contains(System.IO.Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
}
public sealed record SortSettings { public int Key { get; init; } public int Direction { get; init; } }
public sealed record Settings
{
    public string? LastFolderPath { get; init; }
    public SortSettings Sort { get; init; } = new();
    public bool IncludeSubfolders { get; init; }
    public int ThumbnailSize { get; init; } = 160;
    public bool SidebarCollapsed { get; init; }
    public uint? WindowWidth { get; init; }
    public uint? WindowHeight { get; init; }
    public Settings Normalize() => this with
    {
        Sort = new() { Key = Sort?.Key == 1 ? 1 : 0, Direction = Sort?.Direction == 1 ? 1 : 0 },
        ThumbnailSize = ThumbnailSize == 0 ? 160 : (int)(Math.Round(Math.Clamp(ThumbnailSize, 120, 240) / 20d, MidpointRounding.AwayFromZero) * 20),
        WindowWidth = WindowWidth.HasValue && WindowHeight.HasValue ? Math.Max(800u, WindowWidth.Value) : null,
        WindowHeight = WindowWidth.HasValue && WindowHeight.HasValue ? Math.Max(600u, WindowHeight.Value) : null
    };
}
public sealed class NaturalComparer : IComparer<string>
{
    public static readonly NaturalComparer Instance = new();
    public int Compare(string? a, string? b)
    {
        a ??= ""; b ??= ""; int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsAsciiDigit(a[i]) && char.IsAsciiDigit(b[j]))
            {
                int ia = i, jb = j;
                while (i < a.Length && char.IsAsciiDigit(a[i])) i++;
                while (j < b.Length && char.IsAsciiDigit(b[j])) j++;
                int sa = ia, sb = jb;
                while (sa + 1 < i && a[sa] == '0') sa++;
                while (sb + 1 < j && b[sb] == '0') sb++;
                int c = (i - sa).CompareTo(j - sb);
                if (c == 0) c = a.AsSpan(sa, i - sa).SequenceCompareTo(b.AsSpan(sb, j - sb));
                if (c == 0) c = (j - jb).CompareTo(i - ia);
                if (c != 0) return c;
            }
            else
            {
                int c = char.ToUpperInvariant(a[i]).CompareTo(char.ToUpperInvariant(b[j]));
                if (c == 0) c = a[i].CompareTo(b[j]);
                if (c != 0) return c;
                i++; j++;
            }
        }
        return (a.Length - i).CompareTo(b.Length - j);
    }
}
public static class LibraryRules
{
    public static List<LibraryEntry> Sort(IEnumerable<LibraryEntry> entries, SortSettings sort, bool foldersFirst)
    {
        var comparer = Comparer<LibraryEntry>.Create((a, b) =>
        {
            if (foldersFirst && a.IsFolder != b.IsFolder) return a.IsFolder ? -1 : 1;
            int c = sort.Key == 1 ? a.Modified.CompareTo(b.Modified) : NaturalComparer.Instance.Compare(a.Name, b.Name);
            return sort.Direction == 1 ? -c : c;
        });
        return entries.OrderBy(x => x, comparer).ToList();
    }
    public static List<LibraryEntry> Search(IEnumerable<LibraryEntry> entries, string query)
    {
        var term = query.Trim().ToUpperInvariant();
        return entries.Where(x => x.SearchText.Contains(term, StringComparison.Ordinal)).ToList();
    }
}
public sealed class Selection
{
    readonly List<string> ordered = [];
    public IReadOnlyList<string> Ordered => ordered;
    public string? Anchor { get; private set; }
    public bool Contains(string path) => ordered.Contains(path, StringComparer.OrdinalIgnoreCase);
    public void Clear() { ordered.Clear(); Anchor = null; }
    public void Remove(string path) => ordered.RemoveAll(p => StringComparer.OrdinalIgnoreCase.Equals(p, path));
    public void Select(string path, IReadOnlyList<string> visible, bool control, bool shift)
    {
        if (shift && Anchor is not null && visible.Contains(Anchor))
        {
            int a = visible.ToList().IndexOf(Anchor), b = visible.ToList().IndexOf(path);
            if (b < 0) return;
            if (!control) ordered.Clear();
            foreach (string p in visible.Skip(Math.Min(a, b)).Take(Math.Abs(a - b) + 1)) if (!Contains(p)) ordered.Add(p);
        }
        else
        {
            if (!control) ordered.Clear();
            if (Contains(path)) ordered.RemoveAll(p => StringComparer.OrdinalIgnoreCase.Equals(p, path));
            else ordered.Add(path);
            Anchor = path;
        }
    }
}
public sealed class FolderHistory
{
    readonly List<string> paths = []; int index = -1;
    public bool CanBack => index > 0;
    public bool CanForward => index + 1 < paths.Count;
    public void Visit(string path)
    {
        if (index >= 0 && StringComparer.OrdinalIgnoreCase.Equals(paths[index], path)) return;
        paths.RemoveRange(index + 1, paths.Count - index - 1); paths.Add(path); index++;
    }
    public string? Back() => CanBack ? paths[--index] : null;
    public string? Forward() => CanForward ? paths[++index] : null;
}
