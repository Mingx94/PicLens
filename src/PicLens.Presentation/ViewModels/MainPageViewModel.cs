using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PicLens.Core.Services;
using PicLens.Core.Domain;
using PicLens.Core.Models;
using PicLens.Diagnostics;
using PicLens.Services;
using System.Collections.ObjectModel;
using static PicLens.Core.Domain.PathRules;
using static PicLens.ViewModels.ViewModelPathRules;

namespace PicLens.ViewModels;

public sealed partial class MainPageViewModel : ObservableObject
{
    private readonly ISettingsStore settingsStore;
    private readonly IFolderScanner folderScanner;
    private readonly IFileOperationService fileOperationService;
    private readonly IThumbnailService thumbnailService;
    private readonly IAppLogger appLogger;
    private readonly IDialogService dialogService;
    private readonly Action<ImageSequenceSnapshot> openImageViewer;
    private readonly Func<bool> hasUiThreadAccess;
    private readonly Func<Action, bool> tryEnqueueOnUiThread;
    private readonly FolderTreeModule folderTree;
    private readonly FolderNavigationHistory folderHistory = new();
    private readonly List<string> selectedImagePaths = [];
    private readonly List<ImageListItem> dragSources = [];

    private AppSettings settings = AppSettings.CreateDefault();
    private IReadOnlyList<ListItem> currentItems = [];
    private bool suppressIncludeSubfoldersReload;
    private CancellationTokenSource? libraryLoadCancellationSource;

    public MainPageViewModel(
        ISettingsStore settingsStore,
        IFolderScanner folderScanner,
        IFileOperationService fileOperationService,
        IThumbnailService thumbnailService,
        IDialogService dialogService,
        Action<ImageSequenceSnapshot>? openImageViewer = null,
        Func<bool>? hasUiThreadAccess = null,
        Func<Action, bool>? tryEnqueueOnUiThread = null,
        TimeSpan? thumbnailLoadTimeout = null,
        IAppLogger? appLogger = null)
    {
        this.settingsStore = settingsStore;
        this.folderScanner = folderScanner;
        this.fileOperationService = fileOperationService;
        this.thumbnailService = thumbnailService;
        this.dialogService = dialogService;
        this.openImageViewer = openImageViewer ?? (_ => { });
        this.hasUiThreadAccess = hasUiThreadAccess ?? (() => true);
        this.tryEnqueueOnUiThread = tryEnqueueOnUiThread ?? (action =>
        {
            action();
            return true;
        });
        this.thumbnailLoadTimeout = thumbnailLoadTimeout is { } timeout && timeout > TimeSpan.Zero
            ? timeout
            : DefaultThumbnailLoadTimeout;
        this.appLogger = appLogger ?? NullAppLogger.Instance;
        folderTree = new FolderTreeModule(folderScanner, this.appLogger);
    }

    [ObservableProperty]
    public partial string CurrentFolderPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "就緒。PicLens 已初始化。";

