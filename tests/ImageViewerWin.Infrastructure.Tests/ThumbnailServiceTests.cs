using ImageViewerWin.Application.Services;
using ImageViewerWin.Infrastructure.Services;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace ImageViewerWin.Infrastructure.Tests;

public sealed class ThumbnailServiceTests
{
    [Fact]
    public async Task GetOrCreateThumbnailAsync_generates_png_under_cache_root_bounded_to_requested_size()
    {
        using var temp = TempWorkspace.Create();
        var sourcePath = await temp.WriteFileAsync("source.bmp", Bmp(width: 20, height: 10));
        var cacheRoot = Path.Combine(temp.Root, "cache");
        IThumbnailService service = new ThumbnailService(cacheRoot);

        var thumbnailPath = await service.GetOrCreateThumbnailAsync(sourcePath, requestedSize: 5);

        Assert.NotNull(thumbnailPath);
        Assert.StartsWith(cacheRoot, thumbnailPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(".png", Path.GetExtension(thumbnailPath));
        Assert.True(File.Exists(thumbnailPath));

        var dimensions = await ReadDimensionsAsync(thumbnailPath);
        Assert.InRange(dimensions.Width, 1u, 5u);
        Assert.InRange(dimensions.Height, 1u, 5u);
    }

    [Fact]
    public async Task GetOrCreateThumbnailAsync_keys_cache_by_source_metadata_and_requested_size()
    {
        using var temp = TempWorkspace.Create();
        var sourcePath = await temp.WriteFileAsync("source.bmp", Bmp(width: 20, height: 10));
        File.SetLastWriteTimeUtc(sourcePath, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        IThumbnailService service = new ThumbnailService(Path.Combine(temp.Root, "cache"));

        var firstPath = await service.GetOrCreateThumbnailAsync(sourcePath, requestedSize: 8);
        var repeatedPath = await service.GetOrCreateThumbnailAsync(sourcePath, requestedSize: 8);
        var differentSizePath = await service.GetOrCreateThumbnailAsync(sourcePath, requestedSize: 12);

        File.SetLastWriteTimeUtc(sourcePath, new DateTime(2026, 1, 1, 0, 0, 2, DateTimeKind.Utc));
        var changedSourcePath = await service.GetOrCreateThumbnailAsync(sourcePath, requestedSize: 8);

        Assert.Equal(firstPath, repeatedPath);
        Assert.NotEqual(firstPath, differentSizePath);
        Assert.NotEqual(firstPath, changedSourcePath);
    }

    [Fact]
    public async Task GetOrCreateThumbnailAsync_returns_null_for_unsupported_unreadable_and_animated_inputs()
    {
        using var temp = TempWorkspace.Create();
        var unsupportedPath = await temp.WriteFileAsync("source.txt", [1, 2, 3]);
        var missingPath = Path.Combine(temp.Root, "missing.bmp");
        var animatedGifPath = await temp.WriteFileAsync("loop.gif", AnimatedGifHeader());
        var cacheRoot = Path.Combine(temp.Root, "cache");
        IThumbnailService service = new ThumbnailService(cacheRoot);

        var unsupported = await service.GetOrCreateThumbnailAsync(unsupportedPath, requestedSize: 8);
        var missing = await service.GetOrCreateThumbnailAsync(missingPath, requestedSize: 8);
        var animated = await service.GetOrCreateThumbnailAsync(animatedGifPath, requestedSize: 8);

        Assert.Null(unsupported);
        Assert.Null(missing);
        Assert.Null(animated);
        Assert.False(Directory.Exists(cacheRoot));
    }

    [Fact]
    public async Task GetOrCreateThumbnailAsync_prunes_old_cache_files_after_generating_thumbnail()
    {
        using var temp = TempWorkspace.Create();
        var sourcePath = await temp.WriteFileAsync("source.bmp", Bmp(width: 20, height: 10));
        var cacheRoot = Path.Combine(temp.Root, "cache");
        Directory.CreateDirectory(cacheRoot);
        var oldCachePath = Path.Combine(cacheRoot, "old.png");
        await File.WriteAllBytesAsync(oldCachePath, new byte[4096]);
        File.SetLastWriteTimeUtc(oldCachePath, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var service = new ThumbnailService(cacheRoot, maxCacheBytes: 1024);

        var thumbnailPath = await service.GetOrCreateThumbnailAsync(sourcePath, requestedSize: 5);

        Assert.NotNull(thumbnailPath);
        Assert.True(File.Exists(thumbnailPath));
        Assert.False(File.Exists(oldCachePath));
    }

    private static async Task<(uint Width, uint Height)> ReadDimensionsAsync(string path)
    {
        var file = await StorageFile.GetFileFromPathAsync(path);
        using var stream = await file.OpenReadAsync();
        var decoder = await BitmapDecoder.CreateAsync(stream);
        return (decoder.PixelWidth, decoder.PixelHeight);
    }

    private static byte[] Bmp(int width, int height)
    {
        const int fileHeaderSize = 14;
        const int dibHeaderSize = 40;
        const int bytesPerPixel = 3;
        var rowStride = ((width * bytesPerPixel) + 3) & ~3;
        var pixelArraySize = rowStride * height;
        var fileSize = fileHeaderSize + dibHeaderSize + pixelArraySize;
        var bytes = new byte[fileSize];

        bytes[0] = 0x42;
        bytes[1] = 0x4D;
        WriteInt32(bytes, 2, fileSize);
        WriteInt32(bytes, 10, fileHeaderSize + dibHeaderSize);
        WriteInt32(bytes, 14, dibHeaderSize);
        WriteInt32(bytes, 18, width);
        WriteInt32(bytes, 22, height);
        WriteInt16(bytes, 26, 1);
        WriteInt16(bytes, 28, 24);
        WriteInt32(bytes, 34, pixelArraySize);

        var pixelOffset = fileHeaderSize + dibHeaderSize;
        for (var y = 0; y < height; y += 1)
        {
            for (var x = 0; x < width; x += 1)
            {
                var index = pixelOffset + (y * rowStride) + (x * bytesPerPixel);
                bytes[index] = (byte)(x * 255 / Math.Max(1, width - 1));
                bytes[index + 1] = (byte)(y * 255 / Math.Max(1, height - 1));
                bytes[index + 2] = 0x80;
            }
        }

        return bytes;
    }

    private static byte[] AnimatedGifHeader() =>
        [(byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a', 0, 0, 0, 0, 0, 0, 0, 0x2c, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0x2c, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0x3b];

    private static void WriteInt16(byte[] bytes, int offset, short value)
    {
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
    }

    private static void WriteInt32(byte[] bytes, int offset, int value)
    {
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
        bytes[offset + 2] = (byte)(value >> 16);
        bytes[offset + 3] = (byte)(value >> 24);
    }
}
