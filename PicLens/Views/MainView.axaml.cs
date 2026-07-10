using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PicLens.Converters;
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

    private readonly List<LibraryTileItem> librarySelectionOrder = [];
    private readonly TranslateTransform libraryDragPreviewTransform = new();
    private readonly DispatcherTimer libraryDragAutoScrollTimer;
    private readonly DispatcherTimer thumbnailSizeCommitTimer;
    private ImageViewerWindowViewModel previewViewModel = new();
    private LibraryTileItem? pointerDragSource;
    private LibraryTileItem? currentDropRenameTarget;
    private LibraryTileItem? contextMenuItem;
    private Avalonia.Point pointerDragStartPosition;
    private Avalonia.Point libraryDragLastPosition;
    private Avalonia.Point viewerLastPointerPosition;
    private bool pointerDragStarted;
    private bool viewerIsDragging;
    private bool isPreviewOpen;
    private bool initialized;
    private bool initialLoadCompleted;
    private Bitmap? viewerBitmap;
    private string? viewerBitmapPath;
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
            runOnUiThread: async action => await Dispatcher.UIThread.InvokeAsync(action),
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
        DetachedFromVisualTree += (_, _) =>
        {
            ClearViewerImageSource();
            ImagePathConverter.ClearCache();
        };
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
            await ViewModel.NavigateToFolderAsync(node.Path, persist: false, resetFolderTreeRoot: false);
        }
    }

    private async void FolderTreeItem_Expanded(object? sender, RoutedEventArgs e)
    {
        if (e.Source is TreeViewItem { DataContext: FolderTreeItem node })
        {
            await ViewModel.LoadFolderChildrenOnDemandAsync(node);
        }
    }

    private async void LibraryTile_Tapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: LibraryTileItem { IsFolder: true } folder })
        {
            await OpenTileAsync(folder);
            e.Handled = true;
        }
        else if (sender is Control { DataContext: LibraryTileItem image })
        {
            SelectLibraryTile(image, e.KeyModifiers);
            LibraryGrid.Focus();
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

    private async void LibraryTile_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not Control { DataContext: LibraryTileItem item } control)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            await OpenTileAsync(item);
            e.Handled = true;
        }
        else if (e.Key == Key.Space && !item.IsFolder)
        {
            SelectLibraryTile(item, e.KeyModifiers);
            e.Handled = true;
        }
        else if (e.Key == Key.F10 && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && !item.IsFolder)
        {
            OpenLibraryItemContextMenu(control, item);
            e.Handled = true;
        }
    }

    private void LibraryTile_RightTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control { DataContext: LibraryTileItem item } control || item.IsFolder)
        {
            return;
        }

        OpenLibraryItemContextMenu(control, item);
        e.Handled = true;
    }

    private void OpenLibraryItemContextMenu(Control control, LibraryTileItem item)
    {
        contextMenuItem = item;
        if (SelectedLibraryTiles().All(selected => !PathEquals(selected.Path, item.Path)))
        {
            SelectLibraryTile(item, KeyModifiers.None);
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
        control.Focus();
        menu.Open(control);
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
        var ordered = OrderedSelectedLibraryItems();
        return ordered.FirstOrDefault(item => !item.IsFolder) ?? ordered.FirstOrDefault();
    }

    private async Task OpenTileAsync(LibraryTileItem item)
    {
        if (item.IsFolder)
        {
            await ViewModel.NavigateToFolderAsync(item.Path, persist: false, resetFolderTreeRoot: false);
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

        RevealPathInFileExplorer(item.Path);
    }

    private void RevealPathInFileExplorer(string path)
    {
        try
        {
            Process.Start(CreateRevealStartInfo(path));
        }
        catch (Exception ex)
        {
            App.Logger.Error(ex, $"Reveal in file manager failed. Path={path}");
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

        public async Task<bool> ConfirmDropRenameAsync(DropTargetBatchRenamePlan plan)
        {
            var owner = TopLevel.GetTopLevel(view) as Window;
            if (owner is null)
            {
                return false;
            }

            var lines = plan.Items
                .Take(12)
                .Select(item => item.ShouldSkip
                    ? $"{Path.GetFileName(item.SourcePath)}：{ReasonText(item.Reason)}"
                    : $"{Path.GetFileName(item.SourcePath)} → {Path.GetFileName(item.TargetPath)}");
            var renameCount = plan.Items.Count(item => !item.ShouldSkip);
            var skippedCount = plan.Items.Count(item => item.ShouldSkip);
            var suffix = plan.Items.Count > 12 ? $"{Environment.NewLine}另有 {plan.Items.Count - 12} 個項目..." : string.Empty;
            var message = $"將重新命名 {renameCount} 個，略過 {skippedCount} 個。{Environment.NewLine}{Environment.NewLine}{string.Join(Environment.NewLine, lines)}{suffix}";
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
            Opened += (_, _) => cancel.Focus();
            KeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    Close(false);
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
            Height = 210;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            CanResize = false;

            var input = new TextBox
            {
                Text = fileName,
                PlaceholderText = "輸入新的檔案名稱",
                MinWidth = 320
            };
            AutomationProperties.SetName(input, "新檔名");
            input.SelectionStart = 0;
            input.SelectionEnd = Path.GetFileNameWithoutExtension(fileName).Length;

            var error = new TextBlock
            {
                IsVisible = false,
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            };

            var cancel = new Button { Content = "取消" };
            var confirm = new Button { Content = "重新命名" };
            cancel.Click += (_, _) => Close(null);
            void Submit()
            {
                var validation = FileRenamePlanner.ValidateImageFileName(input.Text ?? string.Empty);
                if (!validation.IsValid)
                {
                    error.Text = validation.Reason switch
                    {
                        "empty_name" => "錯誤：請輸入檔名。",
                        "unsupported_extension" => "錯誤：請使用支援的圖片格式。",
                        _ => "錯誤：檔名包含無效字元。"
                    };
                    error.IsVisible = true;
                    AutomationProperties.SetHelpText(input, error.Text);
                    input.Focus();
                    return;
                }

                Close(input.Text);
            }

            confirm.Click += (_, _) => Submit();
            input.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    Submit();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    Close(null);
                    e.Handled = true;
                }
            };
            input.TextChanged += (_, _) =>
            {
                error.IsVisible = false;
                AutomationProperties.SetHelpText(input, null);
            };

            Content = new StackPanel
            {
                Margin = new Thickness(18),
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = "新檔名" },
                    input,
                    error,
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
        ViewModel.LibraryItems.Where(item => item.IsSelected);
}
