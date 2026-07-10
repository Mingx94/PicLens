using PicLens.Core.Models;
using PicLens.ViewModels;

namespace PicLens.ViewModels.Tests;

public sealed class ImageViewerWindowLocalizationTests
{
    [Fact]
    public void EmptyViewerUsesTraditionalChineseDisplayText()
    {
        var viewModel = new ImageViewerWindowViewModel();

        Assert.Equal("尚未選取圖片", viewModel.CurrentImageName);
    }

    [Fact]
    public void ZoomedViewerCanBePannedWithKeyboardDeltas()
    {
        var viewModel = CreateSingleImageViewModel();

        Assert.False(viewModel.TryPanByKeyboard(48, 0));
        Assert.Equal(0, viewModel.OffsetX);
        Assert.Equal(0, viewModel.OffsetY);

        viewModel.UpdateViewport(800, 600);
        viewModel.ZoomAt(400, 300, 1);

        Assert.True(viewModel.Zoom > 1);
        Assert.True(viewModel.TryPanByKeyboard(48, -48));
        Assert.Equal(48, viewModel.OffsetX, precision: 10);
        Assert.Equal(-48, viewModel.OffsetY, precision: 10);
    }

    [Fact]
    public void SelectedAnimatedImageUsesTraditionalChinesePositionTitleAndUnsupportedMessage()
    {
        var image = new ImageListItem(
            Path: @"C:\Images\sample.webp",
            Name: "sample.webp",
            Extension: ".webp",
            ModifiedAtMs: 123,
            SizeBytes: 456,
            IsAnimated: true);
        var snapshot = new ImageSequenceSnapshot(
            SourceFolderPath: @"C:\Images",
            IncludeSubfolders: false,
            Sort: new SortState(SortKey.Name, SortDirection.Asc),
            Images: [image],
            CurrentIndex: 0);

        var viewModel = new ImageViewerWindowViewModel(snapshot);

        Assert.Equal("原生檢視器目前尚不支援播放動畫 WEBP。", viewModel.UnsupportedMessage);
    }

    private static ImageViewerWindowViewModel CreateSingleImageViewModel()
    {
        var image = new ImageListItem(
            Path: @"C:\Images\sample.jpg",
            Name: "sample.jpg",
            Extension: ".jpg",
            ModifiedAtMs: 123,
            SizeBytes: 456);
        var snapshot = new ImageSequenceSnapshot(
            SourceFolderPath: @"C:\Images",
            IncludeSubfolders: false,
            Sort: new SortState(SortKey.Name, SortDirection.Asc),
            Images: [image],
            CurrentIndex: 0);

        return new ImageViewerWindowViewModel(snapshot);
    }
}
