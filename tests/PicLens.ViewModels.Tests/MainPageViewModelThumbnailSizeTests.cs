using PicLens.Core.Services;
using PicLens.Core.Domain;
using PicLens.Core.Models;
using PicLens.ViewModels;

namespace PicLens.ViewModels.Tests;

public sealed class MainPageViewModelThumbnailSizeTests
{
    [Fact]
    public async Task InitializeAsync_clamps_oversized_persisted_thumbnail_size_to_tiles()
    {
        using var workspace = new TempDirectory();
        var scanner = new CountingFolderScanner(
        [
            new ImageListItem("image:first", Path.Combine(workspace.Path, "first.jpg"), "first.jpg", "jpg", 100, 1024)
        ]);
        var settingsStore = new FakeSettingsStore(AppSettings.CreateDefault() with
        {
            LastFolderPath = workspace.Path,
            ThumbnailSize = 350
        });
        var viewModel = CreateViewModel(settingsStore, scanner);

        await viewModel.InitializeAsync();

        var tile = Assert.Single(viewModel.LibraryItems);
        Assert.Equal(200, viewModel.ThumbnailSize);
        Assert.Equal(200, tile.TileWidth);
        Assert.Equal(196, tile.TileHeight);
    }

    [Fact]
    public async Task ChangeThumbnailSizeAsync_persists_size_and_updates_existing_tiles()
    {
        using var workspace = new TempDirectory();
        var scanner = new CountingFolderScanner(
        [
            new ImageListItem("image:first", Path.Combine(workspace.Path, "first.jpg"), "first.jpg", "jpg", 100, 1024)
        ]);
        var settingsStore = new FakeSettingsStore(AppSettings.CreateDefault() with
        {
            LastFolderPath = workspace.Path
        });
        var viewModel = CreateViewModel(settingsStore, scanner);

        await viewModel.InitializeAsync();
        var tile = Assert.Single(viewModel.LibraryItems);

        await viewModel.ChangeThumbnailSizeAsync(188);

        Assert.Equal(180, viewModel.ThumbnailSize);
        Assert.Equal(180, settingsStore.Settings.ThumbnailSize);
        Assert.Equal(180, tile.TileWidth);
        Assert.Equal(176, tile.TileHeight);
        Assert.Equal("縮圖大小已調整為 180。", viewModel.StatusMessage);
    }

    [Fact]
    public async Task LoadThumbnailAsync_updates_still_image_tile_with_cached_thumbnail_path()
    {
        using var workspace = new TempDirectory();
        var imagePath = Path.Combine(workspace.Path, "first.jpg");
        var cachedThumbnailPath = Path.Combine(workspace.Path, "thumbs", "first.png");
        var scanner = new CountingFolderScanner(
        [
            new ImageListItem("image:first", imagePath, "first.jpg", "jpg", 100, 1024)
        ]);
        var settingsStore = new FakeSettingsStore(AppSettings.CreateDefault() with
        {
            LastFolderPath = workspace.Path,
            ThumbnailSize = 350
        });
        var thumbnailService = new TestThumbnailService((_, _, _) => Task.FromResult<string?>(cachedThumbnailPath));
        var viewModel = CreateViewModel(settingsStore, scanner, thumbnailService);

        await viewModel.InitializeAsync();
        var tile = Assert.Single(viewModel.LibraryItems);

        Assert.Null(tile.ThumbnailPath);

        await viewModel.LoadThumbnailAsync(tile);

        Assert.Equal(cachedThumbnailPath, tile.ThumbnailPath);
        Assert.True(tile.CanShowThumbnail);
        Assert.False(tile.ShouldShowIcon);
        Assert.Equal([(imagePath, 200)], thumbnailService.Requests);
    }

