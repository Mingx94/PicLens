using Microsoft.UI.Xaml;
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

        RootFrame.Navigate(typeof(MainPage));
    }

    private void ResizeToLogicalSize(int width, int height)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var scale = GetDpiForWindow(hwnd) / 96.0;
        AppWindow.Resize(new SizeInt32((int)(width * scale), (int)(height * scale)));
    }
}
