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
    public void ViewerFullScreenChromeBindsVisibilityToViewModel()
    {
        var xaml = ReadRepositoryFile("ImageViewerWin", "ImageViewerWindow.xaml");
        const string visibilityBinding = "Visibility=\"{x:Bind local:ImageViewerWindow.BoolToVisibility(ViewModel.IsChromeVisible), Mode=OneWay}\"";

        Assert.Contains("AutomationProperties.AutomationId=\"ViewerTitleBar\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"ViewerStatusBar\"", xaml);
        Assert.Equal(2, CountOccurrences(xaml, visibilityBinding));
    }

    [Fact]
    public void ViewerStatusBarUsesQuickConfirmationTextStyles()
    {
        var xaml = ReadRepositoryFile("ImageViewerWin", "ImageViewerWindow.xaml");
        var statusBarStart = xaml.IndexOf(
            "AutomationProperties.AutomationId=\"ViewerStatusBar\"",
            StringComparison.Ordinal);
        Assert.True(statusBarStart >= 0, "Could not find ViewerStatusBar.");

        var statusBarEnd = xaml.IndexOf("</Grid>", statusBarStart, StringComparison.Ordinal);
        Assert.True(statusBarEnd > statusBarStart, "Could not find the end of ViewerStatusBar.");

        var statusBar = xaml[statusBarStart..statusBarEnd];
        Assert.Contains("Text=\"{x:Bind ViewModel.CurrentImageName, Mode=OneWay}\"", statusBar);
        Assert.Contains("Style=\"{StaticResource BodyStrongTextBlockStyle}\"", statusBar);
        Assert.Contains("Text=\"{x:Bind ViewModel.ZoomLabel, Mode=OneWay}\"", statusBar);
        Assert.Contains("Style=\"{StaticResource CaptionTextBlockStyle}\"", statusBar);
    }

    [Fact]
    public void UnsupportedAnimatedImagePanelUsesPersistentSurfaceWithoutPulse()
    {
        var xaml = ReadRepositoryFile("ImageViewerWin", "ImageViewerWindow.xaml");
        var code = ReadRepositoryFile("ImageViewerWin", "ImageViewerWindow.xaml.cs");

        Assert.Contains("CardBackgroundFillColorDefaultBrush", xaml);
        Assert.DoesNotContain("AcrylicBackgroundFillColorBaseBrush", xaml);
        Assert.DoesNotContain("UnsupportedPulseStoryboard", xaml);
        Assert.DoesNotContain("RepeatBehavior=\"Forever\"", xaml);
        Assert.DoesNotContain("Storyboard.TargetProperty=\"Opacity\"", xaml);
        Assert.DoesNotContain("UnsupportedPulseStoryboard", code);
    }

    [Fact]
    public void ViewerRootKeyHandlingPansZoomedImageFromKeyboard()
    {
        var code = ReadRepositoryFile("ImageViewerWin", "ImageViewerWindow.xaml.cs");

        Assert.Contains("KeyboardPanStep", code);
        Assert.Contains("case VirtualKey.Up:", code);
        Assert.Contains("case VirtualKey.Down:", code);
        Assert.Contains("ViewModel.TryPanByKeyboard(0, KeyboardPanStep)", code);
        Assert.Contains("ViewModel.TryPanByKeyboard(0, -KeyboardPanStep)", code);
        Assert.Contains("ViewModel.TryPanByKeyboard(KeyboardPanStep, 0)", code);
        Assert.Contains("ViewModel.TryPanByKeyboard(-KeyboardPanStep, 0)", code);
    }

    [Fact]
    public void PreviewWindowUsesTallerSystemCaptionButtonsThanMainWindow()
    {
        var mainWindowCode = ReadRepositoryFile("ImageViewerWin", "MainWindow.xaml.cs");
        var viewerWindowCode = ReadRepositoryFile("ImageViewerWin", "ImageViewerWindow.xaml.cs");
        var titleBarLayoutCode = ReadRepositoryFile("ImageViewerWin", "TitleBarLayout.cs");

        Assert.Contains("TitleBarLayout.UseStandardCaptionButtonHeight(AppWindow);", mainWindowCode);
        Assert.Contains("TitleBarLayout.UseTallCaptionButtonHeight(AppWindow);", viewerWindowCode);
        Assert.Contains("TitleBarHeightOption.Standard", titleBarLayoutCode);
        Assert.Contains("TitleBarHeightOption.Tall", titleBarLayoutCode);
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
    public void MainShellUsesPlainTitleBar()
    {
        var shellXaml = ReadRepositoryFile("ImageViewerWin", "MainWindow.xaml");

        Assert.DoesNotContain("Subtitle=\"原生圖庫\"", shellXaml);
        Assert.DoesNotContain("<TitleBar.RightHeader>", shellXaml);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"TitleBarCommandBar\"", shellXaml);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"TitleBarOpenFolderButton\"", shellXaml);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"TitleBarRefreshLibraryButton\"", shellXaml);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"TitleBarSortKeyButton\"", shellXaml);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"TitleBarRecursiveModeToggle\"", shellXaml);
    }

    [Fact]
    public void MainPagePromotesNavigationActionsAboveFolderBlock()
    {
        var xaml = ReadRepositoryFile("ImageViewerWin", "MainPage.xaml");

        Assert.Contains("x:Name=\"TopCommandChrome\"", xaml);
        Assert.Contains("Grid.ColumnSpan=\"2\"", xaml);
        Assert.Contains("MinHeight=\"68\"", xaml);
        Assert.Contains("Padding=\"16,16,8,16\"", xaml);
        Assert.Contains("RowSpacing=\"12\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"FolderNavigationCommandBar\"", xaml);
        Assert.Contains("AutomationProperties.Name=\"全域導覽與圖庫工具列\"", xaml);
        Assert.Contains("<AppBarButton", xaml);
        Assert.Contains("x:Name=\"TitleBarBackButton\"", xaml);
        Assert.Contains("Label=\"上一頁\"", xaml);
        Assert.Contains("x:Name=\"TitleBarForwardButton\"", xaml);
        Assert.Contains("Label=\"下一頁\"", xaml);
        Assert.DoesNotContain("<Button\r\n                    x:Name=\"TitleBarBackButton\"", xaml);
        Assert.DoesNotContain("DefaultLabelPosition=\"Collapsed\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"TitleBarBackButton\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"TitleBarForwardButton\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"TitleBarRefreshLibraryButton\"", xaml);
    }

    [Fact]
    public void MainPagePromotesLibraryActionsIntoHeaderToolbar()
    {
        var xaml = ReadRepositoryFile("ImageViewerWin", "MainPage.xaml");

        Assert.DoesNotContain("AutomationProperties.AutomationId=\"LibraryHeaderGrid\"", xaml);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"LibraryPrimaryActionsPanel\"", xaml);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"LibraryFileActionsPanel\"", xaml);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"LibraryCommandScrollViewer\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"LibraryCommandBar\"", xaml);
        Assert.Contains("DefaultLabelPosition=\"Right\"", xaml);
        Assert.Contains("IsDynamicOverflowEnabled=\"True\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"TitleBarOpenFolderButton\"", xaml);
        Assert.Contains("Glyph=\"&#xE838;\"", xaml);
        Assert.Contains("Label=\"選擇資料夾\"", xaml);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"SortComboBox\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"TitleBarSortMenuButton\"", xaml);
        Assert.Contains("<AppBarButton.Flyout>", xaml);
        Assert.Contains("<MenuFlyout>", xaml);
        Assert.Contains("Text=\"名稱由小到大\"", xaml);
        Assert.Contains("Click=\"SortByNameAscending_Click\"", xaml);
        Assert.Contains("Text=\"名稱由大到小\"", xaml);
        Assert.Contains("Click=\"SortByNameDescending_Click\"", xaml);
        Assert.Contains("Text=\"修改時間最舊到最新\"", xaml);
        Assert.Contains("Click=\"SortByModifiedAtAscending_Click\"", xaml);
        Assert.Contains("Text=\"修改時間最新到最舊\"", xaml);
        Assert.Contains("Click=\"SortByModifiedAtDescending_Click\"", xaml);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"TitleBarSortKeyButton\"", xaml);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"TitleBarSortDirectionButton\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"TitleBarRecursiveModeToggle\"", xaml);
        Assert.Contains("Glyph=\"&#xF89A;\"", xaml);
        Assert.Contains("<CommandBar.SecondaryCommands>", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"TitleBarConvertVisibleButton\"", xaml);
        Assert.Contains("Glyph=\"&#xEE71;\"", xaml);
        Assert.Contains("Label=\"將目前顯示項目轉為 JPG\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"TitleBarClearSameBasenameButton\"", xaml);
        Assert.Contains("Glyph=\"&#xE75C;\"", xaml);
        Assert.Contains("Label=\"清除同名非 JPG 檔案\"", xaml);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"TitleBarRenameSelectedButton\"", xaml);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"TitleBarTrashSelectedButton\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"SelectionCommandBar\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"SelectionRenameButton\"", xaml);
        Assert.Contains("Label=\"重新命名\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"SelectionTrashButton\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"SelectionConvertButton\"", xaml);
        Assert.Contains("Label=\"轉成 JPG\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"SelectionClearButton\"", xaml);
        Assert.Contains("Label=\"移至回收筒\"", xaml);
        Assert.DoesNotContain("Text=\"圖庫\"", xaml);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"LibrarySearchBox\"", xaml);
        Assert.DoesNotContain("<AutoSuggestBox", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"ThumbnailSizeSlider\"", xaml);
    }

    [Fact]
    public void MainPageNavigationButtonsExposeToolTips()
    {
        var xaml = ReadRepositoryFile("ImageViewerWin", "MainPage.xaml");

        Assert.Contains("ToolTipService.ToolTip=\"返回上一個資料夾\"", xaml);
        Assert.Contains("ToolTipService.ToolTip=\"前進到下一個資料夾\"", xaml);
        Assert.Contains("ToolTipService.ToolTip=\"重新整理圖庫\"", xaml);
    }

    [Fact]
    public void MainPageToolbarCommandButtonsExposeToolTips()
    {
        var xaml = ReadRepositoryFile("ImageViewerWin", "MainPage.xaml");

        Assert.Contains("ToolTipService.ToolTip=\"選擇資料夾\"", xaml);
        Assert.Contains("ToolTipService.ToolTip=\"排序\"", xaml);
        Assert.Contains("ToolTipService.ToolTip=\"包含或排除子資料夾\"", xaml);
        Assert.Contains("ToolTipService.ToolTip=\"將目前顯示項目轉為 JPG\"", xaml);
        Assert.Contains("ToolTipService.ToolTip=\"清除同名非 JPG 檔案\"", xaml);
        Assert.Contains("ToolTipService.ToolTip=\"重新命名選取的圖片\"", xaml);
        Assert.Contains("ToolTipService.ToolTip=\"將選取的圖片移至回收筒\"", xaml);
        Assert.Contains("ToolTipService.ToolTip=\"將選取的圖片轉成 JPG\"", xaml);
        Assert.Contains("ToolTipService.ToolTip=\"清除選取\"", xaml);
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

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var startIndex = 0;
        while (true)
        {
            var index = source.IndexOf(value, startIndex, StringComparison.Ordinal);
            if (index < 0)
            {
                return count;
            }

            count++;
            startIndex = index + value.Length;
        }
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
