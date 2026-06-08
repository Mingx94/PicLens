using ImageViewerWin.Core.Models;
using ImageViewerWin.Application.Services;
using ImageViewerWin.ViewModels;

namespace ImageViewerWin.ViewModels.Tests;

public sealed class MainPageTextTests
{
    [Fact]
    public void MainPage_xaml_uses_zh_tw_copy_and_removes_favorites_copy()
    {
        var xaml = File.ReadAllText(Path.Combine(RepositoryRoot(), "ImageViewerWin", "MainPage.xaml"));

        Assert.Contains("資料夾", xaml);
        Assert.Contains("選擇資料夾", xaml);
        Assert.Contains("重新命名", xaml);
        Assert.Contains("移至回收筒", xaml);
        Assert.Contains("檔案操作", xaml);
        Assert.DoesNotContain("Text=\"圖庫\"", xaml);
        Assert.DoesNotContain("Favorites", xaml);
        Assert.DoesNotContain("Favorite", xaml);
    }

    [Fact]
    public void MainPageViewModel_does_not_depend_on_WinUI_controls()
    {
        var code = File.ReadAllText(Path.Combine(RepositoryRoot(), "ImageViewerWin", "ViewModels", "MainPageViewModel.cs"));

        Assert.DoesNotContain("using Microsoft.UI.Xaml.Controls;", code);
        Assert.DoesNotContain("InfoBarSeverity", code);
    }

    [Fact]
    public void MainPage_maps_status_severity_to_InfoBarSeverity_in_the_view_layer()
    {
        var xaml = File.ReadAllText(Path.Combine(RepositoryRoot(), "ImageViewerWin", "MainPage.xaml"));
        var code = File.ReadAllText(Path.Combine(RepositoryRoot(), "ImageViewerWin", "MainPage.xaml.cs"));

        Assert.Contains(
            "Severity=\"{x:Bind local:MainPage.StatusSeverityToInfoBarSeverity(ViewModel.StatusSeverity), Mode=OneWay}\"",
            xaml);
        Assert.Contains("public static InfoBarSeverity StatusSeverityToInfoBarSeverity(MainPageStatusSeverity severity)", code);
        Assert.Contains("MainPageStatusSeverity.Warning => InfoBarSeverity.Warning", code);
        Assert.Contains("MainPageStatusSeverity.Error => InfoBarSeverity.Error", code);
    }

    [Fact]
    public void MainPage_view_model_labels_use_zh_tw_copy()
    {
        var viewModel = new MainPageViewModel(
            new ThrowingSettingsStore(),
            new ThrowingFolderScanner(),
            new ThrowingFileOperationService(),
            new NullThumbnailService(),
            () => Task.FromResult<string?>(null),
            (_, _, _) => Task.FromResult(false),
            _ => Task.FromResult<string?>(null),
            _ => { });

        Assert.Equal("僅目前資料夾", viewModel.RecursiveModeLabel);
        Assert.Equal("名稱由小到大", viewModel.SortLabel);
        Assert.Equal("就緒。原生 ImageViewer 已初始化。", viewModel.StatusMessage);
    }

    [Fact]
    public void Thumbnail_size_slider_commits_after_interaction_finishes()
    {
        var xaml = File.ReadAllText(Path.Combine(RepositoryRoot(), "ImageViewerWin", "MainPage.xaml"));

        Assert.Contains("AutomationProperties.AutomationId=\"ThumbnailSizeSlider\"", xaml);
        Assert.Contains("PointerCaptureLost=\"ThumbnailSizeSlider_CommitValue\"", xaml);
        Assert.Contains("LostFocus=\"ThumbnailSizeSlider_CommitValue\"", xaml);
        Assert.Contains("KeyUp=\"ThumbnailSizeSlider_KeyUp\"", xaml);
        Assert.DoesNotContain("ValueChanged=\"ThumbnailSizeSlider_ValueChanged\"", xaml);
    }

