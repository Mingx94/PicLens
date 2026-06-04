using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageViewerWin.Application.Services;
using ImageViewerWin.Core.Domain;
using ImageViewerWin.Core.Models;
using System.Collections.ObjectModel;

namespace ImageViewerWin.ViewModels;

public sealed partial class MainPageViewModel : ObservableObject
{
    private readonly ISettingsStore settingsStore;
    private readonly IFavoriteFolderService favoriteFolderService;
    private readonly IFolderScanner folderScanner;
    private readonly IFileOperationService fileOperationService;
    private readonly Func<Task<string?>> chooseFolderAsync;
    private readonly Func<string, string, Task<bool>> confirmAsync;
    private readonly Func<ImageListItem, Task<string?>> requestRenameAsync;
    private readonly Action<ImageSequenceSnapshot> openImageViewer;
    private readonly List<string> folderHistory = [];
    private readonly List<string> selectedImagePaths = [];
    private readonly List<ImageListItem> dragSources = [];

    private AppSettings settings = AppSettings.CreateDefault();
    private IReadOnlyList<FavoriteFolder> favoriteFolders = [];
    private IReadOnlyList<ListItem> currentItems = [];
    private int folderHistoryIndex = -1;
    private bool suppressIncludeSubfoldersReload;

    public MainPageViewModel(
        ISettingsStore settingsStore,
        IFavoriteFolderService favoriteFolderService,
        IFolderScanner folderScanner,
        IFileOperationService fileOperationService,
        Func<Task<string?>> chooseFolderAsync,
        Func<string, string, Task<bool>> confirmAsync,
        Func<ImageListItem, Task<string?>> requestRenameAsync,
        Action<ImageSequenceSnapshot> openImageViewer)
    {
        this.settingsStore = settingsStore;
        this.favoriteFolderService = favoriteFolderService;
        this.folderScanner = folderScanner;
        this.fileOperationService = fileOperationService;
        this.chooseFolderAsync = chooseFolderAsync;
        this.confirmAsync = confirmAsync;
        this.requestRenameAsync = requestRenameAsync;
        this.openImageViewer = openImageViewer;
    }

    [ObservableProperty]
    public partial string CurrentFolderPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "Ready. Native ImageViewer shell initialized.";

    [ObservableProperty]
    public partial bool IncludeSubfolders { get; set; }

    [ObservableProperty]
    public partial SortState Sort { get; set; } = new(SortKey.Name, SortDirection.Asc);

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial FavoriteSidebarItem? SelectedFavorite { get; set; }

    public ObservableCollection<FavoriteSidebarItem> Favorites { get; } = [];

    public ObservableCollection<FolderTreeItem> FolderRoots { get; } = [];

    public ObservableCollection<LibraryTileItem> LibraryItems { get; } = [];

    public string RecursiveModeLabel => IncludeSubfolders ? "Recursive" : "Direct";

    public string SortLabel => Sort.Key == SortKey.Name
        ? $"Name {Sort.Direction}"
        : $"Modified {Sort.Direction}";

    public bool HasSingleSelectedImage => SelectedImages().Count == 1;

    public async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            settings = await settingsStore.LoadAsync();
            suppressIncludeSubfoldersReload = true;
            Sort = settings.Sort;
            IncludeSubfolders = settings.IncludeSubfolders;
            suppressIncludeSubfoldersReload = false;

            await LoadFavoritesAsync();
            var initialFolder = StartupFolderSelector.SelectInitialFolder(settings.LastFolderPath, favoriteFolders)
                ?? FirstAvailableFavoritePath()
                ?? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

            if (string.IsNullOrWhiteSpace(initialFolder))
            {
                initialFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            await NavigateToFolderAsync(initialFolder, replaceHistory: true, persist: false);
            StatusMessage = $"Loaded {LibraryItems.Count} items from {CurrentFolderPath}.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task NavigateToFolderAsync(string folderPath, bool replaceHistory = false, bool persist = true)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        var normalized = Path.GetFullPath(folderPath);
        if (!Directory.Exists(normalized))
        {
            StatusMessage = $"Folder is unavailable: {normalized}";
            return;
        }

        ClearSelection();
        CurrentFolderPath = normalized;

        if (replaceHistory)
        {
            folderHistory.Clear();
            folderHistory.Add(normalized);
            folderHistoryIndex = 0;
        }
        else if (folderHistoryIndex < 0 || !PathEquals(folderHistory.ElementAtOrDefault(folderHistoryIndex), normalized))
        {
            if (folderHistoryIndex >= 0 && folderHistoryIndex < folderHistory.Count - 1)
            {
                folderHistory.RemoveRange(folderHistoryIndex + 1, folderHistory.Count - folderHistoryIndex - 1);
            }

            folderHistory.Add(normalized);
            folderHistoryIndex = folderHistory.Count - 1;
        }

        NotifyNavigationCommands();
        if (persist)
        {
            settings = await settingsStore.UpdateAsync(new AppSettingsPatch
            {
                LastFolderPath = normalized,
                HasLastFolderPath = true
            });
        }

        await LoadLibraryAsync();
    }

