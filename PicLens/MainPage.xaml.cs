using PicLens.Core.Models;
using PicLens.Core.Domain;
using PicLens.Diagnostics;
using PicLens.Infrastructure.Services;
using PicLens.ViewModels;
using PicLens.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Pickers;
using Windows.System;
using FoundationPoint = Windows.Foundation.Point;

namespace PicLens;

/// <summary>
/// The main content page displayed inside the application window.
/// </summary>
public sealed partial class MainPage : Page
{
    private const double PointerDragThreshold = 8;

    private readonly List<LibraryTileItem> librarySelectionOrder = [];
    private LibraryTileItem? pointerDragSource;
    private LibraryTileItem? currentDropRenameTarget;
    private FoundationPoint pointerDragStartPosition;
    private Pointer? pointerDragPointer;
    private UIElement? pointerDragCaptureElement;
    private bool pointerDragStarted;
    private IReadOnlyList<LibraryTileItem> pointerDragItems = [];
    private bool initialized;

    public MainPage()
    {
        var settingsStore = new JsonSettingsStore();
        ViewModel = new MainPageViewModel(
            settingsStore,
            new FolderScanner(),
            new FileOperationService(),
            new ThumbnailService(),
            new WinUIDialogService(this),
            new WinUINavigationService(this),
            new WinUIDispatcherService(DispatcherQueue),
            appLogger: App.Logger);

        InitializeComponent();
        Loaded += OnLoaded;
    }

    public MainPageViewModel ViewModel { get; }

