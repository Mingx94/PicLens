using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace PicLens.Converters;

public sealed class ImagePathConverter : IValueConverter
{
    private const int MaxCachedImages = 512;
    private static readonly Dictionary<string, CachedImage> Cache = new(StringComparer.Ordinal);

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
        if (Cache.TryGetValue(key, out var cached) && cached.Stamp == stamp)
        {
            return cached.Bitmap;
        }

        if (Cache.Count >= MaxCachedImages)
        {
            // ponytail: blunt cap; use LRU only if thumbnail browsing proves this too wasteful.
            Cache.Clear();
        }

        var bitmap = new Bitmap(path);
        Cache[key] = new CachedImage(stamp, bitmap);
        return bitmap;
    }

    private sealed record CachedImage(ImageStamp Stamp, Bitmap Bitmap);

    private readonly record struct ImageStamp(long Length, DateTime LastWriteTimeUtc);
}
