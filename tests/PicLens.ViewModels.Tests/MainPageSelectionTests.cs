using PicLens.Core.Models;
using PicLens.ViewModels;

namespace PicLens.ViewModels.Tests;

public sealed class MainPageSelectionTests
{
    [Fact]
    public void Selection_state_defaults_to_empty()
    {
        var viewModel = CreateViewModel();

        Assert.Equal(0, viewModel.SelectedImageCount);
        Assert.False(viewModel.HasSelectedImages);
        Assert.False(viewModel.HasSingleSelectedImage);
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
        Assert.False(viewModel.RenameSelectedCommand.CanExecute(null));
        Assert.True(viewModel.TrashSelectedCommand.CanExecute(null));
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
        Assert.False(viewModel.RenameSelectedCommand.CanExecute(null));
        Assert.False(viewModel.TrashSelectedCommand.CanExecute(null));
    }

    private static MainPageViewModel CreateViewModel() =>
        new(
            new FakeSettingsStore(AppSettings.CreateDefault()),
            new CountingFolderScanner([]),
            new ThrowingFileOperationService(),
            new TestThumbnailService(),
            new TestDialogService());

    private static LibraryTileItem ImageTile(string name, string path) =>
        new(new ImageListItem(path, name, Path.GetExtension(name), 1, 1024));

    private static LibraryTileItem FolderTile(string name, string path) =>
        new(new FolderListItem(path, name, 1));

}
