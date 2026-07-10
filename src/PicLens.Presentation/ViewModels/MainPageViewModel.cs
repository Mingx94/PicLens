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
    private const int LargeBatchConfirmationThreshold = 50;

    private readonly ISettingsStore settingsStore;
    private readonly IFolderScanner folderScanner;
    private readonly IFileOperationService fileOperationService;
    private readonly IThumbnailService thumbnailService;
    private readonly IAppLogger appLogger;
    private readonly IDialogService dialogService;
    private readonly Action<ImageSequenceSnapshot> openImageViewer;
    private readonly Func<Action, Task> runOnUiThread;
    private readonly FolderTreeModule folderTree;
    private readonly FolderNavigationHistory folderHistory = new();
    private readonly List<string> selectedImagePaths = [];
    private readonly List<ImageListItem> dragSources = [];

    private AppSettings settings = AppSettings.CreateDefault();
    private IReadOnlyList<ListItem> currentItems = [];
    private bool suppressIncludeSubfoldersReload;
    private CancellationTokenSource? libraryLoadCancellationSource;
    private CancellationTokenSource? fileOperationCancellationSource;

    public MainPageViewModel(
        ISettingsStore settingsStore,
        IFolderScanner folderScanner,
        IFileOperationService fileOperationService,
        IThumbnailService thumbnailService,
        IDialogService dialogService,
        Action<ImageSequenceSnapshot>? openImageViewer = null,
        Func<Action, Task>? runOnUiThread = null,
        TimeSpan? thumbnailLoadTimeout = null,
        IAppLogger? appLogger = null)
    {
        this.settingsStore = settingsStore;
        this.folderScanner = folderScanner;
        this.fileOperationService = fileOperationService;
        this.thumbnailService = thumbnailService;
        this.dialogService = dialogService;
        this.openImageViewer = openImageViewer ?? (_ => { });
        this.runOnUiThread = runOnUiThread ?? (action =>
        {
            action();
            return Task.CompletedTask;
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
    [NotifyPropertyChangedFor(nameof(LibraryThumbnailHeight))]
    public partial int ThumbnailSize { get; set; } = SettingsRules.DefaultThumbnailSize;

    [ObservableProperty]
    public partial SortState Sort { get; set; } = new(SortKey.Name, SortDirection.Asc);

    [ObservableProperty]
    public partial string SearchQuery { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEmptyFolder))]
    [NotifyPropertyChangedFor(nameof(HasNoSearchResults))]
    public partial bool IsLibraryLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLibraryError))]
    [NotifyPropertyChangedFor(nameof(HasEmptyFolder))]
    [NotifyPropertyChangedFor(nameof(HasNoSearchResults))]
    public partial string? LibraryErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool IsSidebarOpen { get; set; } = true;

    [ObservableProperty]
    public partial bool IsGridViewMode { get; set; } = true;

    public ObservableCollection<FolderTreeItem> FolderRoots => folderTree.Roots;

    public ObservableRangeCollection<LibraryTileItem> LibraryItems { get; } = [];

    public ObservableRangeCollection<LibraryTileItem> FolderLibraryItems { get; } = [];

    public ObservableRangeCollection<LibraryTileItem> ImageLibraryItems { get; } = [];

    public bool HasSearchQuery => !string.IsNullOrWhiteSpace(SearchQuery);

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

    public int LibraryTileLayoutHeight => IsGridViewMode ? ThumbnailSize + 56 : 100;

    public int LibraryThumbnailHeight => ThumbnailSize - 4;

    public string LibraryItemCountText => $"{LibraryItems.Count} 個項目";

    public string FolderItemCountText => $"資料夾 ({FolderLibraryItems.Count})";

    public string ImageItemCountText => $"圖片 ({ImageLibraryItems.Count})";

    public bool HasFolderLibraryItems => FolderLibraryItems.Count > 0;

    public bool HasImageLibraryItems => ImageLibraryItems.Count > 0;

    public bool HasLibraryError => !string.IsNullOrWhiteSpace(LibraryErrorMessage);

    public bool HasEmptyFolder =>
        HasCurrentFolder && !IsLibraryLoading && !HasLibraryError && !HasSearchQuery && LibraryItems.Count == 0;

    public bool HasNoSearchResults =>
        HasCurrentFolder && !IsLibraryLoading && !HasLibraryError && HasSearchQuery && LibraryItems.Count == 0;

    public int SelectedImageCount => selectedImagePaths.Count;

    public bool HasSelectedImages => SelectedImageCount > 0;

    public bool HasSingleSelectedImage => SelectedImageCount == 1;

    public bool IsListViewMode => !IsGridViewMode;

    public bool IsFileOperationActive => fileOperationCancellationSource is not null;

    public int LibraryLayoutMinItemWidth => IsGridViewMode ? ThumbnailSize + 8 : 640;

    public string SelectedSummaryText => SelectedImageCount switch
    {
        0 => "尚未選取",
        1 => "1 張已選取",
        _ => $"{SelectedImageCount} 張已選取"
    };

    public async Task InitializeAsync()
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
            SetStatus($"已載入 {LibraryItems.Count} 個項目。");
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

    private void SetStatus(string message)
    {
        StatusMessage = message;
    }


}
