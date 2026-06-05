using ImageViewerWin.ViewModels;
using Microsoft.UI.Xaml;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Graphics;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ImageViewerWin;

/// <summary>
/// The application window. This hosts a Frame that displays pages. Add your
/// UI and logic to MainPage.xaml / MainPage.xaml.cs instead of here so you
/// can use Page features such as navigation events and the Loaded lifecycle.
/// </summary>
public sealed partial class MainWindow : Window
{
    private MainPageViewModel? titleBarViewModel;

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hWnd);

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        TitleBarLayout.UseTallCaptionButtonHeight(AppWindow);
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Title = "圖片瀏覽器";
        ResizeToLogicalSize(1220, 820);

        // Navigate the root frame to the main page on startup.
        RootFrame.Navigate(typeof(MainPage));
        ConnectTitleBarCommands();
    }

    private void ConnectTitleBarCommands()
    {
        if (RootFrame.Content is not MainPage mainPage)
        {
            return;
        }

        titleBarViewModel = mainPage.ViewModel;
        titleBarViewModel.PropertyChanged += OnTitleBarViewModelPropertyChanged;

        TitleBarBackButton.Command = titleBarViewModel.BackCommand;
        TitleBarForwardButton.Command = titleBarViewModel.ForwardCommand;
        TitleBarOpenFolderButton.Command = titleBarViewModel.OpenFolderCommand;
        TitleBarRefreshLibraryButton.Command = titleBarViewModel.RefreshLibraryCommand;
        TitleBarSortKeyButton.Command = titleBarViewModel.ToggleSortKeyCommand;
        TitleBarSortDirectionButton.Command = titleBarViewModel.ToggleSortDirectionCommand;
        TitleBarConvertVisibleButton.Command = titleBarViewModel.ConvertVisibleCommand;
        TitleBarClearSameBasenameButton.Command = titleBarViewModel.ClearSameBasenameCommand;
        TitleBarRenameSelectedButton.Command = titleBarViewModel.RenameSelectedCommand;
        TitleBarTrashSelectedButton.Command = titleBarViewModel.TrashSelectedCommand;

        SyncTitleBarState();
    }

    private void OnTitleBarRecursiveModeChanged(object sender, RoutedEventArgs e)
    {
        if (titleBarViewModel is null)
        {
            return;
        }

        var includeSubfolders = TitleBarRecursiveModeToggle.IsChecked == true;
        if (titleBarViewModel.IncludeSubfolders != includeSubfolders)
        {
            titleBarViewModel.IncludeSubfolders = includeSubfolders;
        }
    }

    private void OnTitleBarViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainPageViewModel.SortLabel)
            or nameof(MainPageViewModel.RecursiveModeLabel)
            or nameof(MainPageViewModel.IncludeSubfolders))
        {
            SyncTitleBarState();
        }
    }

    private void SyncTitleBarState()
    {
        if (titleBarViewModel is null)
        {
            return;
        }

        TitleBarSortKeyButton.Label = titleBarViewModel.SortLabel;
        TitleBarRecursiveModeToggle.Label = titleBarViewModel.RecursiveModeLabel;
        TitleBarRecursiveModeToggle.IsChecked = titleBarViewModel.IncludeSubfolders;
    }

    private void ResizeToLogicalSize(int width, int height)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var scale = GetDpiForWindow(hwnd) / 96.0;
        AppWindow.Resize(new SizeInt32((int)(width * scale), (int)(height * scale)));
    }
}
