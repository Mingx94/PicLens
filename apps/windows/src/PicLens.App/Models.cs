using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Media.Imaging;
using PicLens.Core;
namespace PicLens.App;

public sealed class ResetCollection<T> : ObservableCollection<T>
{
    public void Reset(IEnumerable<T> values)
    {
        Items.Clear(); foreach (var value in values) Items.Add(value);
        OnPropertyChanged(new(nameof(Count))); OnPropertyChanged(new("Item[]")); OnCollectionChanged(new(NotifyCollectionChangedAction.Reset));
    }
}
public sealed class TileModel(LibraryEntry entry) : Observable
{
    public LibraryEntry Entry { get; } = entry;
    public string Path => Entry.Path;
    public string Name => Entry.Name;
    public bool IsFolder => Entry.IsFolder;
    public string Detail => IsFolder ? "資料夾" : Entry.Animated ? "動畫 · 不支援預覽" : Entry.Extension.TrimStart('.').ToUpperInvariant();
    public Controls.IconKind PlaceholderIcon => IsFolder ? Controls.IconKind.Folder : Entry.Animated ? Controls.IconKind.Play : Controls.IconKind.Image;
    bool selected; BitmapSource? image; string? error;
    public bool Selected { get => selected; set => Set(ref selected, value); }
    public BitmapSource? Image { get => image; set => Set(ref image, value); }
    public string? Error { get => error; set => Set(ref error, value); }
}
public sealed class FolderNode(string path) : Observable
{
    public string Path { get; } = path;
    public string Name => System.IO.Path.GetFileName(Path);
    public ObservableCollection<FolderNode> Children { get; } = [];
    public bool Loaded { get; set; }
}
