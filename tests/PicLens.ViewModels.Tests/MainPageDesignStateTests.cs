using PicLens.Core.Services;
using PicLens.Core.Domain;
using PicLens.Core.Models;
using PicLens.Services;
using PicLens.ViewModels;

namespace PicLens.ViewModels.Tests;

public sealed class MainPageDesignStateTests
{
    [Fact]
    public async Task Search_filters_folder_and_image_sections_and_clear_restores_items()
    {
        using var workspace = new TempDirectory();
        var nested = new FolderListItem(Path.Combine(workspace.Path, "Nested"), "Nested", 10);
        var alpha = new ImageListItem(Path.Combine(workspace.Path, "Alpha.png"), "Alpha.png", ".png", 20, 1024);
        var bravo = new ImageListItem(Path.Combine(workspace.Path, "Bravo.jpg"), "Bravo.jpg", ".jpg", 30, 2048);
        var viewModel = CreateViewModel(workspace.Path, new CountingFolderScanner([nested, alpha, bravo]));

        await viewModel.InitializeAsync();
        viewModel.SearchQuery = "alpha";

        Assert.Empty(viewModel.FolderLibraryItems);
        Assert.Equal(["Alpha.png"], viewModel.ImageLibraryItems.Select(item => item.Name));
        Assert.False(viewModel.HasNoSearchResults);

        viewModel.SearchQuery = "missing";
        Assert.True(viewModel.HasNoSearchResults);

        viewModel.ClearSearchCommand.Execute(null);

        Assert.Single(viewModel.FolderLibraryItems);
        Assert.Equal(2, viewModel.ImageLibraryItems.Count);
        Assert.False(viewModel.HasNoSearchResults);
    }

    [Fact]
    public async Task Empty_folder_has_a_distinct_design_state()
    {
        using var workspace = new TempDirectory();
        var viewModel = CreateViewModel(workspace.Path, new CountingFolderScanner([]));

        await viewModel.InitializeAsync();

        Assert.True(viewModel.HasEmptyFolder);
        Assert.False(viewModel.HasNoSearchResults);
        Assert.False(viewModel.HasLibraryError);
    }

    [Fact]
    public async Task ConvertVisibleCommand_uses_search_filtered_images()
    {
        using var workspace = new TempDirectory();
        var alpha = new ImageListItem(Path.Combine(workspace.Path, "Alpha.png"), "Alpha.png", ".png", 20, 1024);
        var bravo = new ImageListItem(Path.Combine(workspace.Path, "Bravo.jpg"), "Bravo.jpg", ".jpg", 30, 2048);
        var fileOperations = new RecordingFileOperationService();
        var viewModel = CreateViewModel(
            workspace.Path,
            new CountingFolderScanner([alpha, bravo]),
            fileOperations);

        await viewModel.InitializeAsync();
        viewModel.SearchQuery = "bravo";
        await viewModel.ConvertVisibleCommand.ExecuteAsync(null);

        Assert.Equal([bravo.Path], fileOperations.ConvertedPaths);
    }

    [Fact]
    public async Task ConvertVisibleCommand_confirms_large_batches()
    {
        using var workspace = new TempDirectory();
        var images = Enumerable.Range(1, 50)
            .Select(index => new ImageListItem(Path.Combine(workspace.Path, $"Image-{index:000}.png"),
                $"Image-{index:000}.png",
                "png",
                index,
                1024))
            .Cast<ListItem>()
            .ToList();
        var fileOperations = new RecordingFileOperationService();
        var confirmations = new List<string>();
        var viewModel = CreateViewModel(
            workspace.Path,
            new CountingFolderScanner(images),
            fileOperations,
            new TestDialogService(confirmAsync: (message, title, confirmButtonText) =>
            {
                confirmations.Add($"{title}|{confirmButtonText}|{message}");
                return Task.FromResult(true);
            }));

        await viewModel.InitializeAsync();
        await viewModel.ConvertVisibleCommand.ExecuteAsync(null);

        Assert.Single(confirmations);
        Assert.Contains("50 張圖片", confirmations[0], StringComparison.Ordinal);
        Assert.Equal(50, fileOperations.ConvertedPaths.Count);
    }

