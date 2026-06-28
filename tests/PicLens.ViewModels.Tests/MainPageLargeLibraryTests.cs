using System.Collections.Specialized;
using PicLens.Core.Models;
using PicLens.ViewModels;

namespace PicLens.ViewModels.Tests;

public sealed class MainPageLargeLibraryTests
{
    [Fact]
    public async Task InitializeAsync_replaces_large_library_with_single_collection_reset()
    {
        using var workspace = new TempDirectory();
        var images = Enumerable.Range(1, 2000)
            .Select(index => new ImageListItem(
                $"image:{index}",
                Path.Combine(workspace.Path, $"image-{index:0000}.jpg"),
                $"image-{index:0000}.jpg",
                "jpg",
                index,
                1024))
            .Cast<ListItem>()
            .ToList();
        var viewModel = new MainPageViewModel(
            new FakeSettingsStore(AppSettings.CreateDefault() with { LastFolderPath = workspace.Path }),
            new CountingFolderScanner(images),
            new ThrowingFileOperationService(),
            new NullThumbnailService(),
            new NullDialogService());
        var changeCount = 0;
        NotifyCollectionChangedAction? action = null;
        viewModel.LibraryItems.CollectionChanged += (_, e) =>
        {
            changeCount += 1;
            action = e.Action;
        };

        await viewModel.InitializeAsync();

        Assert.Equal(2000, viewModel.LibraryItems.Count);
        Assert.Equal(1, changeCount);
        Assert.Equal(NotifyCollectionChangedAction.Reset, action);
    }
}