    [ObservableProperty]
    public partial bool IncludeSubfolders { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThumbnailSizeLabel))]
    [NotifyPropertyChangedFor(nameof(LibraryTileLayoutHeight))]
    public partial int ThumbnailSize { get; set; } = SettingsRules.DefaultThumbnailSize;

    [ObservableProperty]
    public partial SortState Sort { get; set; } = new(SortKey.Name, SortDirection.Asc);

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public ObservableCollection<FolderTreeItem> FolderRoots => folderTree.Roots;

    public ObservableRangeCollection<LibraryTileItem> LibraryItems { get; } = [];

    public bool HasCurrentFolder => !string.IsNullOrWhiteSpace(CurrentFolderPath);

    public bool HasNoCurrentFolder => !HasCurrentFolder;

    public string CurrentFolderName => FolderDisplayName(CurrentFolderPath, "未選擇資料夾", appLogger);

    public string CurrentParentFolderName => ParentFolderDisplayName(CurrentFolderPath, appLogger);

    public string RecursiveModeLabel => IncludeSubfolders ? "含子資料夾" : "僅目前資料夾";

    public string SortLabel => SortOptionLabel(Sort);

    public double ThumbnailSizeMinimum => SettingsRules.MinThumbnailSize;

    public double ThumbnailSizeMaximum => SettingsRules.MaxThumbnailSize;

    public double ThumbnailSizeStep => SettingsRules.ThumbnailSizeStep;

    public string ThumbnailSizeLabel => $"縮圖 {ThumbnailSize}";

    public int LibraryTileLayoutHeight => ThumbnailSize + 56;

    public string LibraryItemCountText => $"{LibraryItems.Count} 個項目";

    public int SelectedImageCount => selectedImagePaths.Count;

    public bool HasSelectedImages => SelectedImageCount > 0;

    public bool HasSingleSelectedImage => SelectedImageCount == 1;

    public async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            settings = SettingsRules.NormalizeSettings(await settingsStore.LoadAsync());
            suppressIncludeSubfoldersReload = true;
            Sort = settings.Sort;
            IncludeSubfolders = settings.IncludeSubfolders;
            ThumbnailSize = settings.ThumbnailSize;
            suppressIncludeSubfoldersReload = false;

            var initialFolder = string.IsNullOrWhiteSpace(settings.LastFolderPath) || !Directory.Exists(settings.LastFolderPath)
                ? null
                : settings.LastFolderPath;
            var shouldPersistInitialFolder = false;
            if (initialFolder is null)
            {
                initialFolder = await dialogService.ChooseFolderAsync();
                shouldPersistInitialFolder = true;
            }

            if (string.IsNullOrWhiteSpace(initialFolder))
            {
                CurrentFolderPath = string.Empty;
                currentItems = [];
                LibraryItems.Clear();
                NotifyLibraryItemCount();
                folderTree.Clear();
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
            SetStatus($"資料夾無法使用：{normalized}");
            return;
        }

        appLogger.Info(
            $"Navigate to folder started. FolderPath={normalized}; ReplaceHistory={replaceHistory}; Persist={persist}; ResetFolderTreeRoot={resetFolderTreeRoot}; IncludeSubfolders={IncludeSubfolders}; Sort={Sort.Key}/{Sort.Direction}");

        ClearSelection();
        if (resetFolderTreeRoot || string.IsNullOrWhiteSpace(folderTree.RootPath))
        {
            folderTree.UseRoot(normalized);
        }

        CurrentFolderPath = normalized;
        folderHistory.Record(new FolderNavigationHistory.Entry(normalized, folderTree.RootPath), replaceHistory);
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
            var preview = CreateDropRenamePreview(
                FileRenamePlanner.PlanDropTargetBatchRename(
                    dragSources.Select(image => image.Path),
                    targetImage.Path,
                    ExistingTargetDirectoryFiles(targetImage.Path)));
            if (preview.Total == 0)
            {
                SetStatus("沒有可拖放重新命名的圖片。");
                appLogger.Info(
                    $"Drop dragged images ignored. Reason=EmptyPreview; Target={targetImage.Name}; TargetPath={targetImage.Path}");
                return;
            }

            if (!await dialogService.ConfirmDropRenameAsync(preview))
            {
                SetStatus("已取消拖放重新命名。");
                appLogger.Info(
                    $"Drop dragged images canceled. Total={preview.Total}; RenameCount={preview.RenameCount}; SkippedCount={preview.SkippedCount}; Target={targetImage.Name}");
                return;
            }

            var result = await fileOperationService.RenameByDropTargetAsync(dragSources.Select(image => image.Path), targetImage.Path);
            ClearSelection();
            SetBatchStatus("拖放重新命名", result);
            LogBatchItemFailures("Drop dragged images", result);
            appLogger.Info(
                $"Drop dragged images completed. Total={result.Total}; Succeeded={result.Succeeded}; Skipped={result.Skipped}; Failed={result.Failed}; Target={targetImage.Name}");
            await LoadLibraryAsync();
        }
        catch (Exception ex)
        {
            appLogger.Error(ex, "Drop dragged images failed.");
            SetStatus("拖放重新命名時發生錯誤，已寫入診斷記錄。");
        }
        finally
        {
            dragSources.Clear();
        }
    }

    private static DropRenamePreview CreateDropRenamePreview(DropTargetBatchRenamePlan plan)
    {
        var items = plan.Items
            .Select(item => new DropRenamePreviewItem(
                SourcePath: item.SourcePath,
                SourceName: Path.GetFileName(item.SourcePath),
                TargetPath: item.TargetPath,
                TargetName: Path.GetFileName(item.TargetPath),
                WillRename: !item.ShouldSkip,
                Reason: item.Reason))
            .ToList();

        return new DropRenamePreview(
            Total: plan.Total,
            RenameCount: items.Count(item => item.WillRename),
            SkippedCount: items.Count(item => !item.WillRename),
            Items: items);
    }

    private void LogBatchItemFailures(string operationName, FileOperationBatchResult result)
    {
        foreach (var item in result.Items.Where(item => item.Status == FileOperationStatus.Failed))
        {
            var details = item.Message ?? item.Reason ?? "File operation failed.";
            appLogger.Error(
                new IOException(details),
                $"{operationName} item failed. Path={item.Path}; TargetPath={item.TargetPath ?? "<none>"}; Reason={item.Reason ?? "<none>"}");
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
        OnPropertyChanged(nameof(HasNoCurrentFolder));
        OnPropertyChanged(nameof(CurrentFolderName));
        OnPropertyChanged(nameof(CurrentParentFolderName));
    }

    partial void OnSortChanged(SortState value)
    {
        OnPropertyChanged(nameof(SortLabel));
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
            SetStatus($"已從 {CurrentFolderPath} 載入 {LibraryItems.Count} 個項目。");
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
    private async Task ConvertVisible()
    {
        var result = await fileOperationService.ConvertVisibleToJpgAsync(VisibleImages());
        SetBatchStatus("轉換為 JPG", result);
        await LoadLibraryAsync();
    }

    [RelayCommand(CanExecute = nameof(HasSelectedImages))]
    private async Task ConvertSelected()
    {
        var selected = SelectedImages();
        if (selected.Count == 0)
        {
            return;
        }

        var result = await fileOperationService.ConvertVisibleToJpgAsync(selected);
        SetBatchStatus("轉成 JPG", result);
        ClearSelection();
        await LoadLibraryAsync();
    }

    [RelayCommand]
    private async Task ClearSameBasename()
    {
        if (!await dialogService.ConfirmAsync("要將同名的非 JPG 檔案移至回收筒嗎？", "清除同名檔案", "移至回收筒"))
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
        try
        {
            var selected = SelectedImages().SingleOrDefault();
            if (selected is null)
            {
                return;
            }

            var nextName = await dialogService.RequestRenameAsync(selected);
            if (string.IsNullOrWhiteSpace(nextName))
            {
                return;
            }

            var result = await fileOperationService.RenameAsync(selected.Path, nextName);
            SetStatus(result.Status == FileOperationStatus.Renamed
                ? $"已重新命名為 {Path.GetFileName(result.TargetPath)}。"
                : result.Message ?? result.Reason ?? "重新命名已略過。");
            ClearSelection();
            await LoadLibraryAsync();
        }
        catch (Exception ex)
        {
            appLogger.Error(ex, "Rename selected image failed.");
            SetStatus("重新命名時發生錯誤，已寫入診斷記錄。");
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedImages))]
    private async Task TrashSelected()
    {
        try
        {
            var selected = SelectedImages();
            if (selected.Count == 0)
            {
                return;
            }

            var message = selected.Count == 1
                ? $"要將「{selected[0].Name}」移至回收筒嗎？"
                : $"要將選取的 {selected.Count} 張圖片移至回收筒嗎？";
            if (!await dialogService.ConfirmAsync(message, "將選取的圖片移至回收筒", "移至回收筒"))
            {
                return;
            }

            var results = new List<FileOperationResult>(selected.Count);
            foreach (var image in selected)
            {
                results.Add(await fileOperationService.TrashAsync(image.Path));
            }

            var batch = new FileOperationBatchResult(
                Total: results.Count,
                Succeeded: results.Count(result => result.Status == FileOperationStatus.Trashed),
                Skipped: results.Count(result => result.Status is not FileOperationStatus.Trashed and not FileOperationStatus.Failed),
                Failed: results.Count(result => result.Status == FileOperationStatus.Failed),
                Items: results);
            SetBatchStatus("移至回收筒", batch);
            ClearSelection();
            await LoadLibraryAsync();
        }
        catch (Exception ex)
        {
            appLogger.Error(ex, "Trash selected images failed.");
            SetStatus("移至回收筒時發生錯誤，已寫入診斷記錄。");
        }
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
        IsBusy = true;
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
            SetStatus($"無法載入資料夾：{ex.Message}");
        }
        finally
        {
            if (ReferenceEquals(libraryLoadCancellationSource, load.CancellationSource))
            {
                libraryLoadCancellationSource = null;
                IsBusy = false;
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
        IsBusy = false;
    }

    private bool IsCurrentLibraryLoad(LibraryLoadState load) =>
        ReferenceEquals(libraryLoadCancellationSource, load.CancellationSource)
        && !load.CancellationSource.IsCancellationRequested;

    private void RefreshLibraryItems()
    {
        CancelAllThumbnailLoads();
        LibraryItems.ReplaceAll(currentItems.Select(item =>
        {
            var tile = ToTile(item);
            tile.ApplyThumbnailSize(ThumbnailSize);
            return tile;
        }));

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

        var createdAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var rawId = $"{CurrentFolderPath}:{createdAtMs}:{currentImagePath}";

        return new ImageSequenceSnapshot(
            Id: $"sequence:{Uri.EscapeDataString(rawId).Replace("%", "_", StringComparison.Ordinal)}",
            CreatedAtMs: createdAtMs,
            SourceFolderPath: CurrentFolderPath,
            IncludeSubfolders: IncludeSubfolders,
            Sort: Sort,
            Images: images.Select(candidate => candidate with { }).ToList(),
            CurrentIndex: currentIndex);
    }

    private void ClearSelection()
    {
        selectedImagePaths.Clear();
        NotifySelectionCommands();
    }

    private void NotifySelectionCommands()
    {
        OnPropertyChanged(nameof(SelectedImageCount));
        OnPropertyChanged(nameof(HasSelectedImages));
        OnPropertyChanged(nameof(HasSingleSelectedImage));
        ConvertSelectedCommand.NotifyCanExecuteChanged();
        RenameSelectedCommand.NotifyCanExecuteChanged();
        TrashSelectedCommand.NotifyCanExecuteChanged();
    }

    private void NotifyLibraryItemCount()
    {
        OnPropertyChanged(nameof(LibraryItemCountText));
    }

    private void NotifyNavigationCommands()
    {
        BackCommand.NotifyCanExecuteChanged();
        ForwardCommand.NotifyCanExecuteChanged();
    }

    private bool CanNavigateBack() => folderHistory.CanBack;

    private bool CanNavigateForward() => folderHistory.CanForward;

    private List<ImageListItem> VisibleImages() => currentItems.OfType<ImageListItem>().ToList();

    private List<ImageListItem> SelectedImages()
    {
        var imagesByPath = VisibleImages().ToDictionary(image => PathKey(image.Path), PathComparer);
        return selectedImagePaths
            .Select(path => imagesByPath.GetValueOrDefault(PathKey(path)))
            .OfType<ImageListItem>()
            .ToList();
    }

    private static LibraryTileItem ToTile(ListItem item) =>
        item switch
        {
            FolderListItem folder => new LibraryTileItem(
                Name: folder.Name,
                Path: folder.Path,
                Detail: "開啟資料夾",
                SourceItem: folder),
            ImageListItem image => new LibraryTileItem(
                Name: image.Name,
                Path: image.Path,
                Detail: $"{image.Extension.ToUpperInvariant()} - {image.SizeBytes / 1024} KB",
                SourceItem: image),
            _ => throw new ArgumentOutOfRangeException(nameof(item))
        };

    private static string DescribeBatchResult(string label, FileOperationBatchResult result) =>
        $"{label}：成功 {result.Succeeded} 個，略過 {result.Skipped} 個，失敗 {result.Failed} 個。";

    private void SetBatchStatus(string label, FileOperationBatchResult result) =>
        SetStatus(DescribeBatchResult(label, result));

    private void SetStatus(string message)
    {
        StatusMessage = message;
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