    public void UpdateSelectedLibraryItems(IEnumerable<LibraryTileItem> selectedItems)
    {
        selectedImagePaths.Clear();
        selectedImagePaths.AddRange(
            selectedItems
                .Where(item => !item.IsFolder && item.SourceItem is ImageListItem)
                .Select(item => item.Path));
        NotifySelectionCommands();
    }

    public void BeginImageDrag(IEnumerable<LibraryTileItem> selectedItems)
    {
        dragSources.Clear();
        dragSources.AddRange(selectedItems.Select(item => item.SourceItem).OfType<ImageListItem>());
    }

    public async Task DropDraggedImagesOnAsync(LibraryTileItem target)
    {
        if (target.SourceItem is not ImageListItem targetImage || dragSources.Count == 0)
        {
            return;
        }

        var result = await fileOperationService.RenameByDropTargetAsync(dragSources.Select(image => image.Path), targetImage.Path);
        dragSources.Clear();
        ClearSelection();
        StatusMessage = DescribeBatchResult("Drop rename", result);
        await LoadLibraryAsync();
    }

    public async Task OpenLibraryItemAsync(LibraryTileItem item)
    {
        switch (item.SourceItem)
        {
            case FolderListItem folder:
                await NavigateToFolderAsync(folder.Path);
                break;
            case ImageListItem image:
                OpenImage(image);
                break;
        }
    }

    partial void OnIncludeSubfoldersChanged(bool value)
    {
        OnPropertyChanged(nameof(RecursiveModeLabel));
        if (!suppressIncludeSubfoldersReload)
        {
            _ = PersistIncludeSubfoldersAndReloadAsync(value);
        }
    }

    partial void OnSortChanged(SortState value)
    {
        OnPropertyChanged(nameof(SortLabel));
    }

    partial void OnSelectedFavoriteChanged(FavoriteSidebarItem? value)
    {
        RemoveFavoriteCommand.NotifyCanExecuteChanged();
        MoveFavoriteUpCommand.NotifyCanExecuteChanged();
        MoveFavoriteDownCommand.NotifyCanExecuteChanged();

        if (value is not null)
        {
            _ = NavigateToFolderAsync(value.Path);
        }
    }

    [RelayCommand(CanExecute = nameof(CanNavigateBack))]
    private async Task Back()
    {
        var nextIndex = folderHistoryIndex - 1;
        if (nextIndex < 0)
        {
            return;
        }

        folderHistoryIndex = nextIndex;
        NotifyNavigationCommands();
        await NavigateToFolderFromHistoryAsync(folderHistory[folderHistoryIndex]);
    }

    [RelayCommand(CanExecute = nameof(CanNavigateForward))]
    private async Task Forward()
    {
        var nextIndex = folderHistoryIndex + 1;
        if (nextIndex >= folderHistory.Count)
        {
            return;
        }

        folderHistoryIndex = nextIndex;
        NotifyNavigationCommands();
        await NavigateToFolderFromHistoryAsync(folderHistory[folderHistoryIndex]);
    }

    [RelayCommand]
    private async Task AddFavorite()
    {
        var folderPath = await chooseFolderAsync();
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        var normalized = Path.GetFullPath(folderPath);
        var currentUserFavorites = favoriteFolders
            .Where(folder => folder.Source == FavoriteSource.User && !PathEquals(folder.Path, normalized))
            .ToList();
        currentUserFavorites.Add(new FavoriteFolder(
            Id: CreateFavoriteId(normalized),
            Path: normalized,
            Source: FavoriteSource.User,
            Order: currentUserFavorites.Count,
            Name: FolderDisplayName(normalized),
            IsAvailable: Directory.Exists(normalized)));

        await favoriteFolderService.SaveUserFavoritesAsync(currentUserFavorites);
        await LoadFavoritesAsync();
        SelectedFavorite = Favorites.FirstOrDefault(favorite => PathEquals(favorite.Path, normalized));
        StatusMessage = $"Added favorite: {FolderDisplayName(normalized)}.";
    }

