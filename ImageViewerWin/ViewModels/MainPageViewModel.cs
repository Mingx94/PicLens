using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageViewerWin.Application.Services;
using ImageViewerWin.Core.Domain;
using ImageViewerWin.Core.Models;
using System.Collections.ObjectModel;

namespace ImageViewerWin.ViewModels;

public sealed partial class MainPageViewModel : ObservableObject
{
    private const int MaxConcurrentThumbnailLoads = 4;
    private static readonly TimeSpan DefaultThumbnailLoadTimeout = TimeSpan.FromSeconds(8);

    private readonly ISettingsStore settingsStore;
    private readonly IFolderScanner folderScanner;
    private readonly IFileOperationService fileOperationService;
    private readonly IThumbnailService thumbnailService;
    private readonly Func<Task<string?>> chooseFolderAsync;
    private readonly Func<string, string, Task<bool>> confirmAsync;
    private readonly Func<ImageListItem, Task<string?>> requestRenameAsync;
    private readonly Action<ImageSequenceSnapshot> openImageViewer;
    private readonly TimeSpan thumbnailLoadTimeout;
    private readonly SemaphoreSlim thumbnailGate = new(MaxConcurrentThumbnailLoads);
    private readonly Dictionary<LibraryTileItem, ThumbnailLoadState> thumbnailLoads = new(ReferenceEqualityComparer.Instance);
    private readonly List<string> folderHistory = [];
    private readonly List<string> selectedImagePaths = [];
    private readonly List<ImageListItem> dragSources = [];

    private AppSettings settings = AppSettings.CreateDefault();
    private IReadOnlyList<ListItem> currentItems = [];
    private int folderHistoryIndex = -1;
    private bool suppressIncludeSubfoldersReload;

    public MainPageViewModel(
        ISettingsStore settingsStore,
        IFolderScanner folderScanner,
        IFileOperationService fileOperationService,
        IThumbnailService thumbnailService,
        Func<Task<string?>> chooseFolderAsync,
        Func<string, string, Task<bool>> confirmAsync,
        Func<ImageListItem, Task<string?>> requestRenameAsync,
        Action<ImageSequenceSnapshot> openImageViewer,
        TimeSpan? thumbnailLoadTimeout = null)
    {
        this.settingsStore = settingsStore;
        this.folderScanner = folderScanner;
        this.fileOperationService = fileOperationService;
        this.thumbnailService = thumbnailService;
        this.chooseFolderAsync = chooseFolderAsync;
        this.confirmAsync = confirmAsync;
        this.requestRenameAsync = requestRenameAsync;
        this.openImageViewer = openImageViewer;
        this.thumbnailLoadTimeout = thumbnailLoadTimeout is { } timeout && timeout > TimeSpan.Zero
            ? timeout
            : DefaultThumbnailLoadTimeout;
    }

    [ObservableProperty]
    public partial string CurrentFolderPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "就緒。原生 ImageViewer 已初始化。";

