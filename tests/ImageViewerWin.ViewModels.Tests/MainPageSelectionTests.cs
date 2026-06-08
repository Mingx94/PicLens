using ImageViewerWin.Application.Services;
using ImageViewerWin.Core.Models;
using ImageViewerWin.ViewModels;

namespace ImageViewerWin.ViewModels.Tests;

public sealed class MainPageSelectionTests
{
    [Fact]
    public void Selection_state_defaults_to_empty()
    {
        var viewModel = CreateViewModel();

        Assert.Equal(0, viewModel.SelectedImageCount);
        Assert.False(viewModel.HasSelectedImages);
        Assert.False(viewModel.HasSingleSelectedImage);
        Assert.Equal("未選取圖片", viewModel.SelectionSummaryText);
        Assert.False(viewModel.ConvertSelectedCommand.CanExecute(null));
        Assert.False(viewModel.RenameSelectedCommand.CanExecute(null));
        Assert.False(viewModel.TrashSelectedCommand.CanExecute(null));
    }

    [Fact]
    public void Selection_state_tracks_one_selected_image()
    {
        var viewModel = CreateViewModel();
        var image = ImageTile("a.jpg", @"C:\Album\a.jpg");

        viewModel.UpdateSelectedLibraryItems([image]);

        Assert.Equal(1, viewModel.SelectedImageCount);
        Assert.True(viewModel.HasSelectedImages);
        Assert.True(viewModel.HasSingleSelectedImage);
        Assert.Equal("已選 1 張圖片", viewModel.SelectionSummaryText);
        Assert.True(viewModel.ConvertSelectedCommand.CanExecute(null));
        Assert.True(viewModel.RenameSelectedCommand.CanExecute(null));
        Assert.True(viewModel.TrashSelectedCommand.CanExecute(null));
    }

    [Fact]
    public void Selection_state_tracks_multiple_selected_images_without_enabling_single_image_commands()
    {
        var viewModel = CreateViewModel();

        viewModel.UpdateSelectedLibraryItems(
        [
            ImageTile("a.jpg", @"C:\Album\a.jpg"),
            ImageTile("b.png", @"C:\Album\b.png")
        ]);

        Assert.Equal(2, viewModel.SelectedImageCount);
        Assert.True(viewModel.HasSelectedImages);
        Assert.False(viewModel.HasSingleSelectedImage);
        Assert.Equal("已選 2 張圖片", viewModel.SelectionSummaryText);
        Assert.True(viewModel.ConvertSelectedCommand.CanExecute(null));
        Assert.False(viewModel.RenameSelectedCommand.CanExecute(null));
        Assert.False(viewModel.TrashSelectedCommand.CanExecute(null));
    }

    [Fact]
    public void Selection_state_ignores_selected_folders()
    {
        var viewModel = CreateViewModel();

        viewModel.UpdateSelectedLibraryItems(
        [
            FolderTile("Nested", @"C:\Album\Nested"),
            ImageTile("a.jpg", @"C:\Album\a.jpg")
        ]);

        Assert.Equal(1, viewModel.SelectedImageCount);
        Assert.True(viewModel.HasSelectedImages);
        Assert.True(viewModel.HasSingleSelectedImage);
        Assert.Equal("已選 1 張圖片", viewModel.SelectionSummaryText);
        Assert.True(viewModel.ConvertSelectedCommand.CanExecute(null));
    }

    [Fact]
    public void ClearSelectedLibraryItems_resets_selection_state()
    {
        var viewModel = CreateViewModel();
        viewModel.UpdateSelectedLibraryItems([ImageTile("a.jpg", @"C:\Album\a.jpg")]);

        viewModel.ClearSelectedLibraryItems();

        Assert.Equal(0, viewModel.SelectedImageCount);
        Assert.False(viewModel.HasSelectedImages);
        Assert.False(viewModel.HasSingleSelectedImage);
        Assert.Equal("未選取圖片", viewModel.SelectionSummaryText);
        Assert.False(viewModel.ConvertSelectedCommand.CanExecute(null));
        Assert.False(viewModel.RenameSelectedCommand.CanExecute(null));
        Assert.False(viewModel.TrashSelectedCommand.CanExecute(null));
    }

    [Fact]
    public async Task RefreshLibraryCommand_clears_selection_when_current_folder_is_unavailable()
    {
        var viewModel = CreateViewModel();
        viewModel.CurrentFolderPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        viewModel.UpdateSelectedLibraryItems([ImageTile("a.jpg", @"C:\Album\a.jpg")]);

        await viewModel.RefreshLibraryCommand.ExecuteAsync(null);

        Assert.Equal(0, viewModel.SelectedImageCount);
        Assert.False(viewModel.HasSelectedImages);
        Assert.False(viewModel.HasSingleSelectedImage);
        Assert.Equal("未選取圖片", viewModel.SelectionSummaryText);
        Assert.False(viewModel.ConvertSelectedCommand.CanExecute(null));
        Assert.False(viewModel.RenameSelectedCommand.CanExecute(null));
        Assert.False(viewModel.TrashSelectedCommand.CanExecute(null));
    }

    private static MainPageViewModel CreateViewModel() =>
        new(
            new ThrowingSettingsStore(),
            new ThrowingFolderScanner(),
            new ThrowingFileOperationService(),
            new NullThumbnailService(),
            () => Task.FromResult<string?>(null),
            (_, _, _) => Task.FromResult(false),
            _ => Task.FromResult<string?>(null),
            _ => { });

    private static LibraryTileItem ImageTile(string name, string path) =>
        new(
            Name: name,
            Path: path,
            Detail: "JPG - 1 KB",
            IsFolder: false,
            IsSelected: false,
            IsAnimated: false,
            IconGlyph: "\uEB9F",
            SourceItem: new ImageListItem($"image:{name}", path, name, Path.GetExtension(name), 1, 1024));

    private static LibraryTileItem FolderTile(string name, string path) =>
        new(
            Name: name,
            Path: path,
            Detail: "開啟資料夾",
            IsFolder: true,
            IsSelected: false,
            IsAnimated: false,
            IconGlyph: "\uE8B7",
            SourceItem: new FolderListItem($"folder:{name}", path, name, 1));

    private sealed class ThrowingSettingsStore : ISettingsStore
    {
        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AppSettings> UpdateAsync(AppSettingsPatch patch, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class ThrowingFolderScanner : IFolderScanner
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
}