    [RelayCommand(CanExecute = nameof(CanManageSelectedFavorite))]
    private async Task RemoveFavorite()
    {
        if (SelectedFavorite is null)
        {
            return;
        }

        var next = favoriteFolders
            .Where(folder => folder.Source == FavoriteSource.User && folder.Id != SelectedFavorite.Id)
            .ToList();
        await favoriteFolderService.SaveUserFavoritesAsync(next);
        await LoadFavoritesAsync();
        StatusMessage = "Favorite removed.";
    }

    [RelayCommand(CanExecute = nameof(CanMoveFavoriteUp))]
    private async Task MoveFavoriteUp() => await MoveSelectedFavoriteAsync(-1);

    [RelayCommand(CanExecute = nameof(CanMoveFavoriteDown))]
    private async Task MoveFavoriteDown() => await MoveSelectedFavoriteAsync(1);

    [RelayCommand]
    private async Task RefreshLibrary()
    {
        await LoadLibraryAsync();
        StatusMessage = $"Library refreshed for {CurrentFolderPath}.";
    }

    [RelayCommand]
    private async Task ToggleSortKey()
    {
        Sort = Sort with
        {
            Key = Sort.Key == SortKey.Name ? SortKey.ModifiedAt : SortKey.Name
        };
        settings = await settingsStore.UpdateAsync(new AppSettingsPatch { Sort = Sort });
        await LoadLibraryAsync();
        StatusMessage = $"Sort changed to {SortLabel}.";
    }

    [RelayCommand]
    private async Task ToggleSortDirection()
    {
        Sort = Sort with
        {
            Direction = Sort.Direction == SortDirection.Asc ? SortDirection.Desc : SortDirection.Asc
        };
        settings = await settingsStore.UpdateAsync(new AppSettingsPatch { Sort = Sort });
        await LoadLibraryAsync();
        StatusMessage = $"Sort changed to {SortLabel}.";
    }

    [RelayCommand]
    private async Task ConvertVisible()
    {
        var result = await fileOperationService.ConvertVisibleToJpgAsync(VisibleImages());
        StatusMessage = DescribeBatchResult("Convert to JPG", result);
        await LoadLibraryAsync();
    }

    [RelayCommand]
    private async Task ClearSameBasename()
    {
        if (!await confirmAsync("Move same-name non-JPG files to trash?", "Clear same-name files"))
        {
            return;
        }

        var result = await fileOperationService.TrashSameBasenameNonJpgAsync(VisibleImages());
        StatusMessage = DescribeBatchResult("Clear same-name files", result);
        await LoadLibraryAsync();
    }

    [RelayCommand(CanExecute = nameof(HasSingleSelectedImage))]
    private async Task RenameSelected()
    {
        var selected = SelectedImages().SingleOrDefault();
        if (selected is null)
        {
            return;
        }

        var nextName = await requestRenameAsync(selected);
        if (string.IsNullOrWhiteSpace(nextName))
        {
            return;
        }

        var result = await fileOperationService.RenameAsync(selected.Path, nextName);
        StatusMessage = result.Status == FileOperationStatus.Renamed
            ? $"Renamed to {Path.GetFileName(result.TargetPath)}."
            : result.Message ?? result.Reason ?? "Rename skipped.";
        ClearSelection();
        await LoadLibraryAsync();
    }

    [RelayCommand(CanExecute = nameof(HasSingleSelectedImage))]
    private async Task TrashSelected()
    {
        var selected = SelectedImages().SingleOrDefault();
        if (selected is null || !await confirmAsync($"Move \"{selected.Name}\" to trash?", "Move selected image to trash"))
        {
            return;
        }

        var result = await fileOperationService.TrashAsync(selected.Path);
        StatusMessage = result.Status == FileOperationStatus.Trashed
            ? "Moved to trash."
            : result.Message ?? result.Reason ?? "Trash operation failed.";
        ClearSelection();
        await LoadLibraryAsync();
    }

