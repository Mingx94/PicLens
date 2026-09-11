using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PicLens.Services;
namespace PicLens.App;

public sealed class ThumbnailController
{
    readonly Func<string, int, CancellationToken, Task<Pixels>> loadPixels;
    readonly Profile profile;
    public ThumbnailController(ImageService images, Profile profile) : this(images.LoadAsync, profile) { }
    internal ThumbnailController(Func<string, int, CancellationToken, Task<Pixels>> loadPixels, Profile profile)
    {
        this.loadPixels = loadPixels; this.profile = profile;
    }
    readonly Dictionary<TileModel, CancellationTokenSource> pending = [];
    readonly LinkedList<(string Key, BitmapSource Image, long Size)> recent = [];
    readonly HashSet<TileModel> visible = [];
    long bytes; int generation; bool paused;
    public int PendingCount => pending.Count;
    public int VisibleCount => visible.Count;
    public void Visible(TileModel tile)
    {
        visible.Add(tile);
        if (paused || tile.IsFolder || tile.Entry.Animated || tile.Image is not null || pending.ContainsKey(tile)) return;
        var cached = recent.FirstOrDefault(x => x.Key == tile.Path);
        if (cached.Image is not null)
        {
            recent.Remove(cached); bytes -= cached.Size; tile.Error = null; tile.Image = cached.Image; return;
        }
        var ct = new CancellationTokenSource(); pending.Add(tile, ct);
        _ = Load(tile, ct, generation);
    }
    async Task Load(TileModel tile, CancellationTokenSource ct, int epoch)
    {
        bool IsCurrent() => !ct.IsCancellationRequested && epoch == generation && visible.Contains(tile) &&
            pending.TryGetValue(tile, out var current) && ReferenceEquals(current, ct);
        try
        {
            var image = await Task.Run(async () => MakeBitmap(await loadPixels(tile.Path, 256, ct.Token)), ct.Token);
            if (IsCurrent()) { tile.Error = null; tile.Image = image; }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (IsCurrent()) tile.Error = ex.Message; profile.Log($"縮圖失敗 {tile.Path}", ex); }
        finally { if (pending.TryGetValue(tile, out var current) && current == ct) pending.Remove(tile); ct.Dispose(); }
    }
    public void Hidden(TileModel tile)
    {
        visible.Remove(tile); if (pending.Remove(tile, out var ct)) ct.Cancel();
        if (tile.Image is { } image)
        {
            long size = (long)image.PixelWidth * image.PixelHeight * 4;
            recent.AddFirst((tile.Path, image, size)); bytes += size; tile.Image = null;
            while (bytes > 32 * 1024 * 1024 || recent.Count > 256) { bytes -= recent.Last!.Value.Size; recent.RemoveLast(); }
        }
    }
    public void Pause(bool value)
    {
        paused = value;
        foreach (var ct in pending.Values) ct.Cancel(); pending.Clear();
        if (!paused) foreach (var tile in visible.ToArray()) Visible(tile);
    }
    public void Reset()
    {
        generation++; foreach (var ct in pending.Values) ct.Cancel(); pending.Clear();
        foreach (var tile in visible) tile.Image = null;
        visible.Clear(); recent.Clear(); bytes = 0;
    }
    public static BitmapSource MakeBitmap(Pixels pixels)
    {
        var bitmap = BitmapSource.Create(pixels.Width, pixels.Height, 96, 96, PixelFormats.Bgra32, null, pixels.Bytes, pixels.Stride);
        bitmap.Freeze(); return bitmap;
    }
}
