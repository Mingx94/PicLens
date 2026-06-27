using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace PicLens.ViewModels;

public sealed class FolderTreeItem : ObservableObject
{
    private bool isExpanded;
    private bool isSelected;

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
        this.isExpanded = isExpanded;
        this.isSelected = isSelected;
    }

    public string Name { get; }
    public string Path { get; }
    public bool IsReadable { get; }

    public bool IsExpanded
    {
        get => isExpanded;
        set => SetProperty(ref isExpanded, value);
    }

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    public bool HasLoadedChildren { get; set; }

    public ObservableCollection<FolderTreeItem> Children { get; } = [];
}