    [Fact]
    public async Task CancelFileOperationCommand_cancels_running_convert()
    {
        using var workspace = new TempDirectory();
        var image = new ImageListItem(Path.Combine(workspace.Path, "Alpha.png"), "Alpha.png", "png", 20, 1024);
        var fileOperations = new BlockingConvertOperationService();
        var viewModel = CreateViewModel(
            workspace.Path,
            new CountingFolderScanner([image]),
            fileOperations);

        await viewModel.InitializeAsync();
        var commandTask = viewModel.ConvertVisibleCommand.ExecuteAsync(null);
        await fileOperations.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(viewModel.IsFileOperationActive);
        Assert.True(viewModel.CancelFileOperationCommand.CanExecute(null));
        Assert.Contains("正在轉換", viewModel.StatusMessage, StringComparison.Ordinal);

        viewModel.CancelFileOperationCommand.Execute(null);
        await commandTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(fileOperations.WasCanceled);
        Assert.False(viewModel.IsFileOperationActive);
        Assert.Equal("已取消轉換為 JPG。", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Selection_summary_and_detail_follow_selected_images()
    {
        using var workspace = new TempDirectory();
        var alpha = new ImageListItem(Path.Combine(workspace.Path, "Alpha.png"), "Alpha.png", ".png", 20, 1024);
        var bravo = new ImageListItem(Path.Combine(workspace.Path, "Bravo.jpg"), "Bravo.jpg", ".jpg", 30, 2048);
        var viewModel = CreateViewModel(workspace.Path, new CountingFolderScanner([alpha, bravo]));

        await viewModel.InitializeAsync();

        Assert.Equal("尚未選取", viewModel.SelectedSummaryText);
        viewModel.UpdateSelectedLibraryItems([viewModel.LibraryItems[0]]);
        Assert.Equal("1 張已選取", viewModel.SelectedSummaryText);

        viewModel.UpdateSelectedLibraryItems(viewModel.LibraryItems);

        Assert.Equal("2 張已選取", viewModel.SelectedSummaryText);
    }

    [Fact]
    public void Sidebar_and_view_mode_commands_toggle_state()
    {
        var viewModel = CreateViewModel();

        viewModel.ToggleSidebarCommand.Execute(null);
        Assert.False(viewModel.IsSidebarOpen);

        viewModel.SetViewModeCommand.Execute("list");
        Assert.False(viewModel.IsGridViewMode);
        Assert.True(viewModel.IsListViewMode);

        viewModel.SetViewModeCommand.Execute("grid");
        Assert.True(viewModel.IsGridViewMode);
    }

    private static MainPageViewModel CreateViewModel() =>
        new(
            new FakeSettingsStore(AppSettings.CreateDefault()),
            new CountingFolderScanner([]),
            new ThrowingFileOperationService(),
            new TestThumbnailService(),
            new TestDialogService());

    private static MainPageViewModel CreateViewModel(
        string lastFolderPath,
        IFolderScanner folderScanner,
        IFileOperationService? fileOperationService = null,
        IDialogService? dialogService = null) =>
        new(
            new FakeSettingsStore(AppSettings.CreateDefault() with { LastFolderPath = lastFolderPath }),
            folderScanner,
            fileOperationService ?? new ThrowingFileOperationService(),
            new TestThumbnailService(),
            dialogService ?? new TestDialogService());

    private sealed class RecordingFileOperationService : IFileOperationService
    {
        public IReadOnlyList<string> ConvertedPaths { get; private set; } = [];

        public Task<FileOperationBatchResult> ConvertVisibleToJpgAsync(
            IEnumerable<ImageListItem> visibleImages,
            CancellationToken cancellationToken = default)
        {
            ConvertedPaths = visibleImages.Select(image => image.Path).ToArray();
            return Task.FromResult(new FileOperationBatchResult(
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

    private sealed class BlockingConvertOperationService : IFileOperationService
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool WasCanceled { get; private set; }

        public async Task<FileOperationBatchResult> ConvertVisibleToJpgAsync(
            IEnumerable<ImageListItem> visibleImages,
            CancellationToken cancellationToken = default)
        {
            Started.SetResult();
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                WasCanceled = true;
                throw;
            }

            throw new InvalidOperationException("The test should cancel before conversion completes.");
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
}
