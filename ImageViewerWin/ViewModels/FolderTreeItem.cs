using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace ImageViewerWin.ViewModels;

public sealed class FolderTreeItem : ObservableObject
{
    public FolderTreeItem(
        string name,
        string path,
        bool isReadable = true,
        bool isExpanded = false,
        bool isSelected = false)
    {
        Name = name;
        Path = path;
        IsReadable = isReadable;
        IsExpanded = isExpanded;
        IsSelected = isSelected;
    }

    public string Name { get; }
    public string Path { get; }
    public bool IsReadable { get; }
    public bool IsExpanded { get; }
    public bool IsSelected { get; }
    public ObservableCollection<FolderTreeItem> Children { get; } = [];
}
