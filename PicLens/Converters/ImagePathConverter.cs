using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace PicLens.Converters;

public sealed class ImagePathConverter : IValueConverter
{
    private const int MaxCachedImages = 512;
    private static readonly object Sync = new();
    private static readonly Dictionary<string, CachedImage> Cache = new(StringComparer.Ordinal);
    private static readonly Queue<string> CacheKeys = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            return string.Equals(parameter?.ToString(), "cache", StringComparison.Ordinal)
                ? CachedBitmap(path)
                : new Bitmap(path);
        }
        catch
        {
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static Bitmap CachedBitmap(string path)
    {
        var info = new FileInfo(path);
        var key = System.IO.Path.GetFullPath(path);
        var stamp = new ImageStamp(info.Length, info.LastWriteTimeUtc);
        lock (Sync)
        {
            if (Cache.TryGetValue(key, out var cached) && cached.Stamp == stamp)
            {
                return cached.Bitmap;
            }
        }

        var bitmap = new Bitmap(path);
        lock (Sync)
        {
            if (Cache.TryGetValue(key, out var old))
            {
                old.Bitmap.Dispose();
            }
            else
            {
                CacheKeys.Enqueue(key);
            }

            Cache[key] = new CachedImage(stamp, bitmap);
            TrimCache();
        }

        return bitmap;
    }

    public static void ClearCache()
    {
        lock (Sync)
        {
            foreach (var cached in Cache.Values)
            {
                cached.Bitmap.Dispose();
            }

            Cache.Clear();
            CacheKeys.Clear();
        }
    }

    private static void TrimCache()
    {
        while (Cache.Count > MaxCachedImages && CacheKeys.TryDequeue(out var key))
        {
            if (Cache.Remove(key, out var cached))
            {
                cached.Bitmap.Dispose();
            }
        }
    }

    private sealed record CachedImage(ImageStamp Stamp, Bitmap Bitmap);

    private readonly record struct ImageStamp(long Length, DateTime LastWriteTimeUtc);
}
