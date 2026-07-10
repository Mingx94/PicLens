using CommunityToolkit.Mvvm.Input;
using PicLens.Core.Domain;
using PicLens.Core.Models;
using static PicLens.Core.Domain.PathRules;

namespace PicLens.ViewModels;

public sealed partial class MainPageViewModel
{
    partial void OnIncludeSubfoldersChanged(bool value)
    {
        OnPropertyChanged(nameof(RecursiveModeLabel));
        if (!suppressIncludeSubfoldersReload)
        {
            _ = PersistIncludeSubfoldersAndReloadAsync(value);
        }
    }

    partial void OnCurrentFolderPathChanged(string value)
    {
        OnPropertyChanged(nameof(HasCurrentFolder));
        OnPropertyChanged(nameof(HasNoCurrentFolder));
        OnPropertyChanged(nameof(HasEmptyFolder));
        OnPropertyChanged(nameof(HasNoSearchResults));
        OnPropertyChanged(nameof(CurrentFolderName));
        OnPropertyChanged(nameof(CurrentParentFolderName));
    }

    partial void OnSortChanged(SortState value)
    {
        OnPropertyChanged(nameof(SortLabel));
    }

    partial void OnSearchQueryChanged(string value)
    {
        OnPropertyChanged(nameof(HasSearchQuery));
        OnPropertyChanged(nameof(HasEmptyFolder));
        OnPropertyChanged(nameof(HasNoSearchResults));
        ClearSelection();
        ApplyLibraryFilter();
    }

    partial void OnThumbnailSizeChanged(int value)
    {
        OnPropertyChanged(nameof(LibraryLayoutMinItemWidth));
    }

