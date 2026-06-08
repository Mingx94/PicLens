using ImageViewerWin.Application.Services;
using ImageViewerWin.Core.Domain;
using ImageViewerWin.Core.Models;
using ImageViewerWin.ViewModels;

namespace ImageViewerWin.ViewModels.Tests;

public sealed class MainPageSelectionActionTests
{
    [Fact]
    public async Task ConvertSelectedCommand_converts_selected_images_and_clears_selection()
    {
        using var workspace = new TempDirectory();
        var first = new ImageListItem("image:first", Path.Combine(workspace.Path, "first.png"), "first.png", ".png", 100, 1024);
        var second = new ImageListItem("image:second", Path.Combine(workspace.Path, "second.webp"), "second.webp", ".webp", 200, 2048);
        var fileOperations = new RecordingFileOperationService();
        var viewModel = CreateViewModel(
            workspace.Path,
            new CountingFolderScanner([first, second]),
            fileOperations);

        await viewModel.InitializeAsync();
        viewModel.UpdateSelectedLibraryItems(viewModel.LibraryItems);

        Assert.True(viewModel.ConvertSelectedCommand.CanExecute(null));

        await viewModel.ConvertSelectedCommand.ExecuteAsync(null);

        Assert.Equal([first.Path, second.Path], fileOperations.ConvertedPaths);
        Assert.Equal(0, viewModel.SelectedImageCount);
        Assert.False(viewModel.ConvertSelectedCommand.CanExecute(null));
        Assert.Equal("轉成 JPG：成功 2 個，略過 0 個，失敗 0 個。", viewModel.StatusMessage);
    }

    private static MainPageViewModel CreateViewModel(
        string lastFolderPath,
        IFolderScanner folderScanner,
        IFileOperationService fileOperationService) =>
        new(
            new FakeSettingsStore(AppSettings.CreateDefault() with { LastFolderPath = lastFolderPath }),
            folderScanner,
            fileOperationService,
            new NullThumbnailService(),
            () => Task.FromResult<string?>(null),
            (_, _, _) => Task.FromResult(false),
            _ => Task.FromResult<string?>(null),
            _ => { });

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

    private sealed class RecordingFileOperationService : IFileOperationService
    {
        public IReadOnlyList<string> ConvertedPaths { get; private set; } = [];

        public Task<FileOperationBatchResult> ConvertVisibleToJpgAsync(
            IEnumerable<ImageListItem> visibleImages,
            CancellationToken cancellationToken = default)
        {
            ConvertedPaths = visibleImages.Select(image => image.Path).ToArray();
            return Task.FromResult(new FileOperationBatchResult(
                Total: ConvertedPaths.Count,
                Succeeded: ConvertedPaths.Count,
                Skipped: 0,
                Failed: 0,
                Items: ConvertedPaths.Select(path => new FileOperationResult(path, FileOperationStatus.Converted)).ToArray()));
        }

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
