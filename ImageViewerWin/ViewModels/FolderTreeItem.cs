using System.Collections.ObjectModel;

namespace ImageViewerWin.ViewModels;

public sealed class FolderTreeItem
{
    public FolderTreeItem(string name, string path, bool isReadable = true, bool isExpanded = false)
    {
        Name = name;
        Path = path;
        IsReadable = isReadable;
        IsExpanded = isExpanded;
    }

    public string Name { get; }
    public string Path { get; }
    public bool IsReadable { get; }
    public bool IsExpanded { get; }
    public ObservableCollection<FolderTreeItem> Children { get; } = [];
}