    private async Task PersistIncludeSubfoldersAndReloadAsync(bool includeSubfolders)
    {
        settings = await settingsStore.UpdateAsync(new AppSettingsPatch { IncludeSubfolders = includeSubfolders });
        ClearSelection();
        await LoadLibraryAsync();
        StatusMessage = includeSubfolders
            ? "Recursive mode lists supported images under all child folders."
            : "Direct mode lists child folders and supported images in the selected folder.";
    }

    private async Task LoadFavoritesAsync()
    {
        favoriteFolders = await favoriteFolderService.GetFavoriteFoldersAsync();
        Favorites.Clear();
        foreach (var favorite in favoriteFolders)
        {
            Favorites.Add(ToFavoriteSidebarItem(favorite));
        }
    }

    private async Task LoadLibraryAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentFolderPath) || !Directory.Exists(CurrentFolderPath))
        {
            LibraryItems.Clear();
            FolderRoots.Clear();
            currentItems = [];
            return;
        }

        IsBusy = true;
        try
        {
            currentItems = await folderScanner.ScanAsync(new ListQuery(CurrentFolderPath, IncludeSubfolders, Sort));
            LibraryItems.Clear();
            foreach (var item in currentItems)
            {
                LibraryItems.Add(ToTile(item));
            }

            await LoadFolderTreeAsync();
            NotifySelectionCommands();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            currentItems = [];
            LibraryItems.Clear();
            StatusMessage = $"Unable to load folder: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadFolderTreeAsync()
    {
        FolderRoots.Clear();
        var rootPath = BestMatchingFavoriteRoot(CurrentFolderPath) ?? CurrentFolderPath;
        var root = new FolderTreeItem(FolderDisplayName(rootPath), rootPath, Directory.Exists(rootPath), true);
        await PopulateFolderChildrenAsync(root, rootPath);
        FolderRoots.Add(root);
    }

    private async Task PopulateFolderChildrenAsync(FolderTreeItem node, string folderPath)
    {
        IReadOnlyList<ListItem> items;
        try
        {
            items = await folderScanner.ScanAsync(new ListQuery(
                folderPath,
                IncludeSubfolders: false,
                Sort: new SortState(SortKey.Name, SortDirection.Asc)));
        }
        catch
        {
            return;
        }

        foreach (var folder in items.OfType<FolderListItem>())
        {
            var isExpanded = IsPathAncestorOrEqual(folder.Path, CurrentFolderPath);
            var child = new FolderTreeItem(folder.Name, folder.Path, isExpanded: isExpanded);
            node.Children.Add(child);
            if (isExpanded && !PathEquals(folder.Path, folderPath))
            {
                await PopulateFolderChildrenAsync(child, folder.Path);
            }
        }
    }

    private async Task NavigateToFolderFromHistoryAsync(string folderPath)
    {
        ClearSelection();
        CurrentFolderPath = folderPath;
        settings = await settingsStore.UpdateAsync(new AppSettingsPatch
        {
            LastFolderPath = folderPath,
            HasLastFolderPath = true
        });
        await LoadLibraryAsync();
    }

    private void OpenImage(ImageListItem image)
    {
        var images = VisibleImages();
        if (!images.Any(candidate => PathEquals(candidate.Path, image.Path)))
        {
            StatusMessage = "Image is no longer visible in the current library.";
            return;
        }

        var snapshot = ImageSequenceFactory.Create(new CreateImageSequenceSnapshotInput(
            SourceFolderPath: CurrentFolderPath,
            IncludeSubfolders: IncludeSubfolders,
            Sort: Sort,
            Images: images,
            CurrentImagePath: image.Path));
        openImageViewer(snapshot);
    }

    private async Task MoveSelectedFavoriteAsync(int direction)
    {
        if (SelectedFavorite is null)
        {
            return;
        }

        var userFavorites = favoriteFolders.Where(folder => folder.Source == FavoriteSource.User).ToList();
        var index = userFavorites.FindIndex(folder => PathEquals(folder.Path, SelectedFavorite.Path));
        var target = index + direction;
        if (index < 0 || target < 0 || target >= userFavorites.Count)
        {
            return;
        }

        (userFavorites[index], userFavorites[target]) = (userFavorites[target], userFavorites[index]);
        await favoriteFolderService.SaveUserFavoritesAsync(userFavorites);
        await LoadFavoritesAsync();
        SelectedFavorite = Favorites.FirstOrDefault(favorite => PathEquals(favorite.Path, userFavorites[target].Path));
    }

    private void ClearSelection()
    {
        selectedImagePaths.Clear();
        NotifySelectionCommands();
    }

    private void NotifySelectionCommands()
    {
        OnPropertyChanged(nameof(HasSingleSelectedImage));
        RenameSelectedCommand.NotifyCanExecuteChanged();
        TrashSelectedCommand.NotifyCanExecuteChanged();
    }

    private void NotifyNavigationCommands()
    {
        BackCommand.NotifyCanExecuteChanged();
        ForwardCommand.NotifyCanExecuteChanged();
    }

    private bool CanNavigateBack() => folderHistoryIndex > 0;

    private bool CanNavigateForward() => folderHistoryIndex >= 0 && folderHistoryIndex < folderHistory.Count - 1;

    private bool CanManageSelectedFavorite() => SelectedFavorite?.Source == FavoriteSource.User;

    private bool CanMoveFavoriteUp() => CanMoveFavorite(-1);

    private bool CanMoveFavoriteDown() => CanMoveFavorite(1);

    private bool CanMoveFavorite(int direction)
    {
        if (SelectedFavorite?.Source != FavoriteSource.User)
        {
            return false;
        }

        var userFavorites = favoriteFolders.Where(folder => folder.Source == FavoriteSource.User).ToList();
        var index = userFavorites.FindIndex(folder => PathEquals(folder.Path, SelectedFavorite.Path));
        var target = index + direction;
        return index >= 0 && target >= 0 && target < userFavorites.Count;
    }

    private List<ImageListItem> VisibleImages() => currentItems.OfType<ImageListItem>().ToList();

    private List<ImageListItem> SelectedImages() =>
        selectedImagePaths
            .Select(path => VisibleImages().FirstOrDefault(image => PathEquals(image.Path, path)))
            .OfType<ImageListItem>()
            .ToList();

    private string? FirstAvailableFavoritePath() =>
        favoriteFolders.FirstOrDefault(favorite => favorite.IsAvailable != false)?.Path;

    private string? BestMatchingFavoriteRoot(string folderPath) =>
        favoriteFolders
            .Where(favorite => favorite.IsAvailable != false && IsPathAncestorOrEqual(favorite.Path, folderPath))
            .OrderByDescending(favorite => favorite.Path.Length)
            .Select(favorite => favorite.Path)
            .FirstOrDefault();

    private static FavoriteSidebarItem ToFavoriteSidebarItem(FavoriteFolder favorite) =>
        new(
            Name: favorite.Name ?? FolderDisplayName(favorite.Path),
            Id: favorite.Id,
            Path: favorite.Path,
            Source: favorite.Source,
            IsAvailable: favorite.IsAvailable != false);

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
                IconGlyph: "\uE8B7",
                SourceItem: folder),
            ImageListItem image => new LibraryTileItem(
                Name: image.Name,
                Path: image.Path,
                Detail: $"{image.Extension.ToUpperInvariant()} - {image.SizeBytes / 1024} KB",
                IsFolder: false,
                IsSelected: false,
                IsAnimated: image.IsAnimated,
                IconGlyph: image.IsAnimated ? "\uE783" : "\uEB9F",
                SourceItem: image,
                ThumbnailPath: image.IsAnimated ? null : image.Path),
            _ => throw new ArgumentOutOfRangeException(nameof(item))
        };

    private static string FolderDisplayName(string path)
    {
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(name) ? path : name;
    }

    private static string CreateFavoriteId(string path) =>
        $"user:{Uri.EscapeDataString(Path.GetFullPath(path)).Replace("%", "_", StringComparison.Ordinal)}";

    private static string DescribeBatchResult(string label, FileOperationBatchResult result) =>
        $"{label}: {result.Succeeded} succeeded, {result.Skipped} skipped, {result.Failed} failed.";

    private static bool PathEquals(string? left, string? right)
    {
        if (left is null || right is null)
        {
            return false;
        }

        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static bool IsPathAncestorOrEqual(string ancestorPath, string childPath)
    {
        var ancestor = Path.GetFullPath(ancestorPath);
        var child = Path.GetFullPath(childPath);
        if (PathEquals(ancestor, child))
        {
            return true;
        }

        var relative = Path.GetRelativePath(ancestor, child);
        return relative != "."
            && !relative.StartsWith("..", StringComparison.Ordinal)
            && !Path.IsPathRooted(relative);
    }
}
