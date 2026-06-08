using ImageViewerWin.Application.Services;
using ImageViewerWin.Core.Domain;
using ImageViewerWin.Core.Models;
using ImageViewerWin.Diagnostics;
using ImageViewerWin.ViewModels;

namespace ImageViewerWin.ViewModels.Tests;

public sealed class MainPageViewModelDiagnosticLoggingTests
{
    [Fact]
    public async Task OpenLibraryItemAsync_logs_image_viewer_snapshot_context()
    {
        using var workspace = new TempDirectory();
        var image = new ImageListItem(
            "image:first",
            Path.Combine(workspace.Path, "first.jpg"),
            "first.jpg",
            ".jpg",
            100,
            1024);
        var logger = new RecordingAppLogger();
        var openedSnapshots = new List<ImageSequenceSnapshot>();
        var viewModel = CreateViewModel(
            AppSettings.CreateDefault() with { LastFolderPath = workspace.Path },
            [image],
            logger: logger,
            openImageViewer: openedSnapshots.Add);

        await viewModel.InitializeAsync();
        await viewModel.OpenLibraryItemAsync(Assert.Single(viewModel.LibraryItems));

        Assert.Single(openedSnapshots);
        var message = Assert.Single(logger.InfoMessages, entry => entry.StartsWith("Open image viewer requested.", StringComparison.Ordinal));
        Assert.Contains("Image=first.jpg", message);
        Assert.Contains("CurrentIndex=0", message);
        Assert.Contains("ImageCount=1", message);
        Assert.Contains($"CurrentFolderPath={workspace.Path}", message);
        Assert.Contains("IncludeSubfolders=False", message);
        Assert.Contains("Sort=Name/Asc", message);
    }

