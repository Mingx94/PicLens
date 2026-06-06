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
    public void MainPage_view_model_labels_use_zh_tw_copy()
    {
        var viewModel = new MainPageViewModel(
            new ThrowingSettingsStore(),
            new ThrowingFolderScanner(),
            new ThrowingFileOperationService(),
            new NullThumbnailService(),
            () => Task.FromResult<string?>(null),
            (_, _) => Task.FromResult(false),
            _ => Task.FromResult<string?>(null),
            _ => { });

        Assert.Equal("僅目前資料夾", viewModel.RecursiveModeLabel);
        Assert.Equal("名稱 遞增", viewModel.SortLabel);
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