    [ObservableProperty]
    public partial bool IncludeSubfolders { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThumbnailSizeLabel))]
    public partial int ThumbnailSize { get; set; } = SettingsRules.DefaultThumbnailSize;

    [ObservableProperty]
    public partial SortState Sort { get; set; } = new(SortKey.Name, SortDirection.Asc);

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public ObservableCollection<FolderTreeItem> FolderRoots { get; } = [];

    public ObservableCollection<LibraryTileItem> LibraryItems { get; } = [];

    public bool HasCurrentFolder => !string.IsNullOrWhiteSpace(CurrentFolderPath);

    public string RecursiveModeLabel => IncludeSubfolders ? "含子資料夾" : "僅目前資料夾";

    public string SortLabel => $"{SortKeyLabel(Sort.Key)} {SortDirectionLabel(Sort.Direction)}";

    public double ThumbnailSizeMinimum => SettingsRules.MinThumbnailSize;

    public double ThumbnailSizeMaximum => SettingsRules.MaxThumbnailSize;

    public double ThumbnailSizeStep => SettingsRules.ThumbnailSizeStep;

    public string ThumbnailSizeLabel => $"縮圖 {ThumbnailSize}";

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
            ThumbnailSize = settings.ThumbnailSize;
            suppressIncludeSubfoldersReload = false;

            var initialFolder = StartupFolderSelector.SelectInitialFolder(settings.LastFolderPath, Directory.Exists);
            var shouldPersistInitialFolder = false;
            if (initialFolder is null)
            {
                initialFolder = await chooseFolderAsync();
                shouldPersistInitialFolder = true;
            }

            if (string.IsNullOrWhiteSpace(initialFolder))
            {
                CurrentFolderPath = string.Empty;
                currentItems = [];
                LibraryItems.Clear();
                FolderRoots.Clear();
                StatusMessage = "請選擇資料夾以開始瀏覽。";
                return;
            }

            await NavigateToFolderAsync(initialFolder, replaceHistory: true, persist: shouldPersistInitialFolder);
            if (HasCurrentFolder)
            {
                StatusMessage = $"已從 {CurrentFolderPath} 載入 {LibraryItems.Count} 個項目。";
            }
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
            StatusMessage = $"資料夾無法使用：{normalized}";
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
        StatusMessage = DescribeBatchResult("拖放重新命名", result);
        await LoadLibraryAsync();
    }

    public async Task ChangeThumbnailSizeAsync(double thumbnailSize)
    {
        var normalizedSize = SettingsRules.NormalizeThumbnailSize(thumbnailSize);
        if (ThumbnailSize == normalizedSize)
        {
            return;
        }

        ThumbnailSize = normalizedSize;
        CancelAllThumbnailLoads();
        ApplyThumbnailSizeToLibraryItems();
        settings = await settingsStore.UpdateAsync(new AppSettingsPatch { ThumbnailSize = normalizedSize });
        StatusMessage = $"縮圖大小已調整為 {normalizedSize}。";
    }

    public async Task LoadThumbnailAsync(LibraryTileItem tile)
    {
        if (tile.IsFolder || tile.IsAnimated || tile.SourceItem is not ImageListItem image)
        {
            return;
        }

        var requestedSize = ThumbnailSize;
        if (tile.HasThumbnailFor(requestedSize))
        {
            return;
        }

        if (thumbnailLoads.TryGetValue(tile, out var existingLoad)
            && existingLoad.RequestedSize == requestedSize
            && !existingLoad.CancellationSource.IsCancellationRequested)
        {
            return;
        }

        CancelThumbnailLoad(tile);

        var loadCts = new CancellationTokenSource();
        var loadState = new ThumbnailLoadState(loadCts, requestedSize);
        thumbnailLoads[tile] = loadState;

        try
        {
            await thumbnailGate.WaitAsync(loadCts.Token);
            try
            {
                using var timeoutCts = new CancellationTokenSource(thumbnailLoadTimeout);
                using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(loadCts.Token, timeoutCts.Token);
                var token = operationCts.Token;
                var thumbnailTask = thumbnailService.GetOrCreateThumbnailAsync(image.Path, requestedSize, token);
                var thumbnailPath = await WaitForThumbnailResultAsync(thumbnailTask, token);
                if (!loadCts.IsCancellationRequested
                    && ThumbnailSize == requestedSize
                    && LibraryItems.Contains(tile)
                    && PathEquals(tile.Path, image.Path))
                {
                    tile.ApplyThumbnailPath(thumbnailPath, requestedSize);
                }
            }
            finally
            {
                thumbnailGate.Release();
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (thumbnailLoads.TryGetValue(tile, out var activeLoad) && ReferenceEquals(activeLoad, loadState))
            {
                thumbnailLoads.Remove(tile);
            }

            loadCts.Dispose();
        }
    }

    public void CancelThumbnailLoad(LibraryTileItem tile)
    {
        if (thumbnailLoads.TryGetValue(tile, out var loadState))
        {
            loadState.CancellationSource.Cancel();
        }
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

    partial void OnCurrentFolderPathChanged(string value)
    {
        OnPropertyChanged(nameof(HasCurrentFolder));
    }

    partial void OnSortChanged(SortState value)
    {
        OnPropertyChanged(nameof(SortLabel));
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
    private async Task OpenFolder()
    {
        var folderPath = await chooseFolderAsync();
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        await NavigateToFolderAsync(folderPath);
        if (HasCurrentFolder)
        {
            StatusMessage = $"已從 {CurrentFolderPath} 載入 {LibraryItems.Count} 個項目。";
        }
    }

    [RelayCommand]
    private async Task RefreshLibrary()
    {
        await LoadLibraryAsync();
        StatusMessage = $"已重新整理 {CurrentFolderPath} 的圖庫。";
    }

    [RelayCommand]
    private async Task ToggleSortKey()
    {
        Sort = Sort with
        {
            Key = Sort.Key == SortKey.Name ? SortKey.ModifiedAt : SortKey.Name
        };
        settings = await settingsStore.UpdateAsync(new AppSettingsPatch { Sort = Sort });
        ApplyCurrentSort();
        StatusMessage = $"排序已變更為 {SortLabel}。";
    }

    [RelayCommand]
    private async Task ToggleSortDirection()
    {
        Sort = Sort with
        {
            Direction = Sort.Direction == SortDirection.Asc ? SortDirection.Desc : SortDirection.Asc
        };
        settings = await settingsStore.UpdateAsync(new AppSettingsPatch { Sort = Sort });
        ApplyCurrentSort();
        StatusMessage = $"排序已變更為 {SortLabel}。";
    }

    [RelayCommand]
    private async Task ConvertVisible()
    {
        var result = await fileOperationService.ConvertVisibleToJpgAsync(VisibleImages());
        StatusMessage = DescribeBatchResult("轉換為 JPG", result);
        await LoadLibraryAsync();
    }

    [RelayCommand]
    private async Task ClearSameBasename()
    {
        if (!await confirmAsync("要將同名的非 JPG 檔案移至回收筒嗎？", "清除同名檔案"))
        {
            return;
        }

        var result = await fileOperationService.TrashSameBasenameNonJpgAsync(VisibleImages());
        StatusMessage = DescribeBatchResult("清除同名檔案", result);
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
            ? $"已重新命名為 {Path.GetFileName(result.TargetPath)}。"
            : result.Message ?? result.Reason ?? "重新命名已略過。";
        ClearSelection();
        await LoadLibraryAsync();
    }

    [RelayCommand(CanExecute = nameof(HasSingleSelectedImage))]
    private async Task TrashSelected()
    {
        var selected = SelectedImages().SingleOrDefault();
        if (selected is null || !await confirmAsync($"要將「{selected.Name}」移至回收筒嗎？", "將選取的圖片移至回收筒"))
        {
            return;
        }

        var result = await fileOperationService.TrashAsync(selected.Path);
        StatusMessage = result.Status == FileOperationStatus.Trashed
            ? "已移至回收筒。"
            : result.Message ?? result.Reason ?? "移至回收筒失敗。";
        ClearSelection();
        await LoadLibraryAsync();
    }

    private async Task PersistIncludeSubfoldersAndReloadAsync(bool includeSubfolders)
    {
        settings = await settingsStore.UpdateAsync(new AppSettingsPatch { IncludeSubfolders = includeSubfolders });
        ClearSelection();
        await LoadLibraryAsync();
        StatusMessage = includeSubfolders
            ? "含子資料夾模式會列出所有子資料夾中的支援圖片。"
            : "僅目前資料夾模式會列出所選資料夾中的子資料夾與支援圖片。";
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
            RefreshLibraryItems();

            await LoadFolderTreeAsync();
            NotifySelectionCommands();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            currentItems = [];
            LibraryItems.Clear();
            StatusMessage = $"無法載入資料夾：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyCurrentSort()
    {
        currentItems = ListItemSorter.Sort(
            currentItems,
            Sort,
            new SortOptions(KeepFoldersFirst: !IncludeSubfolders));
        RefreshLibraryItems();
        NotifySelectionCommands();
    }

    private void RefreshLibraryItems()
    {
        CancelAllThumbnailLoads();
        LibraryItems.Clear();
        foreach (var item in currentItems)
        {
            var tile = ToTile(item);
            tile.ApplyThumbnailSize(ThumbnailSize);
            LibraryItems.Add(tile);
        }
    }

    private void ApplyThumbnailSizeToLibraryItems()
    {
        foreach (var item in LibraryItems)
        {
            item.ApplyThumbnailSize(ThumbnailSize);
        }
    }

    private async Task LoadFolderTreeAsync()
    {
        FolderRoots.Clear();
        var rootPath = CurrentFolderPath;
        var root = new FolderTreeItem(FolderDisplayName(rootPath), rootPath, Directory.Exists(rootPath), true);
        await PopulateFolderChildrenAsync(root, rootPath);
        FolderRoots.Add(root);
    }

    private async Task PopulateFolderChildrenAsync(FolderTreeItem node, string folderPath)
    {
        IReadOnlyList<FolderListItem> folders;
        try
        {
            folders = await folderScanner.ScanChildFoldersAsync(
                folderPath,
                new SortState(SortKey.Name, SortDirection.Asc));
        }
        catch
        {
            return;
        }

        foreach (var folder in folders)
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
            StatusMessage = "圖片已不在目前圖庫中。";
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

    private List<ImageListItem> VisibleImages() => currentItems.OfType<ImageListItem>().ToList();

    private List<ImageListItem> SelectedImages() =>
        selectedImagePaths
            .Select(path => VisibleImages().FirstOrDefault(image => PathEquals(image.Path, path)))
            .OfType<ImageListItem>()
            .ToList();

    private static LibraryTileItem ToTile(ListItem item) =>
        item switch
        {
            FolderListItem folder => new LibraryTileItem(
                Name: folder.Name,
                Path: folder.Path,
                Detail: "開啟資料夾",
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
                SourceItem: image),
            _ => throw new ArgumentOutOfRangeException(nameof(item))
        };

    private static string FolderDisplayName(string path)
    {
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(name) ? path : name;
    }

    private static string DescribeBatchResult(string label, FileOperationBatchResult result) =>
        $"{label}：成功 {result.Succeeded} 個，略過 {result.Skipped} 個，失敗 {result.Failed} 個。";

    private static string SortKeyLabel(SortKey key) =>
        key == SortKey.Name ? "名稱" : "修改時間";

    private static string SortDirectionLabel(SortDirection direction) =>
        direction == SortDirection.Asc ? "遞增" : "遞減";

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

    private void CancelAllThumbnailLoads()
    {
        foreach (var loadState in thumbnailLoads.Values)
        {
            loadState.CancellationSource.Cancel();
        }

        thumbnailLoads.Clear();
    }

    private static async Task<string?> WaitForThumbnailResultAsync(
        Task<string?> thumbnailTask,
        CancellationToken cancellationToken)
    {
        try
        {
            return await thumbnailTask.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (!thumbnailTask.IsCompleted)
        {
            ObserveFaultIfThumbnailTaskFailsLater(thumbnailTask);
            throw;
        }
    }

    private static void ObserveFaultIfThumbnailTaskFailsLater(Task thumbnailTask)
    {
        _ = thumbnailTask.ContinueWith(
            static completedTask => _ = completedTask.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private sealed record ThumbnailLoadState(CancellationTokenSource CancellationSource, int RequestedSize);

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
