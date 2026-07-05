using PicLens.Core.Services;
using PicLens.Core.Domain;
using PicLens.Core.Models;
using PicLens.ViewModels;

namespace PicLens.ViewModels.Tests;

public sealed class MainPageDesignStateTests
{
    [Fact]
    public async Task Search_filters_folder_and_image_sections_and_clear_restores_items()
    {
        using var workspace = new TempDirectory();
        var nested = new FolderListItem("folder:nested", Path.Combine(workspace.Path, "Nested"), "Nested", 10);
        var alpha = new ImageListItem("image:alpha", Path.Combine(workspace.Path, "Alpha.png"), "Alpha.png", ".png", 20, 1024);
        var bravo = new ImageListItem("image:bravo", Path.Combine(workspace.Path, "Bravo.jpg"), "Bravo.jpg", ".jpg", 30, 2048);
        var viewModel = CreateViewModel(workspace.Path, new CountingFolderScanner([nested, alpha, bravo]));

        await viewModel.InitializeAsync();
        viewModel.SearchQuery = "alpha";

        Assert.Empty(viewModel.FolderLibraryItems);
        Assert.Equal(["Alpha.png"], viewModel.ImageLibraryItems.Select(item => item.Name));

        viewModel.ClearSearchCommand.Execute(null);

        Assert.Single(viewModel.FolderLibraryItems);
        Assert.Equal(2, viewModel.ImageLibraryItems.Count);
    }

    [Fact]
    public async Task ConvertVisibleCommand_uses_search_filtered_images()
    {
        using var workspace = new TempDirectory();
        var alpha = new ImageListItem("image:alpha", Path.Combine(workspace.Path, "Alpha.png"), "Alpha.png", ".png", 20, 1024);
        var bravo = new ImageListItem("image:bravo", Path.Combine(workspace.Path, "Bravo.jpg"), "Bravo.jpg", ".jpg", 30, 2048);
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
    public async Task Recent_folders_are_persisted_deduped_and_capped()
    {
        using var workspace = new TempDirectory();
        var folders = Enumerable.Range(0, 7)
            .Select(index => Path.Combine(workspace.Path, $"Folder-{index}"))
            .ToArray();
        foreach (var folder in folders)
        {
            Directory.CreateDirectory(folder);
        }

        var settings = new FakeSettingsStore(AppSettings.CreateDefault());
        var viewModel = new MainPageViewModel(
            settings,
            new CountingFolderScanner([]),
            new ThrowingFileOperationService(),
            new NullThumbnailService(),
            new NullDialogService());

        foreach (var folder in folders)
        {
            await viewModel.NavigateToFolderAsync(folder, persist: true, resetFolderTreeRoot: true);
        }

        Assert.Equal(SettingsRules.MaxRecentFolderCount, viewModel.RecentFolderPaths.Count);
        Assert.Equal(Path.GetFullPath(folders[^1]), viewModel.RecentFolderPaths[0]);

        await viewModel.NavigateToFolderAsync(folders[3], persist: true, resetFolderTreeRoot: true);

        Assert.Equal(Path.GetFullPath(folders[3]), viewModel.RecentFolderPaths[0]);
        Assert.Equal(viewModel.RecentFolderPaths.Count, viewModel.RecentFolderPaths.Distinct(SettingsRulesPathComparer()).Count());
        Assert.Equal(viewModel.RecentFolderPaths, settings.Current.RecentFolderPaths);
    }

    [Fact]
    public async Task Selection_summary_and_detail_follow_selected_images()
    {
        using var workspace = new TempDirectory();
        var alpha = new ImageListItem("image:alpha", Path.Combine(workspace.Path, "Alpha.png"), "Alpha.png", ".png", 20, 1024);
        var bravo = new ImageListItem("image:bravo", Path.Combine(workspace.Path, "Bravo.jpg"), "Bravo.jpg", ".jpg", 30, 2048);
        var viewModel = CreateViewModel(workspace.Path, new CountingFolderScanner([alpha, bravo]));

        await viewModel.InitializeAsync();

        Assert.Equal("尚未選取", viewModel.SelectedSummaryText);
        viewModel.UpdateSelectedLibraryItems([viewModel.LibraryItems[0]]);
        Assert.Equal("1 張已選取", viewModel.SelectedSummaryText);
        Assert.Contains("Alpha.png", viewModel.SelectedDetailText, StringComparison.Ordinal);
        Assert.Equal(alpha.Path, viewModel.SelectedImagePathForReveal);

        viewModel.UpdateSelectedLibraryItems(viewModel.LibraryItems);

        Assert.Equal("2 張已選取", viewModel.SelectedSummaryText);
        Assert.Null(viewModel.SelectedImagePathForReveal);
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
            new NullThumbnailService(),
            new NullDialogService());

    private static MainPageViewModel CreateViewModel(
        string lastFolderPath,
        IFolderScanner folderScanner,
        IFileOperationService? fileOperationService = null) =>
        new(
            new FakeSettingsStore(AppSettings.CreateDefault() with { LastFolderPath = lastFolderPath }),
            folderScanner,
            fileOperationService ?? new ThrowingFileOperationService(),
            new NullThumbnailService(),
            new NullDialogService());

    private static StringComparer SettingsRulesPathComparer() =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

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
}