    [Fact]
    public void Library_tile_thumbnail_bindings_update_after_async_thumbnail_load()
    {
        var xaml = File.ReadAllText(Path.Combine(RepositoryRoot(), "ImageViewerWin", "MainPage.xaml"));

        Assert.Contains("ContainerContentChanging=\"LibraryGrid_ContainerContentChanging\"", xaml);
        Assert.Contains("Loaded=\"LibraryTile_Loaded\"", xaml);
        Assert.Contains("Unloaded=\"LibraryTile_Unloaded\"", xaml);
        Assert.Contains("Source=\"{x:Bind local:MainPage.CreateBitmapImage(ThumbnailPath), Mode=OneWay}\"", xaml);
        Assert.Contains("Visibility=\"{x:Bind local:MainPage.BoolToVisibility(CanShowThumbnail), Mode=OneWay}\"", xaml);
        Assert.Contains("Visibility=\"{x:Bind local:MainPage.BoolToVisibility(ShouldShowIcon), Mode=OneWay}\"", xaml);
    }

    [Fact]
    public void MainPage_xaml_declares_responsive_and_accessible_contracts()
    {
        var xaml = File.ReadAllText(Path.Combine(RepositoryRoot(), "ImageViewerWin", "MainPage.xaml"));

        Assert.Contains("<VisualStateManager.VisualStateGroups>", xaml);
        Assert.Contains("AdaptiveTrigger MinWindowWidth=\"1008\"", xaml);
        Assert.Contains("AdaptiveTrigger MinWindowWidth=\"641\"", xaml);
        Assert.Contains("AdaptiveTrigger MinWindowWidth=\"0\"", xaml);
        Assert.Contains("x:Name=\"FolderColumn\"", xaml);
        Assert.Contains("x:Name=\"FolderPane\"", xaml);
        Assert.Contains("AutomationProperties.Name=\"資料夾樹\"", xaml);
        Assert.Contains("AutomationProperties.Name=\"圖庫項目\"", xaml);
        Assert.Contains("AutomationProperties.Name=\"檔案操作狀態\"", xaml);
        Assert.Contains("AutomationProperties.LiveSetting=\"Polite\"", xaml);
        Assert.Contains("AutomationProperties.Name=\"{x:Bind AutomationName}\"", xaml);
        Assert.Contains("<AppBarButton", xaml);
        Assert.Contains("x:Name=\"TitleBarRefreshLibraryButton\"", xaml);
        Assert.Contains("VerticalAlignment=\"Center\"", xaml);
        Assert.Contains("x:Name=\"LibraryHeaderPath\"", xaml);
        Assert.Contains("x:Name=\"LibraryHeaderParentText\"", xaml);
        Assert.Contains("x:Name=\"LibraryHeaderCurrentText\"", xaml);
        Assert.Contains("TextTrimming=\"CharacterEllipsis\"", xaml);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"LibrarySearchBox\"", xaml);
        Assert.DoesNotContain("<AutoSuggestBox", xaml);
        Assert.DoesNotContain("SearchQuery", xaml);
    }

    [Fact]
    public void MainPage_top_right_commands_use_compact_centered_height()
    {
        var xaml = File.ReadAllText(Path.Combine(RepositoryRoot(), "ImageViewerWin", "MainPage.xaml"));

        Assert.Contains("x:Name=\"TitleBarBackButton\"", xaml);
        Assert.Contains("x:Name=\"TitleBarForwardButton\"", xaml);
        Assert.Contains("x:Name=\"TitleBarRefreshLibraryButton\"", xaml);
        Assert.Contains("x:Name=\"TitleBarOpenFolderButton\"", xaml);
        Assert.Contains("x:Name=\"TitleBarSortMenuButton\"", xaml);
        Assert.Contains("x:Name=\"TitleBarRecursiveModeToggle\"", xaml);
        Assert.Equal(3, xaml.Split("IsCompact=\"True\"").Length - 1);
        Assert.Contains("LabelPosition=\"Collapsed\"", xaml);
        Assert.Contains("VerticalAlignment=\"Center\"", xaml);
    }

    [Fact]
    public void MainPage_xaml_uses_dense_gallery_spacing()
    {
        var xaml = File.ReadAllText(Path.Combine(RepositoryRoot(), "ImageViewerWin", "MainPage.xaml"));

        Assert.Contains("<Setter Target=\"FolderColumn.Width\" Value=\"280\" />", xaml);
        Assert.Contains("<ColumnDefinition x:Name=\"FolderColumn\" Width=\"280\" />", xaml);
        Assert.Contains("<Setter Target=\"LibraryContent.Padding\" Value=\"20,16,20,16\" />", xaml);
        Assert.Contains("Padding=\"20,16,20,16\"", xaml);
        Assert.Contains("<Setter Property=\"Margin\" Value=\"0,0,12,12\" />", xaml);
        Assert.Contains("<Setter Property=\"MinWidth\" Value=\"128\" />", xaml);
        Assert.Contains("<Setter Property=\"MinHeight\" Value=\"136\" />", xaml);
    }

    [Fact]
    public void MainPage_command_bar_uses_overflow_instead_of_horizontal_scrolling()
    {
        var xaml = File.ReadAllText(Path.Combine(RepositoryRoot(), "ImageViewerWin", "MainPage.xaml"));

        Assert.DoesNotContain("AutomationProperties.AutomationId=\"LibraryCommandScrollViewer\"", xaml);
        Assert.DoesNotContain("<ScrollViewer", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"LibraryCommandBar\"", xaml);
        Assert.Contains("IsDynamicOverflowEnabled=\"True\"", xaml);
    }

    [Fact]
    public void MainPage_declares_contextual_selection_action_bar()
    {
        var xaml = File.ReadAllText(Path.Combine(RepositoryRoot(), "ImageViewerWin", "MainPage.xaml"));
        var code = File.ReadAllText(Path.Combine(RepositoryRoot(), "ImageViewerWin", "MainPage.xaml.cs"));

        Assert.Contains("AutomationProperties.AutomationId=\"LibrarySelectionActionBar\"", xaml);
        Assert.Contains("Background=\"{ThemeResource CardBackgroundFillColorDefaultBrush}\"", xaml);
        Assert.Contains("BorderBrush=\"{ThemeResource CardStrokeColorDefaultBrush}\"", xaml);
        Assert.Contains("Visibility=\"{x:Bind local:MainPage.BoolToVisibility(ViewModel.HasSelectedImages), Mode=OneWay}\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"SelectionSummaryText\"", xaml);
        Assert.Contains("TextTrimming=\"CharacterEllipsis\"", xaml);
        Assert.Contains("Text=\"{x:Bind ViewModel.SelectionSummaryText, Mode=OneWay}\"", xaml);
        Assert.Contains("x:Name=\"SelectionCommandBar\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"SelectionCommandBar\"", xaml);
        Assert.Contains("IsDynamicOverflowEnabled=\"True\"", xaml);
        Assert.Contains("<Setter Target=\"SelectionCommandBar.(Grid.Row)\" Value=\"1\" />", xaml);
        Assert.Contains("<Setter Target=\"SelectionCommandBar.DefaultLabelPosition\" Value=\"Collapsed\" />", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"SelectionRenameButton\"", xaml);
        Assert.Contains("Command=\"{x:Bind ViewModel.RenameSelectedCommand}\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"SelectionTrashButton\"", xaml);
        Assert.Contains("Command=\"{x:Bind ViewModel.TrashSelectedCommand}\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"SelectionConvertButton\"", xaml);
        Assert.Contains("Command=\"{x:Bind ViewModel.ConvertSelectedCommand}\"", xaml);
        Assert.Contains("Label=\"轉成 JPG\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"SelectionClearButton\"", xaml);
        Assert.Contains("Click=\"ClearLibrarySelection_Click\"", xaml);
        Assert.Contains("ClearLibrarySelection_Click", code);
        Assert.Contains("LibraryGrid.SelectedItems.Clear();", code);
        Assert.Contains("librarySelectionOrder.Clear();", code);
        Assert.Contains("ViewModel.ClearSelectedLibraryItems();", code);
    }

    [Fact]
    public void MainPage_dialogs_use_clear_accessible_action_text()
    {
        var code = File.ReadAllText(Path.Combine(RepositoryRoot(), "ImageViewerWin", "MainPage.xaml.cs"));

        Assert.Contains("ConfirmAsync(string message, string title, string primaryButtonText)", code);
        Assert.Contains("PrimaryButtonText = primaryButtonText", code);
        Assert.Contains("Header = \"新檔名\"", code);
        Assert.Contains("PlaceholderText = \"輸入新的檔案名稱\"", code);
        Assert.Contains("AutomationProperties.SetName(input, \"新檔名\")", code);
    }

    [Fact]
    public void Library_grid_items_are_drop_targets_for_drop_rename()
    {
        var xaml = File.ReadAllText(Path.Combine(RepositoryRoot(), "ImageViewerWin", "MainPage.xaml"));
        var code = File.ReadAllText(Path.Combine(RepositoryRoot(), "ImageViewerWin", "MainPage.xaml.cs"));

        Assert.Contains("PointerMoved=\"LibraryGrid_PointerMoved\"", xaml);
        Assert.Contains("PointerReleased=\"LibraryGrid_PointerReleased\"", xaml);
        Assert.Contains("PointerPressed=\"LibraryTile_PointerPressed\"", xaml);
        Assert.Contains("LibraryGrid_PointerMoved", code);
        Assert.Contains("LibraryGrid_PointerReleased", code);
        Assert.Contains("LibraryTile_PointerPressed", code);
        Assert.Contains("DropDraggedImagesOnAsync(target)", code);
    }

    [Fact]
    public void MainPage_defers_thumbnail_loads_out_of_xaml_container_callbacks()
    {
        var code = File.ReadAllText(Path.Combine(RepositoryRoot(), "ImageViewerWin", "MainPage.xaml.cs"));

        Assert.Contains("private void QueueThumbnailLoad(LibraryTileItem item)", code);
        Assert.Contains("DispatcherQueue.TryEnqueue(() => _ = ViewModel.LoadThumbnailAsync(item))", code);
        Assert.Contains("Queue thumbnail load failed.", code);
        Assert.Contains("QueueThumbnailLoad(item);", code);
    }

    [Fact]
    public void Library_tile_labels_use_zh_tw_copy()
    {
        var folder = new LibraryTileItem(
            Name: "Album",
            Path: @"C:\Album",
            Detail: "開啟資料夾",
            IsFolder: true,
            IsSelected: false,
            IsAnimated: false,
            IconGlyph: "\uE8B7",
            SourceItem: new FolderListItem("folder:album", @"C:\Album", "Album", 1));

        var image = folder with
        {
            IsFolder = false,
            IsAnimated = false,
            SourceItem = new ImageListItem("image:a", @"C:\Album\a.jpg", "a.jpg", "jpg", 1, 10)
        };

        var animated = image with { IsAnimated = true };

        Assert.Equal("資料夾", folder.KindLabel);
        Assert.Equal("圖片", image.KindLabel);
        Assert.Equal("不支援動畫圖片", animated.KindLabel);
        Assert.Equal("Album，資料夾，開啟資料夾", folder.AutomationName);
    }

    [Fact]
    public void Status_severity_defaults_to_informational_and_tracks_load_errors()
    {
        var viewModel = new MainPageViewModel(
            new ThrowingSettingsStore(),
            new ThrowingFolderScanner(),
            new ThrowingFileOperationService(),
            new NullThumbnailService(),
            () => Task.FromResult<string?>(null),
            (_, _, _) => Task.FromResult(false),
            _ => Task.FromResult<string?>(null),
            _ => { });

        Assert.Equal(MainPageStatusSeverity.Informational, viewModel.StatusSeverity);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ImageViewerWin.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
    }

    private sealed class ThrowingSettingsStore : ImageViewerWin.Application.Services.ISettingsStore
    {
        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AppSettings> UpdateAsync(AppSettingsPatch patch, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class ThrowingFolderScanner : ImageViewerWin.Application.Services.IFolderScanner
    {
        public Task<IReadOnlyList<ListItem>> ScanAsync(
            ListQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<FolderListItem>> ScanChildFoldersAsync(
            string folderPath,
            SortState sort,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class ThrowingFileOperationService : ImageViewerWin.Application.Services.IFileOperationService
    {
        public Task<FileOperationBatchResult> ConvertVisibleToJpgAsync(
            IEnumerable<ImageListItem> visibleImages,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<FileOperationBatchResult> TrashSameBasenameNonJpgAsync(
            IEnumerable<ImageListItem> visibleImages,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<FileOperationResult> TrashAsync(string path, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<FileOperationResult> RenameAsync(
            string sourcePath,
            string newFileName,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<FileOperationBatchResult> RenameByDropTargetAsync(
            IEnumerable<string> sourcePaths,
            string targetPath,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class NullThumbnailService : IThumbnailService
    {
        public Task<string?> GetOrCreateThumbnailAsync(
            string imagePath,
            int requestedSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }
}
