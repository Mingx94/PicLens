using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using PicLens.Core.Models;
using PicLens.ViewModels;

namespace PicLens.Views;

public partial class MainView
{
    private void OpenImageViewer(ImageSequenceSnapshot snapshot)
    {
        UnsubscribePreviewViewModelEvents();
        ClearViewerImageSource();
        previewViewModel = new ImageViewerWindowViewModel(snapshot);
        previewViewModel.PropertyChanged += PreviewViewModel_PropertyChanged;
        ViewerSurface.DataContext = previewViewModel;
        isPreviewOpen = true;
        App.Logger.Info($"Inline image viewer opened. {ViewerContext()}");
        UpdateViewerImageSource();
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
        ClearViewerImageSource();
        UpdateWindowTitleForViewer();
        ViewerSurface.IsVisible = false;
        LibraryGrid.Focus();
    }

    private void PreviewViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ImageViewerWindowViewModel.CurrentImageName))
        {
            UpdateWindowTitleForViewer();
        }

        if (e.PropertyName is nameof(ImageViewerWindowViewModel.CurrentImageSourcePath)
            or nameof(ImageViewerWindowViewModel.IsImageVisible))
        {
            UpdateViewerImageSource();
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
            window.Title = isPreviewOpen && !string.IsNullOrWhiteSpace(previewViewModel.CurrentImageName)
                ? $"PicLens - {previewViewModel.CurrentImageName}"
                : "PicLens";
        }
    }

    private void UpdateViewerImageSource()
    {
        var path = previewViewModel.CurrentImageSourcePath;
        if (string.Equals(viewerBitmapPath, path, StringComparison.Ordinal))
        {
            return;
        }

        ClearViewerImageSource();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            viewerBitmap = new Bitmap(path);
            viewerBitmapPath = path;
            ViewerImage.Source = viewerBitmap;
        }
        catch (Exception ex)
        {
            App.Logger.Error(ex, $"Load viewer image failed. Path={path}");
        }
    }

    private void ClearViewerImageSource()
    {
        ViewerImage.Source = null;
        viewerBitmap?.Dispose();
        viewerBitmap = null;
        viewerBitmapPath = null;
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
}
