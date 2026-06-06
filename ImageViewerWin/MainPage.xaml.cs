using ImageViewerWin.Core.Models;
using ImageViewerWin.Core.Domain;
using ImageViewerWin.Diagnostics;
using ImageViewerWin.Infrastructure.Services;
using ImageViewerWin.ViewModels;
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

namespace ImageViewerWin;

/// <summary>
/// The main content page displayed inside the application window.
/// </summary>
public sealed partial class MainPage : Page
{
    private const double PointerDragThreshold = 8;

    private readonly List<LibraryTileItem> librarySelectionOrder = [];
    private LibraryTileItem? pointerDragSource;
    private FoundationPoint pointerDragStartPosition;
    private bool pointerDragStarted;
    private bool initialized;

    public MainPage()
    {
        var settingsStore = new JsonSettingsStore();
        ViewModel = new MainPageViewModel(
            settingsStore,
            new FolderScanner(),
            new FileOperationService(),
            new ThumbnailService(),
            ChooseFolderAsync,
            ConfirmAsync,
            RequestRenameAsync,
            OpenImageViewer,
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

    private void LibraryGrid_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (FindDataContext<LibraryTileItem>(e.OriginalSource) is { } item)
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
        if (sender is FrameworkElement { DataContext: LibraryTileItem { IsFolder: false } source })
        {
            pointerDragSource = source;
            pointerDragStartPosition = e.GetCurrentPoint(LibraryGrid).Position;
            pointerDragStarted = false;
        }
        else
        {
            ClearPointerDrag();
        }
    }

    private void LibraryGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (pointerDragSource is null || pointerDragStarted)
        {
            return;
        }

        var position = e.GetCurrentPoint(LibraryGrid).Position;
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

        ViewModel.BeginImageDrag(dragItems);
        pointerDragStarted = true;
        e.Handled = true;
    }

    private async void LibraryGrid_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (pointerDragSource is null)
        {
            return;
        }

        var source = pointerDragSource;
        var wasDrag = pointerDragStarted;
        var target = FindDataContext<LibraryTileItem>(e.OriginalSource);
        ClearPointerDrag();

        if (wasDrag
            && target is { IsFolder: false }
            && !PathEquals(source.Path, target.Path))
        {
            await ViewModel.DropDraggedImagesOnAsync(target);
            e.Handled = true;
        }
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

    private void ClearPointerDrag()
    {
        pointerDragSource = null;
        pointerDragStarted = false;
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

        if (args.InRecycleQueue)
        {
            ViewModel.CancelThumbnailLoad(item);
            return;
        }

        _ = ViewModel.LoadThumbnailAsync(item);
    }

    private void LibraryTile_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: LibraryTileItem item })
        {
            _ = ViewModel.LoadThumbnailAsync(item);
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
                _ = ViewModel.LoadThumbnailAsync(item);
            }
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
}
