using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageViewerWin.Core.Domain;
using ImageViewerWin.Core.Models;

namespace ImageViewerWin.ViewModels;

public partial class ImageViewerWindowViewModel : ObservableObject
{
    private double _viewportHeight;
    private double _viewportWidth;

    public ImageViewerWindowViewModel()
        : this(CreateEmptySnapshot())
    {
    }

    public ImageViewerWindowViewModel(ImageSequenceSnapshot snapshot)
    {
        Snapshot = snapshot;
        CurrentIndex = IsValidIndex(snapshot.CurrentIndex) ? snapshot.CurrentIndex : -1;
        ResetZoomState();
    }

    public ImageSequenceSnapshot Snapshot { get; }

    [ObservableProperty]
    public partial int CurrentIndex { get; set; }

    [ObservableProperty]
    public partial double Zoom { get; set; }

    [ObservableProperty]
    public partial double OffsetX { get; set; }

    [ObservableProperty]
    public partial double OffsetY { get; set; }

    [ObservableProperty]
    public partial bool IsFullScreen { get; set; }

    public ImageListItem? CurrentImage => IsValidIndex(CurrentIndex) ? Snapshot.Images[CurrentIndex] : null;

    public string? CurrentImageSourcePath
    {
        get
        {
            if (!IsImageVisible || CurrentImage is null)
            {
                return null;
            }

            return string.IsNullOrWhiteSpace(CurrentImage.ImageUrl)
                ? CurrentImage.Path
                : CurrentImage.ImageUrl;
        }
    }

    public bool HasImages => Snapshot.Images.Count > 0;

    public bool IsImageVisible => CurrentImage is not null && !IsUnsupportedAnimatedImage;

    public bool IsUnsupportedAnimatedImage => CurrentImage?.IsAnimated == true;

    public bool CanGoPrevious => CurrentIndex > 0;

    public bool CanGoNext => CurrentIndex >= 0 && CurrentIndex < Snapshot.Images.Count - 1;

    public bool CanZoomIn => IsImageVisible && Zoom < ZoomMath.MaxZoom;

    public bool CanZoomOut => IsImageVisible && Zoom > ZoomMath.MinZoom;

    public string CurrentImageName => CurrentImage?.Name ?? "No image selected";

    public string WindowTitle => CurrentImage is null
        ? "ImageViewer"
        : $"{CurrentImage.Name} - ImageViewer";

    public string PositionLabel => HasImages && CurrentIndex >= 0
        ? $"{CurrentIndex + 1} of {Snapshot.Images.Count}"
        : "0 of 0";

    public string ZoomLabel => $"{Zoom * 100:0}%";

    public string FullScreenLabel => IsFullScreen ? "Exit fullscreen" : "Fullscreen";

    public string UnsupportedMessage => CurrentImage is null
        ? "No image is selected."
        : $"Animated {CurrentImage.Extension.ToUpperInvariant()} playback is not supported in the native viewer yet.";

    public void UpdateViewport(double width, double height)
    {
        _viewportWidth = Math.Max(0, width);
        _viewportHeight = Math.Max(0, height);
    }

    public void ZoomAt(double pointerX, double pointerY, int delta)
    {
        if (!IsImageVisible || delta == 0)
        {
            return;
        }

        var next = ZoomMath.ZoomAtPoint(new ZoomAtPointInput(
            Zoom: Zoom,
            Offset: new Point(OffsetX, OffsetY),
            ViewportCenter: new Point(_viewportWidth / 2, _viewportHeight / 2),
            Pointer: new Point(pointerX, pointerY),
            Delta: delta));

        ApplyZoomState(next);
    }

    public void PanBy(double deltaX, double deltaY)
    {
        if (!IsImageVisible || Zoom <= 1)
        {
            return;
        }

        OffsetX += deltaX;
        OffsetY += deltaY;
    }

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private void Previous()
    {
        CurrentIndex -= 1;
        ResetZoomState();
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void Next()
    {
        CurrentIndex += 1;
        ResetZoomState();
    }

    [RelayCommand(CanExecute = nameof(CanZoomIn))]
    private void ZoomIn()
    {
        ZoomAt(_viewportWidth / 2, _viewportHeight / 2, 1);
    }

    [RelayCommand(CanExecute = nameof(CanZoomOut))]
    private void ZoomOut()
    {
        ZoomAt(_viewportWidth / 2, _viewportHeight / 2, -1);
    }

    [RelayCommand]
    private void ResetZoom()
    {
        ResetZoomState();
    }

    partial void OnCurrentIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CurrentImage));
        OnPropertyChanged(nameof(CurrentImageSourcePath));
        OnPropertyChanged(nameof(IsImageVisible));
        OnPropertyChanged(nameof(IsUnsupportedAnimatedImage));
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanZoomIn));
        OnPropertyChanged(nameof(CanZoomOut));
        OnPropertyChanged(nameof(CurrentImageName));
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(PositionLabel));
        OnPropertyChanged(nameof(UnsupportedMessage));
        PreviousCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
        ZoomInCommand.NotifyCanExecuteChanged();
        ZoomOutCommand.NotifyCanExecuteChanged();
    }

    partial void OnZoomChanged(double value)
    {
        OnPropertyChanged(nameof(ZoomLabel));
        OnPropertyChanged(nameof(CanZoomIn));
        OnPropertyChanged(nameof(CanZoomOut));
        ZoomInCommand.NotifyCanExecuteChanged();
        ZoomOutCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsFullScreenChanged(bool value)
    {
        OnPropertyChanged(nameof(FullScreenLabel));
    }

    private static ImageSequenceSnapshot CreateEmptySnapshot() =>
        new(
            Id: "sequence:empty",
            CreatedAtMs: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            SourceFolderPath: string.Empty,
            IncludeSubfolders: false,
            Sort: new SortState(SortKey.Name, SortDirection.Asc),
            Images: [],
            CurrentIndex: -1);

    private void ApplyZoomState(ZoomState state)
    {
        Zoom = state.Zoom;
        OffsetX = state.Offset.X;
        OffsetY = state.Offset.Y;
    }

    private bool IsValidIndex(int index) => index >= 0 && index < Snapshot.Images.Count;

    private void ResetZoomState()
    {
        ApplyZoomState(ZoomMath.ResetZoomState());
    }
}