    [Fact]
    public async Task DropDraggedImagesOnAsync_logs_result_context()
    {
        using var workspace = new TempDirectory();
        var source = new ImageListItem("image:source", Path.Combine(workspace.Path, "source.jpg"), "source.jpg", ".jpg", 100, 1024);
        var target = new ImageListItem("image:target", Path.Combine(workspace.Path, "target.jpg"), "target.jpg", ".jpg", 200, 1024);
        var logger = new RecordingAppLogger();
        var fileOperations = new RecordingFileOperationService(new FileOperationBatchResult(
            Total: 1,
            Succeeded: 1,
            Skipped: 0,
            Failed: 0,
            Items: [new FileOperationResult(source.Path, FileOperationStatus.Renamed, target.Path)]));
        var viewModel = CreateViewModel(
            AppSettings.CreateDefault() with { LastFolderPath = workspace.Path },
            [source, target],
            fileOperationService: fileOperations,
            logger: logger);

        await viewModel.InitializeAsync();
        var sourceTile = viewModel.LibraryItems.Single(item => item.Name == source.Name);
        var targetTile = viewModel.LibraryItems.Single(item => item.Name == target.Name);

        viewModel.BeginImageDrag([sourceTile]);
        await viewModel.DropDraggedImagesOnAsync(targetTile);

        Assert.Contains(logger.InfoMessages, entry =>
            entry.Contains("Drop dragged images started.", StringComparison.Ordinal)
            && entry.Contains("SourceCount=1", StringComparison.Ordinal)
            && entry.Contains("Target=target.jpg", StringComparison.Ordinal));
        Assert.Contains(logger.InfoMessages, entry =>
            entry.Contains("Drop dragged images completed.", StringComparison.Ordinal)
            && entry.Contains("Total=1", StringComparison.Ordinal)
            && entry.Contains("Succeeded=1", StringComparison.Ordinal)
            && entry.Contains("Skipped=0", StringComparison.Ordinal)
            && entry.Contains("Failed=0", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DropDraggedImagesOnAsync_logs_unexpected_failures()
    {
        using var workspace = new TempDirectory();
        var source = new ImageListItem("image:source", Path.Combine(workspace.Path, "source.jpg"), "source.jpg", ".jpg", 100, 1024);
        var target = new ImageListItem("image:target", Path.Combine(workspace.Path, "target.jpg"), "target.jpg", ".jpg", 200, 1024);
        var expected = new IOException("rename failed");
        var logger = new RecordingAppLogger();
        var viewModel = CreateViewModel(
            AppSettings.CreateDefault() with { LastFolderPath = workspace.Path },
            [source, target],
            fileOperationService: new ThrowingDropFileOperationService(expected),
            logger: logger);

        await viewModel.InitializeAsync();
        var sourceTile = viewModel.LibraryItems.Single(item => item.Name == source.Name);
        var targetTile = viewModel.LibraryItems.Single(item => item.Name == target.Name);

        viewModel.BeginImageDrag([sourceTile]);
        await viewModel.DropDraggedImagesOnAsync(targetTile);

        var entry = Assert.Single(logger.ErrorMessages, error => error.Message == "Drop dragged images failed.");
        Assert.Same(expected, entry.Exception);
        Assert.Contains("拖放重新命名時發生錯誤", viewModel.StatusMessage);
        Assert.Equal(MainPageStatusSeverity.Error, viewModel.StatusSeverity);
    }

    [Fact]
    public async Task LoadThumbnailAsync_logs_thumbnail_failures_without_throwing()
    {
        using var workspace = new TempDirectory();
        var image = new ImageListItem(
            "image:first",
            Path.Combine(workspace.Path, "first.jpg"),
            "first.jpg",
            ".jpg",
            100,
            1024);
        var expected = new IOException("decode failed");
        var logger = new RecordingAppLogger();
        var viewModel = CreateViewModel(
            AppSettings.CreateDefault() with { LastFolderPath = workspace.Path },
            [image],
            thumbnailService: new ThrowingThumbnailService(expected),
            logger: logger);

        await viewModel.InitializeAsync();
        await viewModel.LoadThumbnailAsync(Assert.Single(viewModel.LibraryItems));

        var entry = Assert.Single(logger.ErrorMessages, error => error.Message.StartsWith("Load thumbnail failed.", StringComparison.Ordinal));
        Assert.Same(expected, entry.Exception);
        Assert.Contains("Image=first.jpg", entry.Message);
        Assert.Contains("RequestedSize=160", entry.Message);
    }

    private static MainPageViewModel CreateViewModel(
        AppSettings settings,
        IReadOnlyList<ListItem> items,
        IFileOperationService? fileOperationService = null,
        IThumbnailService? thumbnailService = null,
        IAppLogger? logger = null,
        Action<ImageSequenceSnapshot>? openImageViewer = null) =>
        new(
            new FakeSettingsStore(settings),
            new CountingFolderScanner(items),
            fileOperationService ?? new ThrowingFileOperationService(),
            thumbnailService ?? new NullThumbnailService(),
            () => Task.FromResult<string?>(null),
            (_, _, _) => Task.FromResult(false),
            _ => Task.FromResult<string?>(null),
            openImageViewer ?? (_ => { }),
            appLogger: logger);

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

    private sealed class RecordingFileOperationService(FileOperationBatchResult result) : IFileOperationService
    {
        public Task<FileOperationBatchResult> RenameByDropTargetAsync(
            IEnumerable<string> sourcePaths,
            string targetPath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(result);

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
    }

    private sealed class ThrowingDropFileOperationService(Exception exception) : IFileOperationService
    {
        public Task<FileOperationBatchResult> RenameByDropTargetAsync(
            IEnumerable<string> sourcePaths,
            string targetPath,
            CancellationToken cancellationToken = default) =>
            throw exception;

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

    private sealed class ThrowingThumbnailService(Exception exception) : IThumbnailService
    {
        public Task<string?> GetOrCreateThumbnailAsync(
            string imagePath,
            int requestedSize,
            CancellationToken cancellationToken = default) =>
            throw exception;
    }

    private sealed class NullThumbnailService : IThumbnailService
    {
        public Task<string?> GetOrCreateThumbnailAsync(
            string imagePath,
            int requestedSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }

    private sealed class RecordingAppLogger : IAppLogger
    {
        public List<string> InfoMessages { get; } = [];

        public List<(Exception Exception, string Message)> ErrorMessages { get; } = [];

        public void Info(string message) => InfoMessages.Add(message);

        public void Error(Exception exception, string message) => ErrorMessages.Add((exception, message));
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
