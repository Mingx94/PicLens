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
    public void SecondaryViewerUsesWinUiTitleBarShell()
    {
        var xaml = ReadRepositoryFile("ImageViewerWin", "ImageViewerWindow.xaml");
        var code = ReadRepositoryFile("ImageViewerWin", "ImageViewerWindow.xaml.cs");

        Assert.Contains("x:Name=\"ViewerTitleBar\"", xaml);
        Assert.Contains("Title=\"{x:Bind ViewModel.CurrentImageName, Mode=OneWay}\"", xaml);
        Assert.Contains("Subtitle=\"{x:Bind ViewModel.PositionLabel, Mode=OneWay}\"", xaml);
        Assert.Contains("<TitleBar.RightHeader>", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"ViewerTitleCommandBar\"", xaml);
        Assert.Contains("ExtendsContentIntoTitleBar = true;", code);
        Assert.Contains("SetTitleBar(ViewerTitleBar);", code);
    }

    [Fact]
    public void CustomTitleBarsUseTallSystemCaptionButtons()
    {
        var mainWindowCode = ReadRepositoryFile("ImageViewerWin", "MainWindow.xaml.cs");
        var viewerWindowCode = ReadRepositoryFile("ImageViewerWin", "ImageViewerWindow.xaml.cs");
        var titleBarLayoutCode = ReadRepositoryFile("ImageViewerWin", "TitleBarLayout.cs");

        Assert.Contains("TitleBarLayout.UseTallCaptionButtonHeight(AppWindow);", mainWindowCode);
        Assert.Contains("TitleBarLayout.UseTallCaptionButtonHeight(AppWindow);", viewerWindowCode);
        Assert.Contains("appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;", titleBarLayoutCode);
    }

    [Fact]
    public void MainShellUsesTraditionalChineseWindowTitle()
    {
        var xaml = ReadRepositoryFile("ImageViewerWin", "MainWindow.xaml");
        var code = ReadRepositoryFile("ImageViewerWin", "MainWindow.xaml.cs");

        Assert.Contains("Title=\"圖片瀏覽器\"", xaml);
        Assert.Contains("x:Name=\"AppTitleBar\"", xaml);
        Assert.Contains("AppWindow.Title = \"圖片瀏覽器\";", code);
    }

    [Fact]
    public void MainShellPromotesLibraryActionsIntoTitleBar()
    {
        var shellXaml = ReadRepositoryFile("ImageViewerWin", "MainWindow.xaml");
        var pageXaml = ReadRepositoryFile("ImageViewerWin", "MainPage.xaml");
        var code = ReadRepositoryFile("ImageViewerWin", "MainWindow.xaml.cs");

        Assert.Contains("Subtitle=\"原生圖庫\"", shellXaml);
        Assert.Contains("<TitleBar.RightHeader>", shellXaml);
        Assert.Contains("AutomationProperties.AutomationId=\"TitleBarCommandBar\"", shellXaml);
        Assert.Contains("AutomationProperties.AutomationId=\"TitleBarOpenFolderButton\"", shellXaml);
        Assert.Contains("AutomationProperties.AutomationId=\"TitleBarRefreshLibraryButton\"", shellXaml);
        Assert.Contains("ConnectTitleBarCommands();", code);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"LibraryCommandBar\"", pageXaml);
    }

    [Fact]
    public void MainTitleBarCommandButtonsExposeToolTips()
    {
        var xaml = ReadRepositoryFile("ImageViewerWin", "MainWindow.xaml");

        Assert.Contains("ToolTipService.ToolTip=\"返回上一個資料夾\"", xaml);
        Assert.Contains("ToolTipService.ToolTip=\"前進到下一個資料夾\"", xaml);
        Assert.Contains("ToolTipService.ToolTip=\"選擇資料夾\"", xaml);
        Assert.Contains("ToolTipService.ToolTip=\"重新整理圖庫\"", xaml);
        Assert.Contains("ToolTipService.ToolTip=\"切換排序欄位\"", xaml);
        Assert.Contains("ToolTipService.ToolTip=\"切換排序方向\"", xaml);
        Assert.Contains("ToolTipService.ToolTip=\"包含或排除子資料夾\"", xaml);
        Assert.Contains("ToolTipService.ToolTip=\"將目前顯示項目轉為 JPG\"", xaml);
        Assert.Contains("ToolTipService.ToolTip=\"清除同名非 JPG 檔案\"", xaml);
        Assert.Contains("ToolTipService.ToolTip=\"重新命名選取的圖片\"", xaml);
        Assert.Contains("ToolTipService.ToolTip=\"將選取項目移至回收筒\"", xaml);
    }

    [Fact]
    public void ViewerTitleBarCommandButtonsExposeToolTips()
    {
        var xaml = ReadRepositoryFile("ImageViewerWin", "ImageViewerWindow.xaml");

        Assert.Contains("ToolTipService.ToolTip=\"上一張圖片\"", xaml);
        Assert.Contains("ToolTipService.ToolTip=\"下一張圖片\"", xaml);
        Assert.Contains("ToolTipService.ToolTip=\"縮小\"", xaml);
        Assert.Contains("ToolTipService.ToolTip=\"重設縮放\"", xaml);
        Assert.Contains("ToolTipService.ToolTip=\"放大\"", xaml);
        Assert.Contains("ToolTipService.ToolTip=\"切換全螢幕\"", xaml);
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