    partial void OnIsGridViewModeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsListViewMode));
        OnPropertyChanged(nameof(LibraryLayoutMinItemWidth));
        OnPropertyChanged(nameof(LibraryTileLayoutHeight));
    }

    [RelayCommand(CanExecute = nameof(CanNavigateBack))]
    private async Task Back()
    {
        var entry = folderHistory.Back();
        if (entry is null)
        {
            return;
        }

        NotifyNavigationCommands();
        await NavigateToFolderFromHistoryAsync(entry);
    }

    [RelayCommand(CanExecute = nameof(CanNavigateForward))]
    private async Task Forward()
    {
        var entry = folderHistory.Forward();
        if (entry is null)
        {
            return;
        }

        NotifyNavigationCommands();
        await NavigateToFolderFromHistoryAsync(entry);
    }

    [RelayCommand]
    private async Task OpenFolder()
    {
        var folderPath = await dialogService.ChooseFolderAsync();
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        await NavigateToFolderAsync(folderPath, persist: true, resetFolderTreeRoot: true);
        if (HasCurrentFolder)
        {
            SetStatus($"已載入 {LibraryItems.Count} 個項目。");
        }
    }

    [RelayCommand]
    private async Task RefreshLibrary()
    {
        await LoadLibraryAsync();
        SetStatus($"已重新整理 {CurrentFolderPath} 的圖庫。");
    }

    public async Task ChangeSortAsync(SortState sort)
    {
        if (Sort == sort)
        {
            return;
        }

        Sort = sort;
        settings = await settingsStore.UpdateAsync(new AppSettingsPatch { Sort = Sort });
        ApplyCurrentSort();
        SetStatus($"排序已變更為 {SortLabel}。");
    }

    [RelayCommand]
    private async Task ChangeSortOption(string? token)
    {
        try
        {
            var sort = token switch
            {
                "name-asc" => new SortState(SortKey.Name, SortDirection.Asc),
                "name-desc" => new SortState(SortKey.Name, SortDirection.Desc),
                "modified-asc" => new SortState(SortKey.ModifiedAt, SortDirection.Asc),
                "modified-desc" => new SortState(SortKey.ModifiedAt, SortDirection.Desc),
                _ => throw new ArgumentOutOfRangeException(nameof(token), token, "Unsupported sort option.")
            };

            await ChangeSortAsync(sort);
        }
        catch (Exception ex)
        {
            appLogger.Error(ex, $"Change sort option failed. Token={token ?? "<null>"}");
            SetStatus("排序時發生錯誤，已寫入診斷記錄。");
        }
    }

    [RelayCommand]
    private void ToggleIncludeSubfolders() => IncludeSubfolders = !IncludeSubfolders;

    [RelayCommand]
    private void ClearSearch() => SearchQuery = string.Empty;

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarOpen = !IsSidebarOpen;

    [RelayCommand]
    private void SetViewMode(string? mode)
    {
        IsGridViewMode = !string.Equals(mode, "list", StringComparison.OrdinalIgnoreCase);
    }

    private async Task PersistIncludeSubfoldersAndReloadAsync(bool includeSubfolders)
    {
        try
        {
            appLogger.Info(
                $"Toggle include subfolders started. IncludeSubfolders={includeSubfolders}; CurrentFolderPath={CurrentFolderPath}");

            settings = await settingsStore.UpdateAsync(new AppSettingsPatch { IncludeSubfolders = includeSubfolders });
            ClearSelection();
            await LoadLibraryAsync();
            SetStatus(includeSubfolders
                ? "含子資料夾模式會列出所有子資料夾中的支援圖片。"
                : "僅目前資料夾模式會列出所選資料夾中的子資料夾與支援圖片。");

            appLogger.Info(
                $"Toggle include subfolders completed. IncludeSubfolders={includeSubfolders}; CurrentFolderPath={CurrentFolderPath}; ItemCount={LibraryItems.Count}");
        }
        catch (Exception ex)
        {
            appLogger.Error(ex, "Toggle include subfolders failed.");
            SetStatus("切換子資料夾模式時發生錯誤，已寫入診斷記錄。");
        }
    }

    private async Task LoadLibraryAsync()
    {
        ClearSelection();

        if (string.IsNullOrWhiteSpace(CurrentFolderPath) || !Directory.Exists(CurrentFolderPath))
        {
            CancelActiveLibraryLoad();
            LibraryItems.Clear();
            NotifyLibraryItemCount();
            folderTree.Clear();
            currentItems = [];
            return;
        }

        var folderPath = CurrentFolderPath;
        var includeSubfolders = IncludeSubfolders;
        var sort = Sort;
        var folderTreeRoot = folderTree.EnsureRoot(folderPath);
        var load = BeginLibraryLoad();
        LibraryErrorMessage = null;
        IsLibraryLoading = true;
        try
        {
            var isRootChanged = folderTree.IsDisplayedRootChanged(folderTreeRoot);
            if (isRootChanged)
            {
                folderTree.ShowPendingRoot(folderTreeRoot, folderPath);
            }

            var loadedItems = await folderScanner.ScanAsync(
                new ListQuery(folderPath, includeSubfolders, sort),
                load.CancellationSource.Token);

            FolderTreeItem? loadedFolderRoot = null;
            if (isRootChanged)
            {
                loadedFolderRoot = await folderTree.BuildRootAsync(
                    folderTreeRoot,
                    folderPath,
                    load.CancellationSource.Token);
            }

            if (!IsCurrentLibraryLoad(load)
                || !PathEquals(CurrentFolderPath, folderPath)
                || IncludeSubfolders != includeSubfolders
                || Sort != sort)
            {
                return;
            }

            currentItems = loadedItems;
            RefreshLibraryItems();

            if (isRootChanged && loadedFolderRoot != null)
            {
                folderTree.ReplaceRoot(loadedFolderRoot);
            }
            else if (!isRootChanged)
            {
                folderTree.SelectPath(folderPath);
            }

            NotifySelectionCommands();
        }
        catch (OperationCanceledException) when (load.CancellationSource.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            if (!IsCurrentLibraryLoad(load))
            {
                return;
            }

            appLogger.Error(ex, $"Load library failed. CurrentFolderPath={folderPath}; IncludeSubfolders={includeSubfolders}");
            currentItems = [];
            LibraryItems.Clear();
            NotifyLibraryItemCount();
            LibraryErrorMessage = $"無法載入資料夾：{ex.Message}";
            SetStatus(LibraryErrorMessage);
        }
        finally
        {
            if (ReferenceEquals(libraryLoadCancellationSource, load.CancellationSource))
            {
                libraryLoadCancellationSource = null;
                IsLibraryLoading = false;
            }

            load.CancellationSource.Dispose();
        }
    }

    private void ApplyCurrentSort()
    {
        currentItems = ListItemSorter.Sort(
            currentItems,
            Sort,
            keepFoldersFirst: !IncludeSubfolders);
        RefreshLibraryItems();
        NotifySelectionCommands();
    }

    private LibraryLoadState BeginLibraryLoad()
    {
        var previous = libraryLoadCancellationSource;
        var next = new CancellationTokenSource();
        libraryLoadCancellationSource = next;
        previous?.Cancel();
        return new LibraryLoadState(next);
    }

    private void CancelActiveLibraryLoad()
    {
        libraryLoadCancellationSource?.Cancel();
        libraryLoadCancellationSource = null;
        IsLibraryLoading = false;
    }

    private bool IsCurrentLibraryLoad(LibraryLoadState load) =>
        ReferenceEquals(libraryLoadCancellationSource, load.CancellationSource)
        && !load.CancellationSource.IsCancellationRequested;

    private void RefreshLibraryItems()
    {
        CancelAllThumbnailLoads();
        ApplyLibraryFilter();
    }

    private void ApplyLibraryFilter()
    {
        var query = SearchQuery?.Trim() ?? string.Empty;
        var filteredItems = string.IsNullOrWhiteSpace(query)
            ? currentItems
            : currentItems
                .Where(item =>
                    item.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                    || item.Path.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                .ToList();

        var tiles = filteredItems.Select(item =>
        {
            var tile = new LibraryTileItem(item);
            tile.ApplyThumbnailSize(ThumbnailSize);
            return tile;
        }).ToList();

        LibraryItems.ReplaceAll(tiles);
        FolderLibraryItems.ReplaceAll(tiles.Where(item => item.IsFolder));
        ImageLibraryItems.ReplaceAll(tiles.Where(item => !item.IsFolder));
        NotifyLibraryItemCount();
    }

    public Task LoadFolderChildrenOnDemandAsync(FolderTreeItem node) =>
        folderTree.LoadChildrenOnDemandAsync(node, CurrentFolderPath);

    private async Task NavigateToFolderFromHistoryAsync(FolderNavigationHistory.Entry entry)
    {
        ClearSelection();
        folderTree.UseRoot(entry.FolderTreeRootPath);
        CurrentFolderPath = entry.FolderPath;
        await LoadLibraryAsync();
    }

    public void OpenImage(ImageListItem image)
    {
        var images = VisibleImages();
        if (!images.Any(candidate => PathEquals(candidate.Path, image.Path)))
        {
            SetStatus("圖片已不在目前圖庫中。");
            return;
        }

        var snapshot = CreateImageSequenceSnapshot(images, image.Path);

        appLogger.Info(
            $"Open image viewer requested. Image={image.Name}; CurrentIndex={snapshot.CurrentIndex}; ImageCount={snapshot.Images.Count}; CurrentFolderPath={CurrentFolderPath}; IncludeSubfolders={IncludeSubfolders}; Sort={Sort.Key}/{Sort.Direction}");

        try
        {
            openImageViewer(snapshot);
            appLogger.Info(
                $"Open image viewer completed. Image={image.Name}; CurrentIndex={snapshot.CurrentIndex}; ImageCount={snapshot.Images.Count}");
        }
        catch (Exception ex)
        {
            appLogger.Error(ex, "Open image viewer failed.");
            SetStatus("開啟圖片時發生錯誤，已寫入診斷記錄。");
        }
    }

    private ImageSequenceSnapshot CreateImageSequenceSnapshot(
        IReadOnlyList<ImageListItem> images,
        string currentImagePath)
    {
        var currentIndex = -1;
        for (var index = 0; index < images.Count; index += 1)
        {
            if (images[index].Path == currentImagePath)
            {
                currentIndex = index;
                break;
            }
        }

        if (currentIndex < 0)
        {
            throw new InvalidOperationException("Current image must exist in the image sequence.");
        }

        return new ImageSequenceSnapshot(
            SourceFolderPath: CurrentFolderPath,
            IncludeSubfolders: IncludeSubfolders,
            Sort: Sort,
            Images: images.ToList(),
            CurrentIndex: currentIndex);
    }

    private void ClearSelection()
    {
        foreach (var item in LibraryItems)
        {
            item.IsSelected = false;
        }

        selectedImagePaths.Clear();
        NotifySelectionCommands();
    }

    private void NotifySelectionCommands()
    {
        OnPropertyChanged(nameof(SelectedImageCount));
        OnPropertyChanged(nameof(HasSelectedImages));
        OnPropertyChanged(nameof(HasSingleSelectedImage));
        OnPropertyChanged(nameof(SelectedSummaryText));
        RenameSelectedCommand.NotifyCanExecuteChanged();
        TrashSelectedCommand.NotifyCanExecuteChanged();
    }

    private void NotifyLibraryItemCount()
    {
        OnPropertyChanged(nameof(LibraryItemCountText));
        OnPropertyChanged(nameof(FolderItemCountText));
        OnPropertyChanged(nameof(ImageItemCountText));
        OnPropertyChanged(nameof(HasFolderLibraryItems));
        OnPropertyChanged(nameof(HasImageLibraryItems));
        OnPropertyChanged(nameof(HasEmptyFolder));
        OnPropertyChanged(nameof(HasNoSearchResults));
    }

    private void NotifyNavigationCommands()
    {
        BackCommand.NotifyCanExecuteChanged();
        ForwardCommand.NotifyCanExecuteChanged();
    }

    private bool CanNavigateBack() => folderHistory.CanBack;

    private bool CanNavigateForward() => folderHistory.CanForward;

    private List<ImageListItem> VisibleImages() =>
        LibraryItems.Select(item => item.SourceItem).OfType<ImageListItem>().ToList();

    private List<ImageListItem> SelectedImages()
    {
        var imagesByPath = VisibleImages().ToDictionary(image => PathKey(image.Path), PathComparer);
        return selectedImagePaths
            .Select(path => imagesByPath.GetValueOrDefault(PathKey(path)))
            .OfType<ImageListItem>()
            .ToList();
    }

    private static string SortOptionLabel(SortState sort) =>
        (sort.Key, sort.Direction) switch
        {
            (SortKey.Name, SortDirection.Asc) => "名稱由小到大",
            (SortKey.Name, SortDirection.Desc) => "名稱由大到小",
            (SortKey.ModifiedAt, SortDirection.Asc) => "修改時間最舊到最新",
            (SortKey.ModifiedAt, SortDirection.Desc) => "修改時間最新到最舊",
            _ => "名稱由小到大"
        };

    private sealed record LibraryLoadState(CancellationTokenSource CancellationSource);
}
