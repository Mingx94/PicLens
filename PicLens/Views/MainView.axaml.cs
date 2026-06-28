using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PicLens.Core.Domain;
using PicLens.Core.Models;
using PicLens.Infrastructure.Services;
using PicLens.Services;
using PicLens.ViewModels;
using static PicLens.Core.Domain.PathRules;

namespace PicLens.Views;

public partial class MainView : UserControl
{
    private const double PointerDragThreshold = 8;
    private const double KeyboardPanStep = 48;
    private const double ThumbnailPreloadMargin = 240;

    private readonly List<LibraryTileItem> librarySelectionOrder = [];
    private readonly TranslateTransform libraryDragPreviewTransform = new();
    private readonly DispatcherTimer libraryDragAutoScrollTimer;
    private readonly DispatcherTimer thumbnailSizeCommitTimer;
    private ImageViewerWindowViewModel previewViewModel = new();
    private LibraryTileItem? pointerDragSource;
    private LibraryTileItem? currentDropRenameTarget;
    private LibraryTileItem? contextMenuItem;
    private ScrollViewer? libraryGridScrollViewer;
    private Avalonia.Point pointerDragStartPosition;
    private Avalonia.Point libraryDragLastPosition;
    private Avalonia.Point viewerLastPointerPosition;
    private bool pointerDragStarted;
    private bool viewerIsDragging;
    private bool isPreviewOpen;
    private bool initialized;
    private bool initialLoadCompleted;
    private bool libraryGridScrollViewerTracked;
    private IReadOnlyList<LibraryTileItem> pointerDragItems = [];

    public MainView()
    {
        ViewModel = new MainPageViewModel(
            new JsonSettingsStore(),
            new FolderScanner(),
            new FileOperationService(),
            new ThumbnailService(),
            new AvaloniaDialogService(this),
            openImageViewer: OpenImageViewer,
            hasUiThreadAccess: () => Dispatcher.UIThread.CheckAccess(),
            tryEnqueueOnUiThread: action =>
            {
                Dispatcher.UIThread.Post(action);
                return true;
            },
            appLogger: App.Logger);

        InitializeComponent();
        DataContext = ViewModel;
        ViewerSurface.DataContext = previewViewModel;
        LibraryDragPreviewOverlay.RenderTransform = libraryDragPreviewTransform;
        AddHandler(KeyDownEvent, Root_KeyDown, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        libraryDragAutoScrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        libraryDragAutoScrollTimer.Tick += LibraryDragAutoScrollTimer_Tick;
        thumbnailSizeCommitTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        thumbnailSizeCommitTimer.Tick += ThumbnailSizeCommitTimer_Tick;
        ThumbnailSizeSlider.PropertyChanged += ThumbnailSizeSlider_PropertyChanged;
        Loaded += OnLoaded;
    }

    public MainPageViewModel ViewModel { get; }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        FolderTree.AddHandler(TreeViewItem.ExpandedEvent, FolderTreeItem_Expanded);
        _ = InitializeAfterLoadedAsync();
    }

    private async Task InitializeAfterLoadedAsync()
    {
        try
        {
            await ViewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            App.Logger.Error(ex, "Main view initialization failed.");
        }
        finally
        {
            initialLoadCompleted = true;
        }
    }