    [Fact]
    public async Task LoadThumbnailAsync_dispatches_thumbnail_property_update_when_not_on_ui_thread()
    {
        using var workspace = new TempDirectory();
        var imagePath = Path.Combine(workspace.Path, "first.jpg");
        var cachedThumbnailPath = Path.Combine(workspace.Path, "thumbs", "first.png");
        var scanner = new CountingFolderScanner(
        [
            new ImageListItem("image:first", imagePath, "first.jpg", "jpg", 100, 1024)
        ]);
        var settingsStore = new FakeSettingsStore(AppSettings.CreateDefault() with
        {
            LastFolderPath = workspace.Path
        });
        var thumbnailService = new TestThumbnailService((_, _, _) => Task.FromResult<string?>(cachedThumbnailPath));
        var enqueuedActions = new Queue<Action>();
        var viewModel = CreateViewModel(
            settingsStore,
            scanner,
            thumbnailService,
            hasUiThreadAccess: () => false,
            tryEnqueueOnUiThread: action =>
            {
                enqueuedActions.Enqueue(action);
                return true;
            });

        await viewModel.InitializeAsync();
        var tile = Assert.Single(viewModel.LibraryItems);

        var loadTask = viewModel.LoadThumbnailAsync(tile);

        var enqueued = Assert.Single(enqueuedActions);
        Assert.Null(tile.ThumbnailPath);

        enqueued();
        await loadTask;

        Assert.Equal(cachedThumbnailPath, tile.ThumbnailPath);
        Assert.True(tile.CanShowThumbnail);
        Assert.False(tile.ShouldShowIcon);
    }

    [Fact]
    public async Task CancelThumbnailLoad_cancels_pending_request_without_applying_thumbnail_path()
    {
        using var workspace = new TempDirectory();
        var imagePath = Path.Combine(workspace.Path, "first.jpg");
        var scanner = new CountingFolderScanner(
        [
            new ImageListItem("image:first", imagePath, "first.jpg", "jpg", 100, 1024)
        ]);
        var settingsStore = new FakeSettingsStore(AppSettings.CreateDefault() with
        {
            LastFolderPath = workspace.Path
        });
        var thumbnailService = new BlockingThumbnailService(Path.Combine(workspace.Path, "thumb.png"));
        var viewModel = CreateViewModel(settingsStore, scanner, thumbnailService);

        await viewModel.InitializeAsync();
        var tile = Assert.Single(viewModel.LibraryItems);
        var loadTask = viewModel.LoadThumbnailAsync(tile);
        await thumbnailService.WaitForRequestAsync();

        viewModel.CancelThumbnailLoad(tile);
        await loadTask;
        await thumbnailService.WaitForCancellationAsync();

        Assert.True(thumbnailService.WasCanceled);
        Assert.Null(tile.ThumbnailPath);
    }

    [Fact]
    public async Task ChangeThumbnailSizeAsync_prevents_pending_old_size_result_from_updating_tile()
    {
        using var workspace = new TempDirectory();
        var imagePath = Path.Combine(workspace.Path, "first.jpg");
        var scanner = new CountingFolderScanner(
        [
            new ImageListItem("image:first", imagePath, "first.jpg", "jpg", 100, 1024)
        ]);
        var settingsStore = new FakeSettingsStore(AppSettings.CreateDefault() with
        {
            LastFolderPath = workspace.Path
        });
        var thumbnailService = new BlockingThumbnailService(Path.Combine(workspace.Path, "old-size.png"));
        var viewModel = CreateViewModel(settingsStore, scanner, thumbnailService);

        await viewModel.InitializeAsync();
        var tile = Assert.Single(viewModel.LibraryItems);
        var loadTask = viewModel.LoadThumbnailAsync(tile);
        await thumbnailService.WaitForRequestAsync();

        await viewModel.ChangeThumbnailSizeAsync(188);
        await loadTask;
        await thumbnailService.WaitForCancellationAsync();

        Assert.Equal(180, viewModel.ThumbnailSize);
        Assert.True(thumbnailService.WasCanceled);
        Assert.Null(tile.ThumbnailPath);
    }

    [Fact]
    public async Task LoadThumbnailAsync_keeps_existing_pending_request_for_same_tile_and_size()
    {
        using var workspace = new TempDirectory();
        var imagePath = Path.Combine(workspace.Path, "first.jpg");
        var scanner = new CountingFolderScanner(
        [
            new ImageListItem("image:first", imagePath, "first.jpg", "jpg", 100, 1024)
        ]);
        var settingsStore = new FakeSettingsStore(AppSettings.CreateDefault() with
        {
            LastFolderPath = workspace.Path
        });
        var thumbnailService = new BlockingThumbnailService(Path.Combine(workspace.Path, "thumb.png"));
        var viewModel = CreateViewModel(settingsStore, scanner, thumbnailService);

        await viewModel.InitializeAsync();
        var tile = Assert.Single(viewModel.LibraryItems);
        var firstLoadTask = viewModel.LoadThumbnailAsync(tile);
        await thumbnailService.WaitForRequestAsync();

        await viewModel.LoadThumbnailAsync(tile).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, thumbnailService.RequestCount);

