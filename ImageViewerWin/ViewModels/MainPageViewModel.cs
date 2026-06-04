using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageViewerWin.Core.Domain;
using ImageViewerWin.Core.Models;
using System.Collections.ObjectModel;

namespace ImageViewerWin.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string CurrentFolderPath { get; set; } = @"C:\Users\Michael\Pictures";

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "Ready. Native ImageViewer shell initialized.";

    [ObservableProperty]
    public partial bool IncludeSubfolders { get; set; }

    [ObservableProperty]
    public partial SortState Sort { get; set; } = new(SortKey.Name, SortDirection.Asc);

    public ObservableCollection<FavoriteSidebarItem> Favorites { get; } = [];

    public ObservableCollection<FolderTreeItem> FolderRoots { get; } = [];

    public ObservableCollection<LibraryTileItem> LibraryItems { get; } = [];

    public MainPageViewModel()
    {
        SeedNativeShellState();
    }

    public string RecursiveModeLabel => IncludeSubfolders ? "Recursive" : "Direct";

    public string SortLabel => Sort.Key == SortKey.Name
        ? $"Name {Sort.Direction}"
        : $"Modified {Sort.Direction}";

    partial void OnIncludeSubfoldersChanged(bool value)
    {
        OnPropertyChanged(nameof(RecursiveModeLabel));
        StatusMessage = value
            ? "Recursive mode will list supported images under all child folders."
            : "Direct mode lists child folders and supported images in the selected folder.";
    }

    [RelayCommand]
    private void Back()
    {
        StatusMessage = "Back navigation is reserved for the folder history service.";
    }

    [RelayCommand]
    private void Forward()
    {
        StatusMessage = "Forward navigation is reserved for the folder history service.";
    }

    [RelayCommand]
    private void AddFavorite()
    {
        StatusMessage = "Add favorite will open the native folder picker.";
    }

    [RelayCommand]
    private void RefreshLibrary()
    {
        StatusMessage = $"Library refresh requested for {CurrentFolderPath}.";
    }

    [RelayCommand]
    private void ToggleSortDirection()
    {
        Sort = Sort with
        {
            Direction = Sort.Direction == SortDirection.Asc ? SortDirection.Desc : SortDirection.Asc
        };
        OnPropertyChanged(nameof(SortLabel));
        StatusMessage = $"Sort changed to {SortLabel}.";
    }

    [RelayCommand]
    private void ConvertVisible()
    {
        StatusMessage = "Convert visible non-JPG images to JPG will preserve originals and skip collisions.";
    }

    [RelayCommand]
    private void ClearSameBasename()
    {
        StatusMessage = "Clear same-name non-JPG files will confirm before moving files to trash.";
    }

    [RelayCommand]
    private void RenameSelected()
    {
        StatusMessage = "Rename selected image is enabled when exactly one visible image is selected.";
    }

    [RelayCommand]
    private void TrashSelected()
    {
        StatusMessage = "Move selected image to trash is enabled when exactly one visible image is selected.";
    }

    private void SeedNativeShellState()
    {
        Favorites.Add(new FavoriteSidebarItem("Pictures", @"C:\Users\Michael\Pictures", FavoriteSource.System, true));
        Favorites.Add(new FavoriteSidebarItem("Downloads", @"C:\Users\Michael\Downloads", FavoriteSource.System, true));
        Favorites.Add(new FavoriteSidebarItem("Desktop", @"C:\Users\Michael\Desktop", FavoriteSource.System, true));

        var root = new FolderTreeItem("Pictures", @"C:\Users\Michael\Pictures", isExpanded: true);
        root.Children.Add(new FolderTreeItem("Camera Roll", @"C:\Users\Michael\Pictures\Camera Roll"));
        root.Children.Add(new FolderTreeItem("Screenshots", @"C:\Users\Michael\Pictures\Screenshots"));
        FolderRoots.Add(root);

        List<ListItem> items =
        [
            new FolderListItem("folder:camera", @"C:\Users\Michael\Pictures\Camera Roll", "Camera Roll", 30),
            new FolderListItem("folder:screenshots", @"C:\Users\Michael\Pictures\Screenshots", "Screenshots", 20),
            new ImageListItem("image:mountain", @"C:\Users\Michael\Pictures\mountain.jpg", "mountain.jpg", "jpg", 10, 120_000),
            new ImageListItem("image:sample2", @"C:\Users\Michael\Pictures\sample2.png", "sample2.png", "png", 20, 98_000),
            new ImageListItem("image:sample10", @"C:\Users\Michael\Pictures\sample10.webp", "sample10.webp", "webp", 30, 140_000),
            new ImageListItem("image:animated", @"C:\Users\Michael\Pictures\loop.gif", "loop.gif", "gif", 40, 240_000, IsAnimated: true)
        ];

        var sorted = ListItemSorter.Sort(items, Sort, new SortOptions(KeepFoldersFirst: true));
        foreach (var item in sorted)
        {
            LibraryItems.Add(ToTile(item));
        }
    }

    private static LibraryTileItem ToTile(ListItem item) =>
        item switch
        {
            FolderListItem folder => new LibraryTileItem(
                Name: folder.Name,
                Path: folder.Path,
                Detail: "Open folder",
                IsFolder: true,
                IsSelected: false,
                IsAnimated: false,
                IconGlyph: "\uE8B7"),
            ImageListItem image => new LibraryTileItem(
                Name: image.Name,
                Path: image.Path,
                Detail: $"{image.Extension.ToUpperInvariant()} - {image.SizeBytes / 1024} KB",
                IsFolder: false,
                IsSelected: image.Name == "mountain.jpg",
                IsAnimated: image.IsAnimated,
                IconGlyph: image.IsAnimated ? "\uE783" : "\uEB9F"),
            _ => throw new ArgumentOutOfRangeException(nameof(item))
        };
}
