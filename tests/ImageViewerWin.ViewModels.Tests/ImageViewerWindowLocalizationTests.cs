using System.Xml.Linq;
using ImageViewerWin.Core.Models;
using ImageViewerWin.ViewModels;

namespace ImageViewerWin.ViewModels.Tests;

public sealed class ImageViewerWindowLocalizationTests
{
    [Fact]
    public void EmptyViewerUsesTraditionalChineseDisplayText()
    {
        var viewModel = new ImageViewerWindowViewModel();

        Assert.Equal("尚未選取圖片", viewModel.CurrentImageName);
        Assert.Equal("圖片瀏覽器", viewModel.WindowTitle);
        Assert.Equal("0 張，共 0 張", viewModel.PositionLabel);
        Assert.Equal("全螢幕", viewModel.FullScreenLabel);

        viewModel.IsFullScreen = true;

        Assert.Equal("結束全螢幕", viewModel.FullScreenLabel);
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

        Assert.Equal("sample.webp - 圖片瀏覽器", viewModel.WindowTitle);
        Assert.Equal("第 1 張，共 1 張", viewModel.PositionLabel);
        Assert.Equal("原生檢視器目前尚不支援播放動畫 WEBP。", viewModel.UnsupportedMessage);
    }

    [Fact]
    public void SecondaryViewerXamlUsesTraditionalChineseCommandText()
    {
        var xaml = ReadRepositoryFile("ImageViewerWin", "ImageViewerWindow.xaml");

        Assert.Contains("AutomationProperties.Name=\"上一張圖片\"", xaml);
        Assert.Contains("Label=\"上一張\"", xaml);
        Assert.Contains("AutomationProperties.Name=\"下一張圖片\"", xaml);
        Assert.Contains("Label=\"下一張\"", xaml);
        Assert.Contains("AutomationProperties.Name=\"縮小\"", xaml);
        Assert.Contains("Label=\"縮小\"", xaml);
        Assert.Contains("AutomationProperties.Name=\"重設縮放\"", xaml);
        Assert.Contains("Label=\"重設縮放\"", xaml);
        Assert.Contains("AutomationProperties.Name=\"放大\"", xaml);
        Assert.Contains("Label=\"放大\"", xaml);
        Assert.Contains("Text=\"無法預覽動畫\"", xaml);
    }

    [Fact]
    public void MainShellUsesTraditionalChineseWindowTitle()
    {
        var xaml = ReadRepositoryFile("ImageViewerWin", "MainWindow.xaml");
        var code = ReadRepositoryFile("ImageViewerWin", "MainWindow.xaml.cs");

        Assert.Contains("Title=\"圖片瀏覽器\"", xaml);
        Assert.Contains("<TitleBar x:Name=\"AppTitleBar\" Title=\"圖片瀏覽器\">", xaml);
        Assert.Contains("AppWindow.Title = \"圖片瀏覽器\";", code);
    }

    [Fact]
    public void PackageManifestUsesTraditionalChineseVisibleDisplayText()
    {
        var manifestPath = Path.Combine(RepositoryRoot, "ImageViewerWin", "Package.appxmanifest");
        var manifest = XDocument.Load(manifestPath);
        XNamespace foundation = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
        XNamespace uap = "http://schemas.microsoft.com/appx/manifest/uap/windows10";

        var properties = manifest.Root?.Element(foundation + "Properties");
        var visualElements = manifest.Descendants(uap + "VisualElements").Single();

        Assert.Equal("圖片瀏覽器", properties?.Element(foundation + "DisplayName")?.Value);
        Assert.Equal("圖片瀏覽器", properties?.Element(foundation + "PublisherDisplayName")?.Value);
        Assert.Equal("圖片瀏覽器", visualElements.Attribute("DisplayName")?.Value);
        Assert.Equal("圖片瀏覽器", visualElements.Attribute("Description")?.Value);
    }

    private static string ReadRepositoryFile(params string[] pathParts) =>
        File.ReadAllText(Path.Combine([RepositoryRoot, .. pathParts]));

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ImageViewerWin.slnx")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName
                ?? throw new InvalidOperationException("Could not find repository root.");
        }
    }
}
