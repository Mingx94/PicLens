using ImageViewerWin.Application.Services;
using ImageViewerWin.Core.Domain;
using ImageViewerWin.Core.Models;
using ImageViewerWin.ViewModels;

namespace ImageViewerWin.ViewModels.Tests;

public sealed class MainPageViewModelSortTests
{
    [Fact]
    public async Task ToggleSortDirectionReordersCurrentItemsWithoutRescanning()
    {
        using var workspace = new TempDirectory();
        var scanner = new CountingFolderScanner(
        [
            new ImageListItem("image:zulu", System.IO.Path.Combine(workspace.Path, "zulu.jpg"), "zulu.jpg", ".jpg", 200, 1024),
            new ImageListItem("image:alpha", System.IO.Path.Combine(workspace.Path, "alpha.jpg"), "alpha.jpg", ".jpg", 100, 1024)
        ]);
        var settingsStore = new FakeSettingsStore(AppSettings.CreateDefault() with
        {
            LastFolderPath = workspace.Path
        });
        var viewModel = new MainPageViewModel(
            settingsStore,
            scanner,
            new ThrowingFileOperationService(),
            new NullThumbnailService(),
            () => Task.FromResult<string?>(null),
            (_, _) => Task.FromResult(false),
            _ => Task.FromResult<string?>(null),
            _ => { });

        await viewModel.InitializeAsync();
        var scansAfterInitialize = scanner.ScanCount;
        var initialOrder = viewModel.LibraryItems.Select(item => item.Name).ToArray();

        await viewModel.ToggleSortDirectionCommand.ExecuteAsync(null);

        Assert.Equal(scansAfterInitialize, scanner.ScanCount);
        Assert.Equal(["alpha.jpg", "zulu.jpg"], initialOrder);
        Assert.Equal(["zulu.jpg", "alpha.jpg"], viewModel.LibraryItems.Select(item => item.Name));
    }

    private sealed class CountingFolderScanner(IReadOnlyList<ListItem> items) : IFolderScanner
    {
        public int ScanCount { get; private set; }

        public Task<IReadOnlyList<ListItem>> ScanAsync(ListQuery query, CancellationToken cancellationToken = default)
        {
            ScanCount += 1;
            return Task.FromResult(ListItemSorter.Sort(items, query.Sort, new SortOptions(KeepFoldersFirst: true)));
        }

        public Task<IReadOnlyList<FolderListItem>> ScanChildFoldersAsync(
            string folderPath,
            SortState sort,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FolderListItem>>([]);
    }

    private sealed class FakeSettingsStore(AppSettings initialSettings) : ISettingsStore
    {
        private AppSettings settings = initialSettings;

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(settings);

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            this.settings = settings;
            return Task.CompletedTask;
        }

        public Task<AppSettings> UpdateAsync(AppSettingsPatch patch, CancellationToken cancellationToken = default)
        {
            settings = SettingsRules.MergeSettingsPatch(settings, patch);
            return Task.FromResult(settings);
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

    private sealed class NullThumbnailService : IThumbnailService
    {
        public Task<string?> GetOrCreateThumbnailAsync(
            string imagePath,
            int requestedSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
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
