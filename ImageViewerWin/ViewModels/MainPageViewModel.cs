using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageViewerWin.Application.Services;
using ImageViewerWin.Core.Domain;
using ImageViewerWin.Core.Models;
using ImageViewerWin.Diagnostics;
using Microsoft.UI.Xaml.Controls;
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
    private readonly IAppLogger appLogger;
    private readonly Func<Task<string?>> chooseFolderAsync;
    private readonly Func<string, string, string, Task<bool>> confirmAsync;
    private readonly Func<ImageListItem, Task<string?>> requestRenameAsync;
    private readonly Action<ImageSequenceSnapshot> openImageViewer;
    private readonly TimeSpan thumbnailLoadTimeout;
    private readonly SemaphoreSlim thumbnailGate = new(MaxConcurrentThumbnailLoads);
    private readonly Dictionary<LibraryTileItem, ThumbnailLoadState> thumbnailLoads = new(ReferenceEqualityComparer.Instance);
    private readonly List<FolderHistoryEntry> folderHistory = [];
    private readonly List<string> selectedImagePaths = [];
    private readonly List<ImageListItem> dragSources = [];

    private AppSettings settings = AppSettings.CreateDefault();
    private IReadOnlyList<ListItem> currentItems = [];
    private string folderTreeRootPath = string.Empty;
    private int folderHistoryIndex = -1;
    private bool suppressIncludeSubfoldersReload;

    public MainPageViewModel(
        ISettingsStore settingsStore,
        IFolderScanner folderScanner,
        IFileOperationService fileOperationService,
        IThumbnailService thumbnailService,
        Func<Task<string?>> chooseFolderAsync,
        Func<string, string, string, Task<bool>> confirmAsync,
        Func<ImageListItem, Task<string?>> requestRenameAsync,
        Action<ImageSequenceSnapshot> openImageViewer,
        TimeSpan? thumbnailLoadTimeout = null,
        IAppLogger? appLogger = null)
    {
        this.settingsStore = settingsStore;
        this.folderScanner = folderScanner;
        this.fileOperationService = fileOperationService;
        this.thumbnailService = thumbnailService;
        this.appLogger = appLogger ?? NullAppLogger.Instance;
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
    public partial InfoBarSeverity StatusSeverity { get; set; } = InfoBarSeverity.Informational;

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

    public string SortLabel => SortOptionLabel(Sort);

    public IReadOnlyList<SortOption> SortOptions { get; } =
    [
        new("名稱由小到大", new SortState(SortKey.Name, SortDirection.Asc)),
        new("名稱由大到小", new SortState(SortKey.Name, SortDirection.Desc)),
        new("修改時間最舊到最新", new SortState(SortKey.ModifiedAt, SortDirection.Asc)),
        new("修改時間最新到最舊", new SortState(SortKey.ModifiedAt, SortDirection.Desc))
    ];

    public SortOption SelectedSortOption =>
        SortOptions.FirstOrDefault(option => option.State == Sort) ?? SortOptions[0];

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
                SetStatus("請選擇資料夾以開始瀏覽。");
                return;
            }

            await NavigateToFolderAsync(
                initialFolder,
                replaceHistory: true,
                persist: shouldPersistInitialFolder,
                resetFolderTreeRoot: true);
            if (HasCurrentFolder)
            {
                SetStatus($"已從 {CurrentFolderPath} 載入 {LibraryItems.Count} 個項目。");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task NavigateToFolderAsync(
        string folderPath,
        bool replaceHistory = false,
        bool persist = false,
        bool resetFolderTreeRoot = false)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            appLogger.Info("Navigate to folder ignored. FolderPath is empty.");
            return;
        }

        var normalized = Path.GetFullPath(folderPath);
        if (!Directory.Exists(normalized))
        {
            appLogger.Info($"Navigate to folder ignored. FolderPath={normalized}; Reason=DirectoryNotFound");
            SetStatus($"資料夾無法使用：{normalized}", InfoBarSeverity.Warning);
            return;
        }

        appLogger.Info(
            $"Navigate to folder started. FolderPath={normalized}; ReplaceHistory={replaceHistory}; Persist={persist}; ResetFolderTreeRoot={resetFolderTreeRoot}; IncludeSubfolders={IncludeSubfolders}; Sort={Sort.Key}/{Sort.Direction}");

        ClearSelection();
        if (resetFolderTreeRoot || string.IsNullOrWhiteSpace(folderTreeRootPath))
        {
            folderTreeRootPath = normalized;
        }

        CurrentFolderPath = normalized;
        var historyEntry = new FolderHistoryEntry(normalized, folderTreeRootPath);

        if (replaceHistory)
        {
            folderHistory.Clear();
            folderHistory.Add(historyEntry);
            folderHistoryIndex = 0;
        }
        else if (folderHistoryIndex < 0 || !HistoryEntryEquals(folderHistory.ElementAtOrDefault(folderHistoryIndex), historyEntry))
        {
            if (folderHistoryIndex >= 0 && folderHistoryIndex < folderHistory.Count - 1)
            {
                folderHistory.RemoveRange(folderHistoryIndex + 1, folderHistory.Count - folderHistoryIndex - 1);
            }

            folderHistory.Add(historyEntry);
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

        appLogger.Info(
            $"Navigate to folder completed. FolderPath={CurrentFolderPath}; ItemCount={LibraryItems.Count}; IncludeSubfolders={IncludeSubfolders}; Sort={Sort.Key}/{Sort.Direction}");
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
        var selectedImages = selectedItems.Select(item => item.SourceItem).OfType<ImageListItem>().ToList();
        dragSources.Clear();
        dragSources.AddRange(selectedImages);
        appLogger.Info(
            $"Begin image drag. SourceCount={dragSources.Count}; First={dragSources.FirstOrDefault()?.Name ?? "<none>"}");
    }

    public async Task DropDraggedImagesOnAsync(LibraryTileItem target)
    {
        if (target.SourceItem is not ImageListItem targetImage || dragSources.Count == 0)
        {
            appLogger.Info(
                $"Drop dragged images ignored. HasTargetImage={target.SourceItem is ImageListItem}; SourceCount={dragSources.Count}");
            return;
        }

        appLogger.Info(
            $"Drop dragged images started. SourceCount={dragSources.Count}; Target={targetImage.Name}; TargetPath={targetImage.Path}");

        try
        {
            var result = await fileOperationService.RenameByDropTargetAsync(dragSources.Select(image => image.Path), targetImage.Path);
            ClearSelection();
            SetBatchStatus("拖放重新命名", result);
            appLogger.Info(
                $"Drop dragged images completed. Total={result.Total}; Succeeded={result.Succeeded}; Skipped={result.Skipped}; Failed={result.Failed}; Target={targetImage.Name}");
            await LoadLibraryAsync();
        }
        catch (Exception ex)
        {
            appLogger.Error(ex, "Drop dragged images failed.");
            SetStatus("拖放重新命名時發生錯誤，已寫入診斷記錄。", InfoBarSeverity.Error);
        }
        finally
        {
            dragSources.Clear();
        }
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
        SetStatus($"縮圖大小已調整為 {normalizedSize}。");
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
        catch (Exception ex)
        {
            appLogger.Error(ex, $"Load thumbnail failed. Image={image.Name}; Path={image.Path}; RequestedSize={requestedSize}");
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
        appLogger.Info(
            $"Open library item requested. Name={item.Name}; Path={item.Path}; IsFolder={item.IsFolder}; CurrentFolderPath={CurrentFolderPath}");

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
        OnPropertyChanged(nameof(SelectedSortOption));
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

        await NavigateToFolderAsync(folderPath, persist: true, resetFolderTreeRoot: true);
        if (HasCurrentFolder)
        {
            SetStatus($"已從 {CurrentFolderPath} 載入 {LibraryItems.Count} 個項目。");
        }
    }

    [RelayCommand]
    private async Task RefreshLibrary()
    {
        await LoadLibraryAsync();
        SetStatus($"已重新整理 {CurrentFolderPath} 的圖庫。");
    }

    [RelayCommand]
    private async Task ToggleSortKey()
    {
        await ChangeSortAsync(Sort with
        {
            Key = Sort.Key == SortKey.Name ? SortKey.ModifiedAt : SortKey.Name
        });
    }

    [RelayCommand]
    private async Task ToggleSortDirection()
    {
        await ChangeSortAsync(Sort with
        {
            Direction = Sort.Direction == SortDirection.Asc ? SortDirection.Desc : SortDirection.Asc
        });
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
    private async Task ConvertVisible()
    {
        var result = await fileOperationService.ConvertVisibleToJpgAsync(VisibleImages());
        SetBatchStatus("轉換為 JPG", result);
        await LoadLibraryAsync();
    }

    [RelayCommand]
    private async Task ClearSameBasename()
    {
        if (!await confirmAsync("要將同名的非 JPG 檔案移至回收筒嗎？", "清除同名檔案", "移至回收筒"))
        {
            return;
        }

        var result = await fileOperationService.TrashSameBasenameNonJpgAsync(VisibleImages());
        SetBatchStatus("清除同名檔案", result);
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
        SetStatus(
            result.Status == FileOperationStatus.Renamed
                ? $"已重新命名為 {Path.GetFileName(result.TargetPath)}。"
                : result.Message ?? result.Reason ?? "重新命名已略過。",
            result.Status == FileOperationStatus.Renamed ? InfoBarSeverity.Informational : InfoBarSeverity.Warning);
        ClearSelection();
        await LoadLibraryAsync();
    }

    [RelayCommand(CanExecute = nameof(HasSingleSelectedImage))]
    private async Task TrashSelected()
    {
        var selected = SelectedImages().SingleOrDefault();
        if (selected is null || !await confirmAsync($"要將「{selected.Name}」移至回收筒嗎？", "將選取的圖片移至回收筒", "移至回收筒"))
        {
            return;
        }

        var result = await fileOperationService.TrashAsync(selected.Path);
        SetStatus(
            result.Status == FileOperationStatus.Trashed
                ? "已移至回收筒。"
                : result.Message ?? result.Reason ?? "移至回收筒失敗。",
            result.Status == FileOperationStatus.Trashed ? InfoBarSeverity.Informational : InfoBarSeverity.Warning);
        ClearSelection();
        await LoadLibraryAsync();
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
            SetStatus("切換子資料夾模式時發生錯誤，已寫入診斷記錄。", InfoBarSeverity.Error);
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
            RefreshLibraryItems();

            await LoadFolderTreeAsync();
            NotifySelectionCommands();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            appLogger.Error(ex, $"Load library failed. CurrentFolderPath={CurrentFolderPath}; IncludeSubfolders={IncludeSubfolders}");
            currentItems = [];
            LibraryItems.Clear();
            SetStatus($"無法載入資料夾：{ex.Message}", InfoBarSeverity.Error);
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
        var rootPath = string.IsNullOrWhiteSpace(folderTreeRootPath)
            ? CurrentFolderPath
            : folderTreeRootPath;
        var root = new FolderTreeItem(
            FolderDisplayName(rootPath),
            rootPath,
            Directory.Exists(rootPath),
            true,
            PathEquals(rootPath, CurrentFolderPath));
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
        catch (Exception ex)
        {
            appLogger.Error(
                ex,
                $"Load folder tree children failed. FolderPath={folderPath}; FolderTreeRootPath={folderTreeRootPath}; CurrentFolderPath={CurrentFolderPath}");
            return;
        }

        foreach (var folder in folders)
        {
            var isExpanded = IsPathAncestorOrEqual(folder.Path, CurrentFolderPath);
            var child = new FolderTreeItem(
                folder.Name,
                folder.Path,
                isExpanded: isExpanded,
                isSelected: PathEquals(folder.Path, CurrentFolderPath));
            node.Children.Add(child);
            if (isExpanded && !PathEquals(folder.Path, folderPath))
            {
                await PopulateFolderChildrenAsync(child, folder.Path);
            }
        }
    }

    private async Task NavigateToFolderFromHistoryAsync(FolderHistoryEntry entry)
    {
        ClearSelection();
        folderTreeRootPath = entry.FolderTreeRootPath;
        CurrentFolderPath = entry.FolderPath;
        await LoadLibraryAsync();
    }

    private void OpenImage(ImageListItem image)
    {
        var images = VisibleImages();
        if (!images.Any(candidate => PathEquals(candidate.Path, image.Path)))
        {
            SetStatus("圖片已不在目前圖庫中。", InfoBarSeverity.Warning);
            return;
        }

        var snapshot = ImageSequenceFactory.Create(new CreateImageSequenceSnapshotInput(
            SourceFolderPath: CurrentFolderPath,
            IncludeSubfolders: IncludeSubfolders,
            Sort: Sort,
            Images: images,
            CurrentImagePath: image.Path));

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
            SetStatus("開啟圖片時發生錯誤，已寫入診斷記錄。", InfoBarSeverity.Error);
        }
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

    private void SetBatchStatus(string label, FileOperationBatchResult result) =>
        SetStatus(
            DescribeBatchResult(label, result),
            result.Failed > 0 ? InfoBarSeverity.Warning : InfoBarSeverity.Informational);

    private void SetStatus(string message, InfoBarSeverity severity = InfoBarSeverity.Informational)
    {
        StatusMessage = message;
        StatusSeverity = severity;
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

    private sealed record FolderHistoryEntry(string FolderPath, string FolderTreeRootPath);

    private sealed record ThumbnailLoadState(CancellationTokenSource CancellationSource, int RequestedSize);

    private static bool HistoryEntryEquals(FolderHistoryEntry? left, FolderHistoryEntry right) =>
        left is not null
        && PathEquals(left.FolderPath, right.FolderPath)
        && PathEquals(left.FolderTreeRootPath, right.FolderTreeRootPath);

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

public sealed record SortOption(string Label, SortState State);