        viewModel.CancelThumbnailLoad(tile);
        await firstLoadTask;
        await thumbnailService.WaitForCancellationAsync();
    }

    [Fact]
    public async Task LoadThumbnailAsync_times_out_stalled_thumbnail_slots_so_later_tiles_continue()
    {
        using var workspace = new TempDirectory();
        var images = Enumerable.Range(1, 5)
            .Select(index => new ImageListItem(
                $"image:{index}",
                Path.Combine(workspace.Path, $"image-{index}.jpg"),
                $"image-{index}.jpg",
                "jpg",
                index,
                1024))
            .Cast<ListItem>()
            .ToList();
        var fifthThumbnailPath = Path.Combine(workspace.Path, "thumbs", "image-5.png");
        var scanner = new CountingFolderScanner(images);
        var settingsStore = new FakeSettingsStore(AppSettings.CreateDefault() with
        {
            LastFolderPath = workspace.Path
        });
        var thumbnailService = new FirstRequestsStallThumbnailService(4, fifthThumbnailPath);
        var viewModel = CreateViewModel(
            settingsStore,
            scanner,
            thumbnailService,
            thumbnailLoadTimeout: TimeSpan.FromMilliseconds(50));

        await viewModel.InitializeAsync();

        var stalledTasks = viewModel.LibraryItems.Take(4).Select(viewModel.LoadThumbnailAsync).ToList();
        await thumbnailService.WaitForStalledRequestsAsync(4);
        var fifthTask = viewModel.LoadThumbnailAsync(viewModel.LibraryItems[4]);

        await Task.WhenAll(stalledTasks.Append(fifthTask)).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(fifthThumbnailPath, viewModel.LibraryItems[4].ThumbnailPath);
        Assert.Equal(5, thumbnailService.RequestCount);
    }

    private static MainPageViewModel CreateViewModel(
        ISettingsStore settingsStore,
        IFolderScanner scanner,
        IThumbnailService? thumbnailService = null,
        TimeSpan? thumbnailLoadTimeout = null,
        Func<bool>? hasUiThreadAccess = null,
        Func<Action, bool>? tryEnqueueOnUiThread = null) =>
        new(
            settingsStore,
            scanner,
            new ThrowingFileOperationService(),
            thumbnailService ?? new TestThumbnailService(),
            new NullDialogService(),
            hasUiThreadAccess: hasUiThreadAccess,
            tryEnqueueOnUiThread: tryEnqueueOnUiThread,
            thumbnailLoadTimeout: thumbnailLoadTimeout);

    private sealed class BlockingThumbnailService(string thumbnailPath) : IThumbnailService
    {
        private readonly TaskCompletionSource requestStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource cancellationObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int requestCount;

        public int RequestCount => Volatile.Read(ref requestCount);
        public bool WasCanceled { get; private set; }

        public Task WaitForRequestAsync() => requestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        public Task WaitForCancellationAsync() => cancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));

        public async Task<string?> GetOrCreateThumbnailAsync(
            string sourcePath,
            int requestedSize,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref requestCount);
            requestStarted.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return thumbnailPath;
            }
            catch (OperationCanceledException)
            {
                WasCanceled = true;
                cancellationObserved.TrySetResult();
                throw;
            }
        }
    }

    private sealed class FirstRequestsStallThumbnailService(int stalledRequestCount, string thumbnailPath) : IThumbnailService
    {
        private readonly TaskCompletionSource stalledRequestsReached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int requestCount;
        private int stalledRequests;

        public int RequestCount => Volatile.Read(ref requestCount);

        public Task WaitForStalledRequestsAsync(int expectedCount) =>
            stalledRequestsReached.Task.WaitAsync(TimeSpan.FromSeconds(5));

        public async Task<string?> GetOrCreateThumbnailAsync(
            string sourcePath,
            int requestedSize,
            CancellationToken cancellationToken = default)
        {
            var currentRequest = Interlocked.Increment(ref requestCount);
            if (currentRequest <= stalledRequestCount)
            {
                if (Interlocked.Increment(ref stalledRequests) == stalledRequestCount)
                {
                    stalledRequestsReached.TrySetResult();
                }

                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            return thumbnailPath;
        }
    }

}
