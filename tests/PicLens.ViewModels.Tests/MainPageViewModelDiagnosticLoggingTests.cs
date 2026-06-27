using PicLens.Core.Services;
using PicLens.Core.Domain;
using PicLens.Core.Models;
using PicLens.Diagnostics;
using PicLens.Services;
using PicLens.ViewModels;

namespace PicLens.ViewModels.Tests;

public sealed class MainPageViewModelDiagnosticLoggingTests
{

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
            dialogService: new RecordingDropRenameDialogService(confirmDropRename: true),
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
    public async Task DropDraggedImagesOnAsync_confirms_preview_before_renaming()
    {
        using var workspace = new TempDirectory();
        var source = new ImageListItem("image:source", Path.Combine(workspace.Path, "source.jpg"), "source.jpg", ".jpg", 100, 1024);
        var target = new ImageListItem("image:target", Path.Combine(workspace.Path, "target.jpg"), "target.jpg", ".jpg", 200, 1024);
        var dialogService = new RecordingDropRenameDialogService(confirmDropRename: true);
        var fileOperations = new RecordingFileOperationService(new FileOperationBatchResult(
            Total: 1,
            Succeeded: 1,
            Skipped: 0,
            Failed: 0,
            Items: [new FileOperationResult(source.Path, FileOperationStatus.Renamed, Path.Combine(workspace.Path, "target-01.jpg"))]));
        var viewModel = CreateViewModel(
            AppSettings.CreateDefault() with { LastFolderPath = workspace.Path },
            [source, target],
            fileOperationService: fileOperations,
            dialogService: dialogService);

        await viewModel.InitializeAsync();
        viewModel.BeginImageDrag([viewModel.LibraryItems.Single(item => item.Name == source.Name)]);
        await viewModel.DropDraggedImagesOnAsync(viewModel.LibraryItems.Single(item => item.Name == target.Name));

        var preview = Assert.Single(dialogService.DropRenamePreviews);
        Assert.Equal(1, preview.Total);
        Assert.Equal(1, preview.RenameCount);
        Assert.Equal(0, preview.SkippedCount);
        Assert.Equal("target-01.jpg", Assert.Single(preview.Items).TargetName);
        Assert.Equal(1, fileOperations.RenameByDropTargetCallCount);
        Assert.Equal([source.Path], fileOperations.LastSourcePaths);
        Assert.Equal(target.Path, fileOperations.LastTargetPath);
    }

    [Fact]
    public async Task DropDraggedImagesOnAsync_canceling_preview_skips_rename_service()
    {
        using var workspace = new TempDirectory();
        var source = new ImageListItem("image:source", Path.Combine(workspace.Path, "source.jpg"), "source.jpg", ".jpg", 100, 1024);
        var target = new ImageListItem("image:target", Path.Combine(workspace.Path, "target.jpg"), "target.jpg", ".jpg", 200, 1024);
        var dialogService = new RecordingDropRenameDialogService(confirmDropRename: false);
        var fileOperations = new RecordingFileOperationService(new FileOperationBatchResult(
            Total: 1,
            Succeeded: 1,
            Skipped: 0,
            Failed: 0,
            Items: [new FileOperationResult(source.Path, FileOperationStatus.Renamed, Path.Combine(workspace.Path, "target-01.jpg"))]));
        var viewModel = CreateViewModel(
            AppSettings.CreateDefault() with { LastFolderPath = workspace.Path },
            [source, target],
            fileOperationService: fileOperations,
            dialogService: dialogService);

        await viewModel.InitializeAsync();
        viewModel.BeginImageDrag([viewModel.LibraryItems.Single(item => item.Name == source.Name)]);
        await viewModel.DropDraggedImagesOnAsync(viewModel.LibraryItems.Single(item => item.Name == target.Name));

        Assert.Single(dialogService.DropRenamePreviews);
        Assert.Equal(0, fileOperations.RenameByDropTargetCallCount);
        Assert.Equal("已取消拖放重新命名。", viewModel.StatusMessage);
    }