    private async void FolderTree_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (FolderTree.SelectedItem is FolderTreeItem node
            && node.IsReadable
            && !string.IsNullOrWhiteSpace(node.Path)
            && !PathEquals(node.Path, ViewModel.CurrentFolderPath))
        {
            await ViewModel.NavigateToFolderAsync(node.Path, persist: true, resetFolderTreeRoot: false);
        }
    }

    private async void FolderTreeItem_Expanded(object? sender, RoutedEventArgs e)
    {
        if (e.Source is TreeViewItem { DataContext: FolderTreeItem node })
        {
            await ViewModel.LoadFolderChildrenOnDemandAsync(node);
        }
    }

    private void LibraryGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var ordered = OrderedSelectedLibraryItems();
        ViewModel.UpdateSelectedLibraryItems(ordered);
    }

    private async void LibraryTile_Tapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: LibraryTileItem { IsFolder: true } item })
        {
            await OpenTileAsync(item);
            e.Handled = true;
        }
    }

    private async void LibraryTile_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: LibraryTileItem item } && !item.IsFolder)
        {
            await OpenTileAsync(item);
            e.Handled = true;
        }
    }

    private void LibraryTile_RightTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control { DataContext: LibraryTileItem item } control || item.IsFolder)
        {
            return;
        }

        contextMenuItem = item;
        if (SelectedLibraryTiles().All(selected => !PathEquals(selected.Path, item.Path)))
        {
            LibraryGrid.SelectedItems?.Clear();
            LibraryGrid.SelectedItems?.Add(item);
        }

        var menu = new ContextMenu();
        var reveal = new MenuItem { Header = "在檔案管理器中顯示" };
        AutomationProperties.SetAutomationId(reveal, "ImageContextRevealInFileExplorerButton");
        reveal.Click += RevealInFileExplorer;
        menu.Items.Add(reveal);
        var rename = new MenuItem { Header = "重新命名", Command = ViewModel.RenameSelectedCommand };
        AutomationProperties.SetAutomationId(rename, "ImageContextRenameButton");
        menu.Items.Add(rename);
        var trash = new MenuItem { Header = "移至回收筒", Command = ViewModel.TrashSelectedCommand };
        AutomationProperties.SetAutomationId(trash, "ImageContextTrashButton");
        menu.Items.Add(trash);
        menu.Open(control);
        e.Handled = true;
    }

    private async void LibraryGrid_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && await OpenSelectedLibraryItemAsync())
        {
            e.Handled = true;
        }
    }

    private async void Root_KeyDown(object? sender, KeyEventArgs e)
    {
        if (isPreviewOpen)
        {
            ViewerSurface_KeyDown(sender, e);
            return;
        }

        if (e.Key == Key.Enter && await OpenSelectedLibraryItemAsync())
        {
            e.Handled = true;
        }
        else if (e.Key == Key.BrowserBack && ViewModel.BackCommand.CanExecute(null))
        {
            ViewModel.BackCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.BrowserForward && ViewModel.ForwardCommand.CanExecute(null))
        {
            ViewModel.ForwardCommand.Execute(null);
            e.Handled = true;
        }
    }

    private async Task<bool> OpenSelectedLibraryItemAsync()
    {
        var item = SelectedLibraryItemForOpen();
        if (item is null)
        {
            return false;
        }

        await OpenTileAsync(item);
        return true;
    }

    private LibraryTileItem? SelectedLibraryItemForOpen()
    {
        if (LibraryGrid.SelectedItems is null || LibraryGrid.SelectedItems.Count == 0)
        {
            return null;
        }

        var ordered = OrderedSelectedLibraryItems();
        return ordered.FirstOrDefault(item => !item.IsFolder) ?? ordered.FirstOrDefault();
    }

    private async Task OpenTileAsync(LibraryTileItem item)
    {
        if (item.IsFolder)
        {
            await ViewModel.NavigateToFolderAsync(item.Path, persist: true, resetFolderTreeRoot: false);
        }
        else if (item.SourceItem is ImageListItem image)
        {
            ViewModel.OpenImage(image);
        }
    }

    private void RevealInFileExplorer(object? sender, RoutedEventArgs e)
    {
        var item = contextMenuItem;
        if (item is null || item.IsFolder || !File.Exists(item.Path))
        {
            return;
        }

        try
        {
            Process.Start(CreateRevealStartInfo(item.Path));
        }
        catch (Exception ex)
        {
            App.Logger.Error(ex, $"Reveal in file manager failed. Path={item.Path}");
        }
    }

    private static ProcessStartInfo CreateRevealStartInfo(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{path}\"",
                UseShellExecute = true
            };
        }

        if (OperatingSystem.IsLinux())
        {
            var directory = Path.GetDirectoryName(path)
                ?? throw new IOException("Path must include a directory.");
            var startInfo = new ProcessStartInfo
            {
                FileName = "xdg-open",
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add(directory);
            return startInfo;
        }

        throw new PlatformNotSupportedException("Reveal is only supported on Windows and Linux.");
    }

    private void LibraryTile_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: LibraryTileItem item } || item.IsFolder)
        {
            return;
        }

        var point = e.GetCurrentPoint(LibraryGrid);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        pointerDragSource = item;
        pointerDragStartPosition = point.Position;
        libraryDragLastPosition = point.Position;
        pointerDragStarted = false;
        pointerDragItems = [];
    }

    private void LibraryTile_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (pointerDragSource is null)
        {
            return;
        }

        var point = e.GetPosition(LibraryGrid);
        libraryDragLastPosition = point;

        if (!pointerDragStarted)
        {
            var distance = point - pointerDragStartPosition;
            if (Math.Abs(distance.X) < PointerDragThreshold && Math.Abs(distance.Y) < PointerDragThreshold)
            {
                return;
            }

            pointerDragStarted = true;
            pointerDragItems = DragItemsFor(pointerDragSource);
            ViewModel.BeginImageDrag(pointerDragItems);
            if (sender is Control control)
            {
                e.Pointer.Capture(control);
            }
        }

        UpdateDragPreview(e.GetPosition(this));
        SetDropRenameTarget(DropRenameTargetAt(point));
        UpdateLibraryDragAutoScroll(point);
        e.Handled = true;
    }

    private async void LibraryTile_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!pointerDragStarted)
        {
            ClearPointerDrag();
            return;
        }

        var target = DropRenameTargetAt(e.GetPosition(LibraryGrid)) ?? currentDropRenameTarget;
        ClearPointerDrag();
        if (target is not null)
        {
            await ViewModel.DropDraggedImagesOnAsync(target);
            QueueVisibleThumbnailLoads();
        }
    }

    private void LibraryTile_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => ClearPointerDrag(false);

    private IReadOnlyList<LibraryTileItem> DragItemsFor(LibraryTileItem source)
    {
        if (SelectedLibraryTiles().Any(item => PathEquals(item.Path, source.Path)))
        {
            return OrderedSelectedLibraryItems().Where(item => !item.IsFolder).ToList();
        }

        return source.IsFolder ? [] : [source];
    }

    private LibraryTileItem? DropRenameTargetAt(Avalonia.Point position)
    {
        if (pointerDragSource is null)
        {
            return null;
        }

        var hit = LibraryGrid.InputHitTest(position) as StyledElement;
        var item = FindLibraryTileItem(hit);
        return item is not null && CanDropDraggedItem(pointerDragSource, item) ? item : null;
    }

    private static bool CanDropDraggedItem(LibraryTileItem source, LibraryTileItem target) =>
        !source.IsFolder
        && !target.IsFolder
        && !PathEquals(source.Path, target.Path);

    private void SetDropRenameTarget(LibraryTileItem? target)
    {
        if (ReferenceEquals(currentDropRenameTarget, target))
        {
            return;
        }

        if (currentDropRenameTarget is not null)
        {
            currentDropRenameTarget.IsDropRenameTarget = false;
        }

        currentDropRenameTarget = target;
        if (currentDropRenameTarget is not null)
        {
            currentDropRenameTarget.IsDropRenameTarget = true;
        }
    }

    private void ClearPointerDrag(bool releaseCapture = true)
    {
        if (releaseCapture)
        {
            pointerDragSource = null;
        }

        pointerDragStarted = false;
        pointerDragItems = [];
        SetDropRenameTarget(null);
        HideDragPreview();
        StopLibraryDragAutoScroll();
    }

    private void UpdateDragPreview(Avalonia.Point position)
    {
        var count = pointerDragItems.Count;
        LibraryDragPreviewText.Text = count <= 1 ? "拖曳 1 張圖片" : $"拖曳 {count} 張圖片";
        libraryDragPreviewTransform.X = position.X + 12;
        libraryDragPreviewTransform.Y = position.Y + 12;
        LibraryDragPreviewOverlay.IsVisible = true;
    }

    private void HideDragPreview()
    {
        LibraryDragPreviewOverlay.IsVisible = false;
    }

    private void UpdateLibraryDragAutoScroll(Avalonia.Point position)
    {
        libraryDragLastPosition = position;
        if (DragInteractionRules.CalculateLibraryDragAutoScrollDelta(position.Y, LibraryGrid.Bounds.Height) == 0)
        {
            StopLibraryDragAutoScroll();
            return;
        }

        if (!libraryDragAutoScrollTimer.IsEnabled)
        {
            libraryDragAutoScrollTimer.Start();
        }
    }

    private void StopLibraryDragAutoScroll()
    {
        if (libraryDragAutoScrollTimer.IsEnabled)
        {
            libraryDragAutoScrollTimer.Stop();
        }
    }

    private void LibraryDragAutoScrollTimer_Tick(object? sender, EventArgs e)
    {
        EnsureLibraryGridScrollViewer();
        if (libraryGridScrollViewer is null)
        {
            return;
        }

        var delta = DragInteractionRules.CalculateLibraryDragAutoScrollDelta(libraryDragLastPosition.Y, LibraryGrid.Bounds.Height);
        if (delta == 0)
        {
            StopLibraryDragAutoScroll();
            return;
        }

        libraryGridScrollViewer.Offset = libraryGridScrollViewer.Offset.WithY(Math.Max(0, libraryGridScrollViewer.Offset.Y + delta));
    }

    private void LibraryTile_Loaded(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: LibraryTileItem item })
        {
            Dispatcher.UIThread.Post(() => QueueThumbnailLoadIfVisible((Control)sender, item));
        }
    }

    private void LibraryTile_Unloaded(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: LibraryTileItem item })
        {
            ViewModel.CancelThumbnailLoad(item);
        }
    }

    private void QueueVisibleThumbnailLoads()
    {
        EnsureLibraryGridScrollViewer();
        foreach (var tile in LibraryGrid.GetVisualDescendants().OfType<Border>())
        {
            if (tile.Classes.Contains("tile") && tile.DataContext is LibraryTileItem item)
            {
                QueueThumbnailLoadIfVisible(tile, item);
            }
        }
    }

    private void QueueThumbnailLoadIfVisible(Control tile, LibraryTileItem item)
    {
        if (IsInLibraryGridViewport(tile))
        {
            QueueThumbnailLoad(item);
        }
    }

    private void QueueThumbnailLoad(LibraryTileItem item)
    {
        Dispatcher.UIThread.Post(() => _ = ViewModel.LoadThumbnailAsync(item));
    }

    private void EnsureLibraryGridScrollViewer()
    {
        if (libraryGridScrollViewer is null)
        {
            libraryGridScrollViewer = LibraryGrid.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        }

        if (libraryGridScrollViewer is not null && !libraryGridScrollViewerTracked)
        {
            libraryGridScrollViewer.ScrollChanged += LibraryGridScrollViewer_ScrollChanged;
            libraryGridScrollViewerTracked = true;
        }
    }

    private void LibraryGridScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e) =>
        QueueVisibleThumbnailLoads();

    private bool IsInLibraryGridViewport(Control tile)
    {
        EnsureLibraryGridScrollViewer();
        if (libraryGridScrollViewer is null)
        {
            return true;
        }

        var topLeft = tile.TranslatePoint(new Avalonia.Point(), libraryGridScrollViewer);
        if (topLeft is null)
        {
            return false;
        }

        // ponytail: keep ListBox selection and only gate thumbnail work; use ItemsRepeater if grid virtualization is needed.
        var viewport = new Rect(
            -ThumbnailPreloadMargin,
            -ThumbnailPreloadMargin,
            libraryGridScrollViewer.Bounds.Width + ThumbnailPreloadMargin * 2,
            libraryGridScrollViewer.Bounds.Height + ThumbnailPreloadMargin * 2);
        var bounds = new Rect(topLeft.Value, tile.Bounds.Size);
        return viewport.Intersects(bounds);
    }

    private async void ThumbnailSizeSlider_CommitValue(object? sender, RoutedEventArgs e)
    {
        await CommitThumbnailSizeSliderValueAsync();
    }

    private async void ThumbnailSizeSlider_KeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await CommitThumbnailSizeSliderValueAsync();
            e.Handled = true;
        }
    }

    private void ThumbnailSizeSlider_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (!initialLoadCompleted || e.Property != RangeBase.ValueProperty)
        {
            return;
        }

        thumbnailSizeCommitTimer.Stop();
        thumbnailSizeCommitTimer.Start();
    }

    private async void ThumbnailSizeCommitTimer_Tick(object? sender, EventArgs e)
    {
        thumbnailSizeCommitTimer.Stop();
        await CommitThumbnailSizeSliderValueAsync();
    }

    private async Task CommitThumbnailSizeSliderValueAsync()
    {
        await ViewModel.ChangeThumbnailSizeAsync(ThumbnailSizeSlider.Value);
        QueueVisibleThumbnailLoads();
    }

    private IReadOnlyList<LibraryTileItem> OrderedSelectedLibraryItems()
    {
        var selectedItems = SelectedLibraryTiles().ToList();
        var selectedPaths = selectedItems.Select(item => PathKey(item.Path)).ToHashSet(PathComparer);
        librarySelectionOrder.RemoveAll(item => !selectedPaths.Contains(PathKey(item.Path)));

        var ordered = librarySelectionOrder
            .Where(item => selectedPaths.Contains(PathKey(item.Path)))
            .ToList();

        foreach (var item in selectedItems)
        {
            if (!ordered.Any(existing => PathEquals(existing.Path, item.Path)))
            {
                ordered.Add(item);
            }
        }

        return ordered;
    }

    private static LibraryTileItem? FindLibraryTileItem(object? originalSource)
    {
        var current = originalSource as StyledElement;
        while (current is not null)
        {
            if (current.DataContext is LibraryTileItem item)
            {
                return item;
            }

            current = current is Visual visual ? visual.GetVisualParent() as StyledElement : null;
        }

        return null;
    }

    private void OpenImageViewer(ImageSequenceSnapshot snapshot)
    {
        UnsubscribePreviewViewModelEvents();
        previewViewModel = new ImageViewerWindowViewModel(snapshot);
        previewViewModel.PropertyChanged += PreviewViewModel_PropertyChanged;
        ViewerSurface.DataContext = previewViewModel;
        isPreviewOpen = true;
        App.Logger.Info($"Inline image viewer opened. {ViewerContext()}");
        UpdateWindowTitleForViewer();
        ViewerSurface.IsVisible = true;
        previewViewModel.UpdateViewport(ViewerImageSurface.Bounds.Width, ViewerImageSurface.Bounds.Height);
        ViewerSurface.Focus();
    }

    private void CloseViewer_Click(object? sender, RoutedEventArgs e) => CloseInlineViewer();

    private void CloseInlineViewer()
    {
        if (!isPreviewOpen)
        {
            return;
        }

        viewerIsDragging = false;
        isPreviewOpen = false;
        App.Logger.Info($"Inline image viewer closed. {ViewerContext()}");
        UnsubscribePreviewViewModelEvents();
        UpdateWindowTitleForViewer();
        ViewerSurface.IsVisible = false;
        LibraryGrid.Focus();
    }

    private void PreviewViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ImageViewerWindowViewModel.CurrentImageName)
            or nameof(ImageViewerWindowViewModel.WindowTitle))
        {
            UpdateWindowTitleForViewer();
        }
    }

    private void UnsubscribePreviewViewModelEvents()
    {
        previewViewModel.PropertyChanged -= PreviewViewModel_PropertyChanged;
    }

    private void UpdateWindowTitleForViewer()
    {
        if (TopLevel.GetTopLevel(this) is MainWindow window)
        {
            window.SetViewerTitle(isPreviewOpen ? previewViewModel.CurrentImageName : null);
        }
    }

    private void ViewerImageSurface_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        previewViewModel.UpdateViewport(e.NewSize.Width, e.NewSize.Height);
    }

    private void ViewerImageSurface_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        previewViewModel.ZoomAt(e.GetPosition(ViewerImageSurface).X, e.GetPosition(ViewerImageSurface).Y, e.Delta.Y > 0 ? 1 : -1);
        e.Handled = true;
    }

    private void ViewerImageSurface_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(ViewerImageSurface);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        viewerIsDragging = true;
        viewerLastPointerPosition = point.Position;
        e.Pointer.Capture(ViewerImageSurface);
        e.Handled = true;
    }

    private void ViewerImageSurface_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!viewerIsDragging)
        {
            return;
        }

        var position = e.GetPosition(ViewerImageSurface);
        previewViewModel.PanBy(position.X - viewerLastPointerPosition.X, position.Y - viewerLastPointerPosition.Y);
        viewerLastPointerPosition = position;
        e.Handled = true;
    }

    private void ViewerImageSurface_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        viewerIsDragging = false;
        e.Pointer.Capture(null);
    }

    private void ViewerImageSurface_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        viewerIsDragging = false;
    }

    private void ViewerSurface_KeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                CloseInlineViewer();
                e.Handled = true;
                break;
            case Key.Left:
                if (!previewViewModel.TryPanByKeyboard(KeyboardPanStep, 0)
                    && previewViewModel.PreviousCommand.CanExecute(null))
                {
                    previewViewModel.PreviousCommand.Execute(null);
                }
                e.Handled = true;
                break;
            case Key.Right:
                if (!previewViewModel.TryPanByKeyboard(-KeyboardPanStep, 0)
                    && previewViewModel.NextCommand.CanExecute(null))
                {
                    previewViewModel.NextCommand.Execute(null);
                }
                e.Handled = true;
                break;
            case Key.Up:
                e.Handled = previewViewModel.TryPanByKeyboard(0, KeyboardPanStep);
                break;
            case Key.Down:
                e.Handled = previewViewModel.TryPanByKeyboard(0, -KeyboardPanStep);
                break;
        }
    }

    private string ViewerContext() =>
        $"CurrentIndex={previewViewModel.CurrentIndex}; ImageCount={previewViewModel.Snapshot.Images.Count}; CurrentImage={previewViewModel.CurrentImageName}; SourceFolderPath={previewViewModel.Snapshot.SourceFolderPath}; IncludeSubfolders={previewViewModel.Snapshot.IncludeSubfolders}; Sort={previewViewModel.Snapshot.Sort.Key}/{previewViewModel.Snapshot.Sort.Direction}";

    private sealed class AvaloniaDialogService(MainView view) : IDialogService
    {
        public async Task<string?> ChooseFolderAsync()
        {
            var topLevel = TopLevel.GetTopLevel(view);
            if (topLevel is null || !topLevel.StorageProvider.CanPickFolder)
            {
                return null;
            }

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "選擇圖片資料夾",
                AllowMultiple = false
            });

            return folders.Count == 0
                ? null
                : folders[0].TryGetLocalPath() ?? folders[0].Path.LocalPath;
        }

        public async Task<bool> ConfirmAsync(string message, string title, string confirmButtonText)
        {
            var owner = TopLevel.GetTopLevel(view) as Window;
            if (owner is null)
            {
                return false;
            }

            return await SimpleDialogWindow.ConfirmAsync(owner, title, message, confirmButtonText);
        }

        public async Task<bool> ConfirmDropRenameAsync(DropRenamePreview preview)
        {
            var owner = TopLevel.GetTopLevel(view) as Window;
            if (owner is null)
            {
                return false;
            }

            var lines = preview.Items
                .Take(12)
                .Select(item => item.WillRename
                    ? $"{item.SourceName} → {item.TargetName}"
                    : $"{item.SourceName}：{ReasonText(item.Reason)}");
            var suffix = preview.Items.Count > 12 ? $"{Environment.NewLine}另有 {preview.Items.Count - 12} 個項目..." : string.Empty;
            var message = $"將重新命名 {preview.RenameCount} 個，略過 {preview.SkippedCount} 個。{Environment.NewLine}{Environment.NewLine}{string.Join(Environment.NewLine, lines)}{suffix}";
            return await SimpleDialogWindow.ConfirmAsync(owner, "確認拖放重新命名", message, "套用重新命名");
        }

        public async Task<string?> RequestRenameAsync(ImageListItem item)
        {
            var owner = TopLevel.GetTopLevel(view) as Window;
            if (owner is null)
            {
                return null;
            }

            return await RenameDialogWindow.RequestAsync(owner, item.Name);
        }

        private static string ReasonText(string? reason) =>
            reason switch
            {
                "target_exists" => "目標已存在",
                "same_path" => "來源與目標相同",
                "invalid_name" => "檔名無效",
                _ => reason ?? "已略過"
            };
    }

    private sealed class SimpleDialogWindow : Window
    {
        private SimpleDialogWindow(string title, string message, string confirmButtonText)
        {
            Title = title;
            Width = 440;
            Height = 260;
            MinWidth = 360;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            CanResize = false;

            var text = new TextBlock
            {
                Text = message,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            };
            var cancel = new Button { Content = "取消" };
            var confirm = new Button { Content = confirmButtonText };
            cancel.Click += (_, _) => Close(false);
            confirm.Click += (_, _) => Close(true);
            var buttons = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Children = { cancel, confirm }
            };
            Grid.SetRow(buttons, 1);

            Content = new Grid
            {
                Margin = new Thickness(18),
                RowDefinitions = RowDefinitions.Parse("*,Auto"),
                Children =
                {
                    new ScrollViewer { Content = text },
                    buttons
                }
            };
        }

        public static Task<bool> ConfirmAsync(Window owner, string title, string message, string confirmButtonText) =>
            new SimpleDialogWindow(title, message, confirmButtonText).ShowDialog<bool>(owner);
    }

    private sealed class RenameDialogWindow : Window
    {
        private RenameDialogWindow(string fileName)
        {
            Title = "重新命名選取的圖片";
            Width = 420;
            Height = 160;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            CanResize = false;

            var input = new TextBox
            {
                Text = fileName,
                PlaceholderText = "輸入新的檔案名稱",
                MinWidth = 320
            };
            input.SelectionStart = 0;
            input.SelectionEnd = Path.GetFileNameWithoutExtension(fileName).Length;

            var cancel = new Button { Content = "取消" };
            var confirm = new Button { Content = "重新命名" };
            cancel.Click += (_, _) => Close(null);
            confirm.Click += (_, _) => Close(input.Text);

            Content = new StackPanel
            {
                Margin = new Thickness(18),
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = "新檔名" },
                    input,
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        Spacing = 8,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Children = { cancel, confirm }
                    }
                }
            };

            Opened += (_, _) => input.Focus();
        }

        public static Task<string?> RequestAsync(Window owner, string fileName) =>
            new RenameDialogWindow(fileName).ShowDialog<string?>(owner);
    }

    private IEnumerable<LibraryTileItem> SelectedLibraryTiles() =>
        LibraryGrid.SelectedItems?.Cast<LibraryTileItem>() ?? [];
}
