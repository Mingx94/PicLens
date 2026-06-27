using PicLens.Core.Services;
using PicLens.Core.Domain;
using PicLens.Core.Models;
using PicLens.Diagnostics;
using PicLens.ViewModels;

namespace PicLens.ViewModels.Tests;

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

    [Fact]
    public async Task TrashSelectedCommand_trashes_all_selected_images_and_clears_selection()
    {
        using var workspace = new TempDirectory();
        var first = new ImageListItem("image:first", Path.Combine(workspace.Path, "first.png"), "first.png", ".png", 100, 1024);
        var second = new ImageListItem("image:second", Path.Combine(workspace.Path, "second.webp"), "second.webp", ".webp", 200, 2048);
        var fileOperations = new RecordingFileOperationService();
        var viewModel = CreateViewModel(
            workspace.Path,
            new CountingFolderScanner([first, second]),
            fileOperations,
            confirmAsync: (_, _, _) => Task.FromResult(true));

        await viewModel.InitializeAsync();
        viewModel.UpdateSelectedLibraryItems(viewModel.LibraryItems);

        Assert.True(viewModel.TrashSelectedCommand.CanExecute(null));

        await viewModel.TrashSelectedCommand.ExecuteAsync(null);

        Assert.Equal([first.Path, second.Path], fileOperations.TrashedPaths);
        Assert.Equal(0, viewModel.SelectedImageCount);
        Assert.Equal("移至回收筒：成功 2 個，略過 0 個，失敗 0 個。", viewModel.StatusMessage);
    }

    [Fact]
    public async Task RenameSelectedCommand_logs_error_when_rename_throws()
    {
        using var workspace = new TempDirectory();
        var image = new ImageListItem("image:first", Path.Combine(workspace.Path, "first.png"), "first.png", ".png", 100, 1024);
        var logger = new RecordingAppLogger();
        var viewModel = CreateViewModel(
            workspace.Path,
            new CountingFolderScanner([image]),
            new ThrowingFileOperationService(new InvalidOperationException("rename failed")),
            requestRenameAsync: _ => Task.FromResult<string?>("renamed.png"),
            appLogger: logger);

        await viewModel.InitializeAsync();
        viewModel.UpdateSelectedLibraryItems(viewModel.LibraryItems);

        await viewModel.RenameSelectedCommand.ExecuteAsync(null);

        Assert.Single(logger.ErrorMessages);
        Assert.Equal("Rename selected image failed.", logger.ErrorMessages[0].Message);
        Assert.Equal("重新命名時發生錯誤，已寫入診斷記錄。", viewModel.StatusMessage);
    }

    [Fact]
    public async Task TrashSelectedCommand_logs_error_when_trash_throws()
    {
        using var workspace = new TempDirectory();
        var image = new ImageListItem("image:first", Path.Combine(workspace.Path, "first.png"), "first.png", ".png", 100, 1024);
        var logger = new RecordingAppLogger();
        var viewModel = CreateViewModel(
            workspace.Path,
            new CountingFolderScanner([image]),
            new ThrowingFileOperationService(new InvalidOperationException("trash failed")),
            confirmAsync: (_, _, _) => Task.FromResult(true),
            appLogger: logger);

        await viewModel.InitializeAsync();
        viewModel.UpdateSelectedLibraryItems(viewModel.LibraryItems);

        await viewModel.TrashSelectedCommand.ExecuteAsync(null);

        Assert.Single(logger.ErrorMessages);
        Assert.Equal("Trash selected images failed.", logger.ErrorMessages[0].Message);
        Assert.Equal("移至回收筒時發生錯誤，已寫入診斷記錄。", viewModel.StatusMessage);
    }

    private static MainPageViewModel CreateViewModel(
        string lastFolderPath,
        IFolderScanner folderScanner,
        IFileOperationService fileOperationService,
        Func<string, string, string, Task<bool>>? confirmAsync = null,
        Func<ImageListItem, Task<string?>>? requestRenameAsync = null,
        IAppLogger? appLogger = null) =>
        new(
            new FakeSettingsStore(AppSettings.CreateDefault() with { LastFolderPath = lastFolderPath }),
            folderScanner,
            fileOperationService,
            new NullThumbnailService(),
            new TestDialogService(confirmAsync: confirmAsync, requestRenameAsync: requestRenameAsync),
            appLogger: appLogger);

    private sealed class RecordingFileOperationService : IFileOperationService
    {
        public IReadOnlyList<string> ConvertedPaths { get; private set; } = [];

        public IReadOnlyList<string> TrashedPaths { get; private set; } = [];

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

        public Task<FileOperationResult> TrashAsync(string path, CancellationToken cancellationToken = default)
        {
            TrashedPaths = TrashedPaths.Append(path).ToArray();
            return Task.FromResult(new FileOperationResult(path, FileOperationStatus.Trashed));
        }

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


}