    public static BitmapImage? CreateBitmapImage(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return new BitmapImage(new Uri(Path.GetFullPath(path)));
        }
        catch
        {
            return null;
        }
    }

    public static Visibility BoolToVisibility(bool value) =>
        value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility InvertBoolToVisibility(bool value) =>
        value ? Visibility.Collapsed : Visibility.Visible;

    public static InfoBarSeverity StatusSeverityToInfoBarSeverity(MainPageStatusSeverity severity) =>
        severity switch
        {
            MainPageStatusSeverity.Success => InfoBarSeverity.Success,
            MainPageStatusSeverity.Warning => InfoBarSeverity.Warning,
            MainPageStatusSeverity.Error => InfoBarSeverity.Error,
            _ => InfoBarSeverity.Informational
        };

    public static string FolderNameFromPath(string? path) =>
        FolderSegmentFromPath(path, fallback: "未選擇資料夾");

    public static string ParentFolderNameFromPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "資料夾";
        }

        try
        {
            var parent = Directory.GetParent(Path.GetFullPath(path));
            return FolderSegmentFromPath(parent?.FullName, fallback: "資料夾");
        }
        catch (Exception ex)
        {
            App.Logger.Error(ex, $"Parent folder name lookup failed. Path={path}");
            return "資料夾";
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        await ViewModel.InitializeAsync();
    }

    private async void FolderTree_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is FolderTreeItem folder)
        {
            await ViewModel.NavigateToFolderAsync(folder.Path);
        }
    }

    private void LibraryGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        foreach (var removed in e.RemovedItems.OfType<LibraryTileItem>())
        {
            librarySelectionOrder.RemoveAll(item => PathEquals(item.Path, removed.Path));
        }

        foreach (var added in e.AddedItems.OfType<LibraryTileItem>())
        {
            if (!librarySelectionOrder.Any(item => PathEquals(item.Path, added.Path)))
            {
                librarySelectionOrder.Add(added);
            }
        }

        ViewModel.UpdateSelectedLibraryItems(OrderedSelectedLibraryItems());
    }

    private void ClearLibrarySelection_Click(object sender, RoutedEventArgs e)
    {
        LibraryGrid.SelectedItems.Clear();
        librarySelectionOrder.Clear();
        ViewModel.ClearSelectedLibraryItems();
    }

    private void LibraryGrid_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (FindDataContext<LibraryTileItem>(e.OriginalSource) is { } item)
        {
            e.Handled = true;
            QueueOpenLibraryItemFromDoubleTap(item);
        }
    }

    private void LibraryTile_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: LibraryTileItem item })
        {
            e.Handled = true;
            QueueOpenLibraryItemFromDoubleTap(item);
        }
    }

    private void QueueOpenLibraryItemFromDoubleTap(LibraryTileItem item)
    {
        if (!App.DispatcherQueue.TryEnqueue(() => OpenLibraryItemFromDoubleTap(item)))
        {
            OpenLibraryItemFromDoubleTap(item);
        }
    }

    private async void OpenLibraryItemFromDoubleTap(LibraryTileItem item)
    {
        await ViewModel.OpenLibraryItemAsync(item);
    }

    private async void LibraryGrid_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is not VirtualKey.Enter)
        {
            return;
        }

        var item = LibraryGrid.SelectedItems.OfType<LibraryTileItem>().FirstOrDefault()
            ?? LibraryGrid.SelectedItem as LibraryTileItem;
        if (item is not null)
        {
            await ViewModel.OpenLibraryItemAsync(item);
            e.Handled = true;
        }
    }

    private async void Root_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var updateKind = e.GetCurrentPoint(Root).Properties.PointerUpdateKind;
        switch (updateKind)
        {
            case PointerUpdateKind.XButton1Released:
                if (ViewModel.BackCommand.CanExecute(null))
                {
                    await ViewModel.BackCommand.ExecuteAsync(null);
                    e.Handled = true;
                }

                break;
            case PointerUpdateKind.XButton2Released:
                if (ViewModel.ForwardCommand.CanExecute(null))
                {
                    await ViewModel.ForwardCommand.ExecuteAsync(null);
                    e.Handled = true;
                }

                break;
        }
    }

    private void RecursiveModeToggle_Changed(object sender, RoutedEventArgs e)
    {
        var includeSubfolders = TitleBarRecursiveModeToggle.IsChecked == true;
        if (ViewModel.IncludeSubfolders != includeSubfolders)
        {
            ViewModel.IncludeSubfolders = includeSubfolders;
        }
    }

    private async void SortByNameAscending_Click(object sender, RoutedEventArgs e) =>
        await ChangeSortFromMenuAsync(new SortState(SortKey.Name, SortDirection.Asc));

    private async void SortByNameDescending_Click(object sender, RoutedEventArgs e) =>
        await ChangeSortFromMenuAsync(new SortState(SortKey.Name, SortDirection.Desc));

    private async void SortByModifiedAtAscending_Click(object sender, RoutedEventArgs e) =>
        await ChangeSortFromMenuAsync(new SortState(SortKey.ModifiedAt, SortDirection.Asc));

    private async void SortByModifiedAtDescending_Click(object sender, RoutedEventArgs e) =>
        await ChangeSortFromMenuAsync(new SortState(SortKey.ModifiedAt, SortDirection.Desc));

    private async Task ChangeSortFromMenuAsync(SortState sort)
    {
        try
        {
            await ViewModel.ChangeSortAsync(sort);
        }
        catch (Exception ex)
        {
            App.Logger.Error(ex, $"Change sort from menu failed. Sort={sort.Key}/{sort.Direction}");
        }
    }

    private void LibraryTile_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(LibraryGrid);
        if (point.Properties.IsLeftButtonPressed
            && sender is FrameworkElement { DataContext: LibraryTileItem { IsFolder: false } source })
        {
            ClearPointerDrag();
            pointerDragSource = source;
            pointerDragStartPosition = point.Position;
            pointerDragPointer = e.Pointer;
            pointerDragStarted = false;
        }
        else
        {
            ClearPointerDrag();
        }
    }

    private void LibraryTile_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: LibraryTileItem { IsFolder: false } item } tile)
        {
            return;
        }

        if (!LibraryGrid.SelectedItems.OfType<LibraryTileItem>().Any(selected => PathEquals(selected.Path, item.Path)))
        {
            LibraryGrid.SelectedItems.Clear();
            librarySelectionOrder.Clear();
            LibraryGrid.SelectedItems.Add(item);
        }

        LibraryImageContextFlyout.ShowAt(tile, e.GetPosition(tile));
        e.Handled = true;
    }

    private void LibraryGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (pointerDragSource is null || !IsActiveDragPointer(e.Pointer))
        {
            return;
        }

        var position = e.GetCurrentPoint(LibraryGrid).Position;
        if (!pointerDragStarted)
        {
            if (Math.Abs(position.X - pointerDragStartPosition.X) < PointerDragThreshold
                && Math.Abs(position.Y - pointerDragStartPosition.Y) < PointerDragThreshold)
            {
                return;
            }

            var dragItems = DragItemsFor(pointerDragSource);
            if (dragItems.Count == 0)
            {
                ClearPointerDrag();
                return;
            }

            if (!TryCaptureLibraryDragPointer(pointerDragSource, e.Pointer))
            {
                ClearPointerDrag();
                return;
            }

            pointerDragItems = dragItems;
            ViewModel.BeginImageDrag(dragItems);
            pointerDragStarted = true;
        }

        UpdateDragPreview(position);
        SetDropRenameTarget(DropRenameTargetAt(position));
        e.Handled = true;
    }

    private async void LibraryGrid_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (pointerDragSource is null || !IsActiveDragPointer(e.Pointer))
        {
            return;
        }

        var source = pointerDragSource;
        var wasDrag = pointerDragStarted;
        var target = currentDropRenameTarget
            ?? DropRenameTargetAt(e.GetCurrentPoint(LibraryGrid).Position)
            ?? FindDataContext<LibraryTileItem>(e.OriginalSource);
        ClearPointerDrag();

        if (wasDrag
            && target is { IsFolder: false }
            && !PathEquals(source.Path, target.Path))
        {
            await ViewModel.DropDraggedImagesOnAsync(target);
            e.Handled = true;
        }
    }

    private void LibraryGrid_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (!IsActiveDragPointer(e.Pointer))
        {
            return;
        }

        ClearPointerDrag();
        e.Handled = true;
    }

    private void LibraryGrid_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (!IsActiveDragPointer(e.Pointer))
        {
            return;
        }

        ClearPointerDrag(releaseCapture: false);
    }

    private IReadOnlyList<LibraryTileItem> DragItemsFor(LibraryTileItem source)
    {
        var orderedSelection = OrderedSelectedLibraryItems();
        var isDraggingSelectedItem = orderedSelection.Any(item => PathEquals(item.Path, source.Path));
        IEnumerable<LibraryTileItem> dragCandidates = orderedSelection.Count > 0 && isDraggingSelectedItem
            ? orderedSelection
            : [source];

        return dragCandidates.Where(item => !item.IsFolder).ToList();
    }

    private bool TryCaptureLibraryDragPointer(LibraryTileItem source, Pointer pointer)
    {
        if (pointerDragCaptureElement is not null)
        {
            return true;
        }

        if (LibraryGrid.CapturePointer(pointer))
        {
            pointerDragCaptureElement = LibraryGrid;
            return true;
        }

        App.Logger.Error(
            new InvalidOperationException("CapturePointer returned false."),
            $"Capture library drag pointer failed. Source={source.Name}; Path={source.Path}");
        return false;
    }

    private LibraryTileItem? DropRenameTargetAt(FoundationPoint position)
    {
        foreach (var target in ViewModel.LibraryItems.Where(item => !item.IsFolder))
        {
            if (pointerDragSource is null || PathEquals(pointerDragSource.Path, target.Path))
            {
                continue;
            }

            if (LibraryGrid.ContainerFromItem(target) is not FrameworkElement container)
            {
                continue;
            }

            var topLeft = container.TransformToVisual(LibraryGrid).TransformPoint(new FoundationPoint(0, 0));
            if (position.X >= topLeft.X
                && position.X <= topLeft.X + container.ActualWidth
                && position.Y >= topLeft.Y
                && position.Y <= topLeft.Y + container.ActualHeight)
            {
                return target;
            }
        }

        return null;
    }

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

    private bool IsActiveDragPointer(Pointer pointer) =>
        pointerDragPointer is not null && pointerDragPointer.PointerId == pointer.PointerId;

    private void ClearPointerDrag(bool releaseCapture = true)
    {
        var capturedElement = pointerDragCaptureElement;
        var capturedPointer = pointerDragPointer;
        SetDropRenameTarget(null);
        pointerDragSource = null;
        pointerDragStarted = false;
        pointerDragItems = [];
        pointerDragPointer = null;
        pointerDragCaptureElement = null;
        HideDragPreview();

        if (releaseCapture && capturedElement is not null && capturedPointer is not null)
        {
            try
            {
                capturedElement.ReleasePointerCapture(capturedPointer);
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "Release library drag pointer capture failed.");
            }
        }
    }

    private void UpdateDragPreview(FoundationPoint position)
    {
        if (!pointerDragStarted || pointerDragItems.Count == 0)
        {
            HideDragPreview();
            return;
        }

        LibraryDragPreviewText.Text = pointerDragItems.Count == 1
            ? $"拖曳 1 張：{pointerDragItems[0].Name}"
            : $"拖曳 {pointerDragItems.Count} 張：{pointerDragItems[0].Name}";
        LibraryDragPreviewOverlay.Visibility = Visibility.Visible;

        const double offset = 16;
        var maxX = Math.Max(0, LibraryGrid.ActualWidth - LibraryDragPreviewOverlay.ActualWidth - offset);
        var maxY = Math.Max(0, LibraryGrid.ActualHeight - LibraryDragPreviewOverlay.ActualHeight - offset);
        LibraryDragPreviewTransform.X = Math.Clamp(position.X + offset, 0, maxX);
        LibraryDragPreviewTransform.Y = Math.Clamp(position.Y + offset, 0, maxY);
    }

    private void HideDragPreview()
    {
        LibraryDragPreviewOverlay.Visibility = Visibility.Collapsed;
        LibraryDragPreviewText.Text = string.Empty;
        LibraryDragPreviewTransform.X = 0;
        LibraryDragPreviewTransform.Y = 0;
    }

    private async void ThumbnailSizeSlider_CommitValue(object sender, RoutedEventArgs e)
    {
        await CommitThumbnailSizeSliderValueAsync();
    }

    private async void ThumbnailSizeSlider_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is not VirtualKey.Left
            and not VirtualKey.Right
            and not VirtualKey.Home
            and not VirtualKey.End
            and not VirtualKey.PageDown
            and not VirtualKey.PageUp)
        {
            return;
        }

        await CommitThumbnailSizeSliderValueAsync();
    }

    private async Task CommitThumbnailSizeSliderValueAsync()
    {
        var normalizedSize = SettingsRules.NormalizeThumbnailSize(ThumbnailSizeSlider.Value);
        if (!initialized || ViewModel.ThumbnailSize == normalizedSize)
        {
            return;
        }

        await ViewModel.ChangeThumbnailSizeAsync(normalizedSize);
        QueueVisibleThumbnailLoads();
    }

    private void LibraryGrid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.Item is not LibraryTileItem item)
        {
            return;
        }

        if (args.ItemContainer is not null)
        {
            AutomationProperties.SetAutomationId(args.ItemContainer, $"{item.AutomationId}_Container");
            AutomationProperties.SetName(args.ItemContainer, item.AutomationName);
        }

        if (args.InRecycleQueue)
        {
            ViewModel.CancelThumbnailLoad(item);
            return;
        }

        QueueThumbnailLoad(item);
    }

    private void LibraryTile_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: LibraryTileItem item })
        {
            QueueThumbnailLoad(item);
        }
    }

    private void LibraryTile_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: LibraryTileItem item })
        {
            ViewModel.CancelThumbnailLoad(item);
        }
    }

    private void QueueVisibleThumbnailLoads()
    {
        foreach (var item in ViewModel.LibraryItems)
        {
            if (LibraryGrid.ContainerFromItem(item) is not null)
            {
                QueueThumbnailLoad(item);
            }
        }
    }

    private void QueueThumbnailLoad(LibraryTileItem item)
    {
        if (!DispatcherQueue.TryEnqueue(() => _ = ViewModel.LoadThumbnailAsync(item)))
        {
            App.Logger.Error(
                new InvalidOperationException("Failed to enqueue thumbnail load."),
                $"Queue thumbnail load failed. Name={item.Name}; Path={item.Path}");
        }
    }

    private async Task<string?> ChooseFolderAsync()
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary
        };
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle);
        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    private async Task<bool> ConfirmAsync(string message, string title, string primaryButtonText)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = message,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task<bool> ConfirmDropRenameAsync(DropRenamePreview preview)
    {
        var content = CreateDropRenamePreviewContent(preview);
        AutomationProperties.SetName(content, "拖放重新命名預覽");

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "確認拖放重新命名",
            Content = content,
            PrimaryButtonText = "套用重新命名",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private static UIElement CreateDropRenamePreviewContent(DropRenamePreview preview)
    {
        var panel = new StackPanel
        {
            MaxWidth = 560,
            Spacing = 12
        };

        panel.Children.Add(new TextBlock
        {
            Text = $"將重新命名 {preview.RenameCount} 個，略過 {preview.SkippedCount} 個。",
            TextWrapping = TextWrapping.Wrap,
            Style = TryGetTextStyle("BodyStrongTextBlockStyle")
        });

        var rows = new StackPanel { Spacing = 8 };
        foreach (var item in preview.Items.Take(12))
        {
            rows.Children.Add(CreateDropRenamePreviewRow(item));
        }

        if (preview.Items.Count > 12)
        {
            rows.Children.Add(new TextBlock
            {
                Text = $"另有 {preview.Items.Count - 12} 個項目。",
                TextWrapping = TextWrapping.Wrap,
                Foreground = TryGetBrush("TextFillColorSecondaryBrush"),
                Style = TryGetTextStyle("CaptionTextBlockStyle")
            });
        }

        panel.Children.Add(rows);

        return new ScrollViewer
        {
            MaxHeight = 360,
            Content = panel
        };
    }

    private static UIElement CreateDropRenamePreviewRow(DropRenamePreviewItem item)
    {
        var row = new Grid
        {
            ColumnSpacing = 8,
            Padding = new Thickness(0, 4, 0, 4)
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var icon = new FontIcon
        {
            VerticalAlignment = VerticalAlignment.Center,
            Glyph = item.WillRename ? "\uE8FB" : "\uE711",
            FontFamily = TryGetFontFamily("SymbolThemeFontFamily")
        };
        Grid.SetColumn(icon, 0);
        row.Children.Add(icon);

        var text = new TextBlock
        {
            Text = item.WillRename
                ? $"{item.SourceName} -> {item.TargetName}"
                : $"{item.SourceName} ({DropRenameReasonText(item.Reason)})",
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.WrapWholeWords
        };
        Grid.SetColumn(text, 1);
        row.Children.Add(text);

        return row;
    }

    private static string DropRenameReasonText(string? reason) =>
        reason switch
        {
            "already_target_sequence" => "已是目標序列名稱",
            _ => reason ?? "略過"
        };

    private static Style? TryGetTextStyle(string resourceKey) =>
        Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(resourceKey, out var resource)
            ? resource as Style
            : null;

    private static Brush? TryGetBrush(string resourceKey) =>
        Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(resourceKey, out var resource)
            ? resource as Brush
            : null;

    private static FontFamily? TryGetFontFamily(string resourceKey) =>
        Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(resourceKey, out var resource)
            ? resource as FontFamily
            : null;

    private async Task<string?> RequestRenameAsync(ImageListItem image)
    {
        var input = new TextBox
        {
            Header = "新檔名",
            Text = image.Name,
            PlaceholderText = "輸入新的檔案名稱",
            SelectionStart = 0,
            SelectionLength = Path.GetFileNameWithoutExtension(image.Name).Length,
            MinWidth = 320
        };
        AutomationProperties.SetName(input, "新檔名");

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "重新命名選取的圖片",
            Content = input,
            PrimaryButtonText = "重新命名",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary
            ? input.Text
            : null;
    }

    private static void OpenImageViewer(ImageSequenceSnapshot snapshot)
    {
        var window = new ImageViewerWindow(snapshot);
        WindowForeground.ActivateOwnedWindow(App.Window, window);
    }

    private static T? FindDataContext<T>(object originalSource)
        where T : class
    {
        var current = originalSource as DependencyObject;
        while (current is not null)
        {
            if (current is FrameworkElement { DataContext: T value })
            {
                return value;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private IReadOnlyList<LibraryTileItem> OrderedSelectedLibraryItems()
    {
        var selectedItems = LibraryGrid.SelectedItems.OfType<LibraryTileItem>().ToList();
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

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static string PathKey(string path) => Path.GetFullPath(path);

    private static bool PathEquals(string left, string right) =>
        PathComparer.Equals(PathKey(left), PathKey(right));

    private static string FolderSegmentFromPath(string? path, string fallback)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return fallback;
        }

        try
        {
            var normalized = Path.GetFullPath(path);
            var trimmed = normalized.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var name = Path.GetFileName(trimmed);
            return string.IsNullOrWhiteSpace(name) ? normalized : name;
        }
        catch (Exception ex)
        {
            App.Logger.Error(ex, $"Folder segment lookup failed. Path={path}; Fallback={fallback}");
            return path;
        }
    }

    private async void FolderTree_Expanding(TreeView sender, TreeViewExpandingEventArgs args)
    {
        if (args.Item is FolderTreeItem node)
        {
            await ViewModel.LoadFolderChildrenOnDemandAsync(node);
        }
    }

    private sealed class WinUIDialogService : IDialogService
    {
        private readonly MainPage page;
        public WinUIDialogService(MainPage page) => this.page = page;
        public Task<string?> ChooseFolderAsync() => page.ChooseFolderAsync();
        public Task<bool> ConfirmAsync(string message, string title, string confirmButtonText) => page.ConfirmAsync(message, title, confirmButtonText);
        public Task<bool> ConfirmDropRenameAsync(DropRenamePreview preview) => page.ConfirmDropRenameAsync(preview);
        public Task<string?> RequestRenameAsync(ImageListItem item) => page.RequestRenameAsync(item);
    }

    private sealed class WinUINavigationService : INavigationService
    {
        private readonly MainPage page;
        public WinUINavigationService(MainPage page) => this.page = page;
        public void OpenImageViewer(ImageSequenceSnapshot snapshot) => MainPage.OpenImageViewer(snapshot);
    }

    private sealed class WinUIDispatcherService : IDispatcherService
    {
        private readonly Microsoft.UI.Dispatching.DispatcherQueue queue;
        public WinUIDispatcherService(Microsoft.UI.Dispatching.DispatcherQueue queue) => this.queue = queue;
        public bool HasUiThreadAccess => queue.HasThreadAccess;
        public bool TryEnqueue(Action action) => queue.TryEnqueue(() => action());
    }
}
