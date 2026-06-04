using ImageViewerWin.Core.Models;
using ImageViewerWin.Infrastructure.Services;
using ImageViewerWin.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using Windows.System;

namespace ImageViewerWin;

/// <summary>
/// The main content page displayed inside the application window.
/// </summary>
public sealed partial class MainPage : Page
{
    private readonly List<LibraryTileItem> librarySelectionOrder = [];
    private bool initialized;

    public MainPage()
    {
        var settingsStore = new JsonSettingsStore();
        ViewModel = new MainPageViewModel(
            settingsStore,
            new FavoriteFolderService(settingsStore),
            new FolderScanner(),
            new FileOperationService(),
            ChooseFolderAsync,
            ConfirmAsync,
            RequestRenameAsync,
            OpenImageViewer);

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

    private async void LibraryGrid_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (FindDataContext<LibraryTileItem>(e.OriginalSource) is { } item)
        {
            await ViewModel.OpenLibraryItemAsync(item);
            e.Handled = true;
        }
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

    private void LibraryGrid_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        var eventItems = e.Items.OfType<LibraryTileItem>().ToList();
        var orderedSelection = OrderedSelectedLibraryItems();
        var isDraggingOutsideSelection = eventItems.Count == 1
            && !orderedSelection.Any(item => PathEquals(item.Path, eventItems[0].Path));
        var dragCandidates = orderedSelection.Count > 0 && !isDraggingOutsideSelection
            ? orderedSelection
            : eventItems;
        var items = dragCandidates.Where(item => !item.IsFolder).ToList();
        if (items.Count == 0)
        {
            e.Cancel = true;
            return;
        }

        ViewModel.BeginImageDrag(items);
        e.Data.RequestedOperation = DataPackageOperation.Move;
        e.Data.SetText(string.Join(Environment.NewLine, items.Select(item => item.Path)));
    }

    private void LibraryGrid_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Move;
        e.Handled = true;
    }

    private async void LibraryGrid_Drop(object sender, DragEventArgs e)
    {
        if (FindDataContext<LibraryTileItem>(e.OriginalSource) is { IsFolder: false } target)
        {
            await ViewModel.DropDraggedImagesOnAsync(target);
            e.Handled = true;
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

    private async Task<bool> ConfirmAsync(string message, string title)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = message,
            PrimaryButtonText = "Continue",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task<string?> RequestRenameAsync(ImageListItem image)
    {
        var input = new TextBox
        {
            Text = image.Name,
            SelectionStart = 0,
            SelectionLength = Path.GetFileNameWithoutExtension(image.Name).Length,
            MinWidth = 320
        };
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Rename selected image",
            Content = input,
            PrimaryButtonText = "Rename",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary
            ? input.Text
            : null;
    }

    private static void OpenImageViewer(ImageSequenceSnapshot snapshot)
    {
        var window = new ImageViewerWindow(snapshot);
        window.Activate();
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
