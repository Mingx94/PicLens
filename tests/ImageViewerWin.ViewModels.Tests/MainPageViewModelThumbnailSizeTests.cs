using ImageViewerWin.Application.Services;
using ImageViewerWin.Core.Domain;
using ImageViewerWin.Core.Models;
using ImageViewerWin.ViewModels;

namespace ImageViewerWin.ViewModels.Tests;

public sealed class MainPageViewModelThumbnailSizeTests
{
    [Fact]
    public async Task InitializeAsync_applies_persisted_thumbnail_size_to_tiles()
    {
        using var workspace = new TempDirectory();
        var scanner = new CountingFolderScanner(
        [
            new ImageListItem("image:first", Path.Combine(workspace.Path, "first.jpg"), "first.jpg", "jpg", 100, 1024)
        ]);
        var settingsStore = new FakeSettingsStore(AppSettings.CreateDefault() with
        {
            LastFolderPath = workspace.Path,
            ThumbnailSize = 350
        });
        var viewModel = CreateViewModel(settingsStore, scanner);

        await viewModel.InitializeAsync();

        var tile = Assert.Single(viewModel.LibraryItems);
        Assert.Equal(350, viewModel.ThumbnailSize);
        Assert.Equal(350, tile.TileWidth);
        Assert.Equal(346, tile.TileHeight);
    }

    [Fact]
    public async Task ChangeThumbnailSizeAsync_persists_size_and_updates_existing_tiles()
    {
        using var workspace = new TempDirectory();
        var scanner = new CountingFolderScanner(
        [
            new ImageListItem("image:first", Path.Combine(workspace.Path, "first.jpg"), "first.jpg", "jpg", 100, 1024)
        ]);
        var settingsStore = new FakeSettingsStore(AppSettings.CreateDefault() with
        {
            LastFolderPath = workspace.Path
        });
        var viewModel = CreateViewModel(settingsStore, scanner);

        await viewModel.InitializeAsync();
        var tile = Assert.Single(viewModel.LibraryItems);

        await viewModel.ChangeThumbnailSizeAsync(226);

        Assert.Equal(250, viewModel.ThumbnailSize);
        Assert.Equal(250, settingsStore.Settings.ThumbnailSize);
        Assert.Equal(250, tile.TileWidth);
        Assert.Equal(246, tile.TileHeight);
        Assert.Equal("縮圖大小已調整為 250。", viewModel.StatusMessage);
    }

    private static MainPageViewModel CreateViewModel(
        ISettingsStore settingsStore,
        IFolderScanner scanner) =>
        new(
            settingsStore,
            scanner,
            new ThrowingFileOperationService(),
            () => Task.FromResult<string?>(null),
            (_, _) => Task.FromResult(false),
            _ => Task.FromResult<string?>(null),
            _ => { });

    private sealed class CountingFolderScanner(IReadOnlyList<ListItem> items) : IFolderScanner
    {
        public Task<IReadOnlyList<ListItem>> ScanAsync(ListQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(ListItemSorter.Sort(items, query.Sort, new SortOptions(KeepFoldersFirst: true)));

        public Task<IReadOnlyList<FolderListItem>> ScanChildFoldersAsync(
            string folderPath,
            SortState sort,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FolderListItem>>([]);
    }

    private sealed class FakeSettingsStore(AppSettings initialSettings) : ISettingsStore
    {
        public AppSettings Settings { get; private set; } = initialSettings;

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Settings);

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            Settings = settings;
            return Task.CompletedTask;
        }

        public Task<AppSettings> UpdateAsync(AppSettingsPatch patch, CancellationToken cancellationToken = default)
        {
            Settings = SettingsRules.MergeSettingsPatch(Settings, patch);
            return Task.FromResult(Settings);
        }
    }

    private sealed class ThrowingFileOperationService : IFileOperationService
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

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ImageViewerWin.ViewModels.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