    [Fact]
    public async Task DropDraggedImagesOnAsync_logs_per_item_batch_failures()
    {
        using var workspace = new TempDirectory();
        var source = new ImageListItem("image:source", Path.Combine(workspace.Path, "source.jpg"), "source.jpg", ".jpg", 100, 1024);
        var target = new ImageListItem("image:target", Path.Combine(workspace.Path, "target.jpg"), "target.jpg", ".jpg", 200, 1024);
        var logger = new RecordingAppLogger();
        var fileOperations = new RecordingFileOperationService(new FileOperationBatchResult(
            Total: 1,
            Succeeded: 0,
            Skipped: 0,
            Failed: 1,
            Items: [new FileOperationResult(source.Path, FileOperationStatus.Failed, Path.Combine(workspace.Path, "target-01.jpg"), "rename_failed", "locked")]));
        var viewModel = CreateViewModel(
            AppSettings.CreateDefault() with { LastFolderPath = workspace.Path },
            [source, target],
            fileOperationService: fileOperations,
            dialogService: new RecordingDropRenameDialogService(confirmDropRename: true),
            logger: logger);

        await viewModel.InitializeAsync();
        viewModel.BeginImageDrag([viewModel.LibraryItems.Single(item => item.Name == source.Name)]);
        await viewModel.DropDraggedImagesOnAsync(viewModel.LibraryItems.Single(item => item.Name == target.Name));

        var entry = Assert.Single(logger.ErrorMessages, error => error.Message.StartsWith("Drop dragged images item failed.", StringComparison.Ordinal));
        Assert.Equal("locked", entry.Exception.Message);
        Assert.Contains("Reason=rename_failed", entry.Message);
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
            dialogService: new RecordingDropRenameDialogService(confirmDropRename: true),
            logger: logger);

        await viewModel.InitializeAsync();
        var sourceTile = viewModel.LibraryItems.Single(item => item.Name == source.Name);
        var targetTile = viewModel.LibraryItems.Single(item => item.Name == target.Name);

        viewModel.BeginImageDrag([sourceTile]);
        await viewModel.DropDraggedImagesOnAsync(targetTile);

        var entry = Assert.Single(logger.ErrorMessages, error => error.Message == "Drop dragged images failed.");
        Assert.Same(expected, entry.Exception);
        Assert.Contains("拖放重新命名時發生錯誤", viewModel.StatusMessage);
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
            thumbnailService: new TestThumbnailService((_, _, _) => throw expected),
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
        IDialogService? dialogService = null,
        IAppLogger? logger = null,
        Action<ImageSequenceSnapshot>? openImageViewer = null) =>
        new(
            new FakeSettingsStore(settings),
            new CountingFolderScanner(items),
            fileOperationService ?? new ThrowingFileOperationService(),
            thumbnailService ?? new NullThumbnailService(),
            dialogService ?? new NullDialogService(),
            openImageViewer: openImageViewer,
            appLogger: logger);


    private sealed class RecordingFileOperationService(FileOperationBatchResult result) : IFileOperationService
    {
        public int RenameByDropTargetCallCount { get; private set; }

        public IReadOnlyList<string> LastSourcePaths { get; private set; } = [];

        public string? LastTargetPath { get; private set; }

        public Task<FileOperationBatchResult> RenameByDropTargetAsync(
            IEnumerable<string> sourcePaths,
            string targetPath,
            CancellationToken cancellationToken = default)
        {
            RenameByDropTargetCallCount += 1;
            LastSourcePaths = sourcePaths.ToList();
            LastTargetPath = targetPath;
            return Task.FromResult(result);
        }

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

    private sealed class RecordingDropRenameDialogService(bool confirmDropRename) : IDialogService
    {
        public List<DropRenamePreview> DropRenamePreviews { get; } = [];

        public Task<string?> ChooseFolderAsync() => Task.FromResult<string?>(null);

        public Task<bool> ConfirmAsync(string message, string title, string confirmButtonText) =>
            Task.FromResult(false);

        public Task<bool> ConfirmDropRenameAsync(DropRenamePreview preview)
        {
            DropRenamePreviews.Add(preview);
            return Task.FromResult(confirmDropRename);
        }

        public Task<string?> RequestRenameAsync(ImageListItem item) => Task.FromResult<string?>(null);
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

}
