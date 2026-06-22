using System.Xml.Linq;
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
        Assert.Equal("PicLens", viewModel.WindowTitle);
        Assert.Equal("0 張，共 0 張", viewModel.PositionLabel);
        Assert.Equal("全螢幕", viewModel.FullScreenLabel);

        viewModel.IsFullScreen = true;

        Assert.Equal("結束全螢幕", viewModel.FullScreenLabel);
    }

    [Fact]
    public void FullScreenStateHidesViewerChrome()
    {
        var viewModel = new ImageViewerWindowViewModel();

        Assert.True(viewModel.IsChromeVisible);

        viewModel.IsFullScreen = true;

        Assert.False(viewModel.IsChromeVisible);

        viewModel.IsFullScreen = false;

        Assert.True(viewModel.IsChromeVisible);
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
            Id: "image:sample",
            Path: @"C:\Images\sample.webp",
            Name: "sample.webp",
            Extension: ".webp",
            ModifiedAtMs: 123,
            SizeBytes: 456,
            IsAnimated: true);
        var snapshot = new ImageSequenceSnapshot(
            Id: "sequence:sample",
            CreatedAtMs: 123,
            SourceFolderPath: @"C:\Images",
            IncludeSubfolders: false,
            Sort: new SortState(SortKey.Name, SortDirection.Asc),
            Images: [image],
            CurrentIndex: 0);

        var viewModel = new ImageViewerWindowViewModel(snapshot);

        Assert.Equal("PicLens - sample.webp", viewModel.WindowTitle);
        Assert.Equal("第 1 張，共 1 張", viewModel.PositionLabel);
        Assert.Equal("原生檢視器目前尚不支援播放動畫 WEBP。", viewModel.UnsupportedMessage);
    }

    [Fact]
    public void PackageManifestUsesTraditionalChineseVisibleDisplayText()
    {
        var manifestPath = Path.Combine(RepositoryRoot, "PicLens", "Package.appxmanifest");
        var manifest = XDocument.Load(manifestPath);
        XNamespace foundation = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
        XNamespace uap = "http://schemas.microsoft.com/appx/manifest/uap/windows10";

        var properties = manifest.Root?.Element(foundation + "Properties");
        var visualElements = manifest.Descendants(uap + "VisualElements").Single();

        Assert.Equal("PicLens", properties?.Element(foundation + "DisplayName")?.Value);
        Assert.Equal("PicLens", properties?.Element(foundation + "PublisherDisplayName")?.Value);
        Assert.Equal("PicLens", visualElements.Attribute("DisplayName")?.Value);
        Assert.Equal("PicLens", visualElements.Attribute("Description")?.Value);
    }

    private static ImageViewerWindowViewModel CreateSingleImageViewModel()
    {
        var image = new ImageListItem(
            Id: "image:sample",
            Path: @"C:\Images\sample.jpg",
            Name: "sample.jpg",
            Extension: ".jpg",
            ModifiedAtMs: 123,
            SizeBytes: 456);
        var snapshot = new ImageSequenceSnapshot(
            Id: "sequence:sample",
            CreatedAtMs: 123,
            SourceFolderPath: @"C:\Images",
            IncludeSubfolders: false,
            Sort: new SortState(SortKey.Name, SortDirection.Asc),
            Images: [image],
            CurrentIndex: 0);

        return new ImageViewerWindowViewModel(snapshot);
    }

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PicLens.slnx")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName
                ?? throw new InvalidOperationException("Could not find repository root.");
        }
    }
}
