using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using PicLens.App;
using PicLens.App.Controls;
using PicLens.Services;
namespace PicLens.Tests;

public sealed class WpfTests
{
    [Fact]
    public Task ThumbnailRetryClearsFailureIncludingCacheHit() => StaAsync(async () =>
    {
        using var f = new Fixture(); var profile = new Profile(Path.Combine(f.Root, "profile"));
        int attempts = 0;
        var controller = new ThumbnailController((_, _, _) => Interlocked.Increment(ref attempts) == 1
            ? Task.FromException<Pixels>(new IOException("暫時無法讀取"))
            : Task.FromResult(new Pixels(1, 1, 4, new byte[4])), profile);
        var tile = new TileModel(new("retry.png", "retry.png", false, 0, 0));
        controller.Visible(tile); await Until(() => controller.PendingCount == 0 && tile.Error is not null);
        controller.Hidden(tile); controller.Visible(tile);
        await Until(() => tile.Image is not null); Assert.Null(tile.Error);
        controller.Hidden(tile); tile.Error = "過期錯誤"; controller.Visible(tile);
        Assert.NotNull(tile.Image); Assert.Null(tile.Error); Assert.Equal(2, attempts);
        controller.Reset();
    });
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public Task CanceledThumbnailCannotAlterReplacement(bool finishNewFirst, bool oldFails) => StaAsync(async () =>
    {
        using var f = new Fixture(); var profile = new Profile(Path.Combine(f.Root, "profile"));
        var old = new TaskCompletionSource<Pixels>(TaskCreationOptions.RunContinuationsAsynchronously);
        var next = new TaskCompletionSource<Pixels>(TaskCreationOptions.RunContinuationsAsynchronously);
        int started = 0;
        var controller = new ThumbnailController((_, _, _) => Interlocked.Increment(ref started) == 1 ? old.Task : next.Task, profile);
        var tile = new TileModel(new("race.png", "race.png", false, 0, 0));
        controller.Visible(tile); await Until(() => Volatile.Read(ref started) == 1);
        controller.Hidden(tile); controller.Visible(tile); await Until(() => Volatile.Read(ref started) == 2);
        if (finishNewFirst) { next.SetResult(new Pixels(3, 1, 12, new byte[12])); await Until(() => tile.Image is not null); }
        // This decoder deliberately ignores cancellation to reproduce a late completion.
        if (oldFails) old.SetException(new IOException("過期解碼失敗"));
        else old.SetResult(new Pixels(1, 1, 4, new byte[4]));
        if (oldFails) await Until(() => File.Exists(profile.LogPath) && File.ReadAllText(profile.LogPath).Contains("過期解碼失敗"));
        else await Task.Delay(100);
        Assert.Null(tile.Error);
        Assert.Equal(finishNewFirst ? 0 : 1, controller.PendingCount);
        if (!finishNewFirst) { next.SetResult(new Pixels(3, 1, 12, new byte[12])); await Until(() => tile.Image is not null); }
        Assert.Equal(3, tile.Image!.PixelWidth); Assert.Null(tile.Error);
        controller.Reset();
    });
    [Fact]
    public Task SettingsFailureStaysVisibleAndRetryPersistsLatestValues() => StaAsync(async () =>
    {
        using var f = new Fixture(); var profile = new Profile(Path.Combine(f.Root, "profile")); profile.Save(new());
        await using var pool = new WorkerPool(profile, Fixture.Worker); await using var images = new ImageService(profile, pool);
        var model = new LibraryViewModel(profile, images); var dispatcher = Dispatcher.CurrentDispatcher;
        bool wrongThread = false; model.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(model.SettingsSaveError) && !dispatcher.CheckAccess()) wrongThread = true; };
        string original = File.ReadAllText(profile.SettingsPath);
        using (var locked = new FileStream(profile.SettingsPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
            model.ThumbnailSize = 180; await model.FlushSettingsAsync();
            Assert.Contains("尚未儲存", model.SettingsSaveError);
            model.Status = "正在讀取資料夾…"; Assert.NotNull(model.SettingsSaveError);
            model.ThumbnailSize = 220; model.SidebarCollapsed = true;
            await model.FlushSettingsAsync(); Assert.NotNull(model.SettingsSaveError);
            Assert.Equal(original, File.ReadAllText(profile.SettingsPath));
        }
        await model.RetrySettingsSaveAsync();
        Assert.Null(model.SettingsSaveError); Assert.False(wrongThread);
        var saved = profile.Load(); Assert.Equal(220, saved.ThumbnailSize); Assert.True(saved.SidebarCollapsed);
        Assert.Empty(Directory.GetFiles(profile.Root, "*.tmp")); model.Stop();
    });
    static Task StaAsync(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.BeginInvoke(async () =>
            {
                try { await action(); completion.SetResult(); }
                catch (Exception ex) { completion.SetException(ex); }
                finally { dispatcher.InvokeShutdown(); }
            });
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return completion.Task;
    }
    static async Task Until(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(8);
        while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(15);
        Assert.True(condition(), "Dispatcher condition timed out");
    }
    [Fact]
    public Task ViewerRejectsOldRequestsAndCopiesSequence() => StaAsync(async () =>
    {
        using var f = new Fixture(); var profile = new Profile(Path.Combine(f.Root, "profile"));
        await using var pool = new WorkerPool(profile, Fixture.Worker); await using var images = new ImageService(profile, pool);
        string a = f.Image("a.png"), b = f.Image("b.png", 160, 100);
        var canvas = new ViewerCanvas(); var window = new Window { Content = canvas, Width = 600, Height = 400, ShowInTaskbar = false };
        var viewer = new ViewerController(images, profile, canvas); int paints = 0; viewer.SharpPaint += _ => paints++;
        window.Show();
        try
        {
            var entries = new List<PicLens.Core.LibraryEntry> { new(a, "a.png", false, 0, 0), new(b, "b.png", false, 0, 0) };
            viewer.Open(entries, a); entries.Clear(); viewer.Navigate(1); viewer.Navigate(-1);
            await Until(() => paints > 0); Assert.Equal(a, viewer.CurrentPath); Assert.True(canvas.Original);
            viewer.Close(); viewer.Open([new(b, "b.png", false, 0, 0)], b);
            await Until(() => paints > 1); Assert.Equal(b, viewer.CurrentPath);
            viewer.Close(); await Task.Delay(150); Assert.False(canvas.Original); Assert.False(viewer.IsOpen);
        }
        finally { viewer.Close(); window.Close(); }
    });
    [Fact]
    public Task SearchAndNavigationPreservePickerRootAndResetOnce() => StaAsync(async () =>
    {
        using var f = new Fixture(); string source = f.Image("library/a.png"); f.Image("library/child/b.png");
        string root = Path.GetDirectoryName(source)!; var profile = new Profile(Path.Combine(f.Root, "profile"));
        await using var pool = new WorkerPool(profile, Fixture.Worker); await using var images = new ImageService(profile, pool);
        var model = new LibraryViewModel(profile, images);
        try
        {
            await model.Pick(root); int resets = 0; model.Items.CollectionChanged += (_, e) => { if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset) resets++; };
            model.Search = "a.png"; Assert.Equal(1, resets); Assert.Single(model.Items);
            File.Delete(source); model.Search = ""; Assert.Contains(model.Items, t => t.Path == source); // Filtering must not rescan.
            await model.Navigate(Path.Combine(root, "child")); Assert.Equal(root, model.RootPath);
            await model.FlushSettingsAsync(); Assert.Equal(root, profile.Load().LastFolderPath);
            await model.Pick(Path.Combine(root, "child"), persist: false);
            Assert.Equal(Path.Combine(root, "child"), model.RootPath);
            await model.FlushSettingsAsync(); Assert.Equal(root, profile.Load().LastFolderPath);
            Assert.Empty(model.Selection.Ordered);
        }
        finally { model.Stop(); await model.FlushSettingsAsync(); }
    });
    static Task Sta(Action action)
    {
        var task = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => { try { action(); task.SetResult(); } catch (Exception ex) { task.SetException(ex); } finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return task.Task;
    }
    [Fact]
    public Task TenThousandItemsKeepBoundedContainers() => Sta(() =>
    {
        var list = new ListBox { ItemsSource = Enumerable.Range(0, 10000).ToList(), Width = 800, Height = 600 };
        var factory = new FrameworkElementFactory(typeof(VirtualizingTilePanel));
        list.ItemsPanel = new ItemsPanelTemplate(factory); ScrollViewer.SetCanContentScroll(list, true);
        var window = new Window { Content = list, Width = 820, Height = 640, ShowInTaskbar = false };
        window.Show(); window.UpdateLayout();
        try
        {
            var panel = MainWindow.FindVisual<VirtualizingTilePanel>(list)!; Assert.NotNull(panel);
            Assert.InRange(panel.RealizedCount, 1, 24);
            panel.SetVerticalOffset(200000); window.UpdateLayout(); Assert.InRange(panel.RealizedCount, 1, 24);
            panel.SetVerticalOffset(0); window.UpdateLayout(); Assert.InRange(panel.RealizedCount, 1, 24);
            list.ItemsSource = Array.Empty<int>(); window.UpdateLayout(); Assert.Equal(0, panel.RealizedCount);
        }
        finally { window.Close(); }
    });
    [Fact]
    public Task LargeImagesAreTiledWithoutDownsampling() => Sta(() =>
    {
        var image = PreparedImage.Create(new Pixels(5000, 2, 20000, new byte[40000]));
        Assert.Equal(5000, image.Width); Assert.Equal(3, image.Tiles.Count);
        Assert.Equal(2049, image.Tiles[0].Image.PixelWidth);
    });
    [Fact]
    public void LaunchOptionsRejectUnknownOrInvalidArguments()
    {
        Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--smoke-ms", "-1"]));
        Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--unknown"]));
        Assert.Equal(800, LaunchOptions.Parse(["--width", "500"]).Width);
    }
}
