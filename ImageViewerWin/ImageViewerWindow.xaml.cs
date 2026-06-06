using ImageViewerWin.Core.Models;
using ImageViewerWin.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Windows.System;

namespace ImageViewerWin;

public sealed partial class ImageViewerWindow : Window
{
    private const double KeyboardPanStep = 48;

    private bool _isDragging;
    private Windows.Foundation.Point _lastPointerPosition;

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hWnd);

    public ImageViewerWindow()
        : this(new ImageViewerWindowViewModel())
    {
    }

    public ImageViewerWindow(ImageSequenceSnapshot snapshot)
        : this(new ImageViewerWindowViewModel(snapshot))
    {
    }

    public ImageViewerWindow(ImageViewerWindowViewModel viewModel)
    {
        ViewModel = viewModel;
        App.Logger.Info($"ImageViewerWindow constructing. {ViewerContext()}");
        InitializeComponent();
        App.Logger.Info($"ImageViewerWindow InitializeComponent completed. {ViewerContext()}");

        ExtendsContentIntoTitleBar = true;
        TitleBarLayout.UseTallCaptionButtonHeight(AppWindow);
        SetTitleBar(ViewerTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Title = ViewModel.WindowTitle;
        ResizeToLogicalSize(1120, 760);
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Maximize();
        }

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        App.Logger.Info($"ImageViewerWindow constructed. {ViewerContext()}");
    }

    public ImageViewerWindowViewModel ViewModel { get; }

    public static Visibility BoolToVisibility(bool value) =>
        value ? Visibility.Visible : Visibility.Collapsed;

    public static BitmapImage? CreateBitmapImage(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        try
        {
            if (Uri.TryCreate(source, UriKind.Absolute, out var absoluteUri))
            {
                return new BitmapImage(absoluteUri);
            }

            return new BitmapImage(new Uri(Path.GetFullPath(source)));
        }
        catch (Exception ex)
        {
            App.Logger.Error(ex, $"Create viewer bitmap image failed. Source={source}");
            return null;
        }
    }

    private void OnRootLoaded(object sender, RoutedEventArgs e)
    {
        App.Logger.Info(
            $"ImageViewerWindow loaded. ViewportWidth={ImageSurface.ActualWidth}; ViewportHeight={ImageSurface.ActualHeight}; {ViewerContext()}");
        Root.Focus(FocusState.Programmatic);
        ViewModel.UpdateViewport(ImageSurface.ActualWidth, ImageSurface.ActualHeight);
    }

    private void OnImageSurfaceSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ViewModel.UpdateViewport(e.NewSize.Width, e.NewSize.Height);
    }

    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Left:
                if (ViewModel.TryPanByKeyboard(KeyboardPanStep, 0))
                {
                    e.Handled = true;
                    break;
                }

                if (ViewModel.PreviousCommand.CanExecute(null))
                {
                    ViewModel.PreviousCommand.Execute(null);
                }

                e.Handled = true;
                break;
            case VirtualKey.Right:
                if (ViewModel.TryPanByKeyboard(-KeyboardPanStep, 0))
                {
                    e.Handled = true;
                    break;
                }

                if (ViewModel.NextCommand.CanExecute(null))
                {
                    ViewModel.NextCommand.Execute(null);
                }

                e.Handled = true;
                break;
            case VirtualKey.Up:
                if (ViewModel.TryPanByKeyboard(0, KeyboardPanStep))
                {
                    e.Handled = true;
                }

                break;
            case VirtualKey.Down:
                if (ViewModel.TryPanByKeyboard(0, -KeyboardPanStep))
                {
                    e.Handled = true;
                }

                break;
            case VirtualKey.Escape:
                if (ViewModel.IsFullScreen)
                {
                    SetFullScreen(false);
                }
                else
                {
                    App.Logger.Info($"Image viewer closing from Escape. {ViewerContext()}");
                    Close();
                    App.Window.Activate();
                }

                e.Handled = true;
                break;
        }
    }

    private void OnToggleFullScreenClicked(object sender, RoutedEventArgs e)
    {
        SetFullScreen(!ViewModel.IsFullScreen);
    }

    private void OnImageSurfacePointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (!ViewModel.IsImageVisible)
        {
            return;
        }

        var pointer = e.GetCurrentPoint(ImageSurface);
        ViewModel.ZoomAt(pointer.Position.X, pointer.Position.Y, pointer.Properties.MouseWheelDelta);
        e.Handled = true;
    }

    private void OnImageSurfacePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!ViewModel.IsImageVisible)
        {
            return;
        }

        _isDragging = true;
        _lastPointerPosition = e.GetCurrentPoint(ImageSurface).Position;
        ImageSurface.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnImageSurfacePointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        var position = e.GetCurrentPoint(ImageSurface).Position;
        ViewModel.PanBy(position.X - _lastPointerPosition.X, position.Y - _lastPointerPosition.Y);
        _lastPointerPosition = position;
        e.Handled = true;
    }

    private void OnImageSurfacePointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        ImageSurface.ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ImageViewerWindowViewModel.WindowTitle))
        {
            App.Logger.Info($"Image viewer current image changed. {ViewerContext()}");
            AppWindow.Title = ViewModel.WindowTitle;
        }
    }

    private void SetFullScreen(bool enabled)
    {
        App.Logger.Info($"Image viewer fullscreen changed. Enabled={enabled}; {ViewerContext()}");
        AppWindow.SetPresenter(enabled
            ? AppWindowPresenterKind.FullScreen
            : AppWindowPresenterKind.Default);
        ViewModel.IsFullScreen = enabled;
        Root.Focus(FocusState.Programmatic);
    }

    private void ResizeToLogicalSize(int width, int height)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var scale = GetDpiForWindow(hwnd) / 96.0;
        AppWindow.Resize(new SizeInt32((int)(width * scale), (int)(height * scale)));
    }

    private string ViewerContext() =>
        $"CurrentIndex={ViewModel.CurrentIndex}; ImageCount={ViewModel.Snapshot.Images.Count}; CurrentImage={ViewModel.CurrentImageName}; SourceFolderPath={ViewModel.Snapshot.SourceFolderPath}; IncludeSubfolders={ViewModel.Snapshot.IncludeSubfolders}; Sort={ViewModel.Snapshot.Sort.Key}/{ViewModel.Snapshot.Sort.Direction}";
}
