using ImageViewerWin.Application.Services;
using ImageViewerWin.Core.Domain;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ImageViewerWin.Infrastructure.Services;

public sealed class ThumbnailService : IThumbnailService
{
    private const int BufferSize = 64 * 1024;
    private const long DefaultMaxCacheBytes = 512L * 1024L * 1024L;
    private const string CacheAlgorithmVersion = "v1";

    private readonly string cacheRoot;
    private readonly long maxCacheBytes;

    public ThumbnailService()
        : this(DefaultCacheRoot())
    {
    }

    public ThumbnailService(string cacheRoot, long maxCacheBytes = DefaultMaxCacheBytes)
    {
        this.cacheRoot = Path.GetFullPath(cacheRoot);
        this.maxCacheBytes = Math.Max(0, maxCacheBytes);
    }

    public async Task<string?> GetOrCreateThumbnailAsync(
        string imagePath,
        int requestedSize,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (requestedSize <= 0 || ImageFormatRules.GetSupportedImageExtension(imagePath) is not { } extension)
        {
            return null;
        }

        FileInfo sourceInfo;
        try
        {
            sourceInfo = new FileInfo(imagePath);
        }
        catch (Exception ex) when (IsExpectedFailure(ex))
        {
            return null;
        }

        if (!sourceInfo.Exists)
        {
            return null;
        }

        try
        {
            if (await IsKnownAnimatedAsync(sourceInfo.FullName, extension, cancellationToken))
            {
                return null;
            }

            var cachePath = GetCachePath(sourceInfo, requestedSize);
            if (File.Exists(cachePath) && new FileInfo(cachePath).Length > 0)
            {
                return cachePath;
            }

            Directory.CreateDirectory(cacheRoot);
            await CreateThumbnailAsync(sourceInfo.FullName, cachePath, requestedSize, cancellationToken);
            PruneCache(cachePath);
            return cachePath;
        }
        catch (Exception ex) when (IsExpectedFailure(ex))
        {
            return null;
        }
    }

    private async Task CreateThumbnailAsync(
        string sourcePath,
        string cachePath,
        int requestedSize,
        CancellationToken cancellationToken)
    {
        var tempPath = Path.Combine(
            cacheRoot,
            $"{Path.GetFileNameWithoutExtension(cachePath)}-{Guid.NewGuid():N}.tmp");
        var tempFileName = Path.GetFileName(tempPath);

        var sourceFile = await StorageFile.GetFileFromPathAsync(sourcePath).AsTask(cancellationToken);
        using var inputStream = await sourceFile.OpenReadAsync().AsTask(cancellationToken);
        var decoder = await BitmapDecoder.CreateAsync(inputStream).AsTask(cancellationToken);
        var (width, height) = FitWithin(decoder.PixelWidth, decoder.PixelHeight, (uint)requestedSize);

        if (width == 0 || height == 0)
        {
            throw new NotSupportedException("Image dimensions must be greater than zero.");
        }

        var cacheFolder = await StorageFolder.GetFolderFromPathAsync(cacheRoot).AsTask(cancellationToken);
        var tempFile = await cacheFolder
            .CreateFileAsync(tempFileName, CreationCollisionOption.FailIfExists)
            .AsTask(cancellationToken);

        try
        {
            using IRandomAccessStream outputStream = await tempFile.OpenAsync(FileAccessMode.ReadWrite).AsTask(cancellationToken);
            var pixelData = await decoder.GetPixelDataAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    new BitmapTransform
                    {
                        ScaledWidth = width,
                        ScaledHeight = height,
                        InterpolationMode = BitmapInterpolationMode.Fant
                    },
                    ExifOrientationMode.RespectExifOrientation,
                    ColorManagementMode.ColorManageToSRgb)
                .AsTask(cancellationToken);
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outputStream).AsTask(cancellationToken);
            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                width,
                height,
                decoder.DpiX,
                decoder.DpiY,
                pixelData.DetachPixelData());
            await encoder.FlushAsync().AsTask(cancellationToken);
        }
        catch
        {
            await tempFile.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask();
            throw;
        }

        try
        {
            File.Move(tempPath, cachePath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }
    }

    private string GetCachePath(FileInfo sourceInfo, int requestedSize)
    {
        var key = string.Join(
            '\n',
            CacheAlgorithmVersion,
            NormalizePathForKey(sourceInfo.FullName),
            sourceInfo.LastWriteTimeUtc.Ticks.ToString(),
            sourceInfo.Length.ToString(),
            requestedSize.ToString());
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
        return Path.Combine(cacheRoot, $"{hash}.png");
    }

    private void PruneCache(string pathToKeep)
    {
        if (maxCacheBytes <= 0 || !Directory.Exists(cacheRoot))
        {
            return;
        }

        var files = Directory
            .EnumerateFiles(cacheRoot, "*.png", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .Where(file => file.Exists)
            .OrderByDescending(file => PathEquals(file.FullName, pathToKeep))
            .ThenByDescending(file => file.LastWriteTimeUtc)
            .ToList();

        var totalBytes = files.Sum(file => file.Length);
        foreach (var file in files.AsEnumerable().Reverse())
        {
            if (totalBytes <= maxCacheBytes || PathEquals(file.FullName, pathToKeep))
            {
                continue;
            }

            try
            {
                totalBytes -= file.Length;
                file.Delete();
            }
            catch (Exception ex) when (IsExpectedFailure(ex))
            {
            }
        }
    }

    private static async Task<bool> IsKnownAnimatedAsync(
        string path,
        string extension,
        CancellationToken cancellationToken)
    {
        return extension.ToLowerInvariant() switch
        {
            "gif" => await IsAnimatedGifAsync(path, cancellationToken),
            "webp" => await IsAnimatedWebpAsync(path, cancellationToken),
            _ => false
        };
    }

    private static async Task<bool> IsAnimatedGifAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, BufferSize, useAsync: true);
        var buffer = new byte[BufferSize];
        var descriptorCount = 0;

        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read == 0)
            {
                return false;
            }

            for (var index = 0; index < read; index += 1)
            {
                if (buffer[index] != 0x2C)
                {
                    continue;
                }

                descriptorCount += 1;
                if (descriptorCount > 1)
                {
                    return true;
                }
            }
        }
    }

    private static async Task<bool> IsAnimatedWebpAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, BufferSize, useAsync: true);
        var header = new byte[12];
        var headerRead = await stream.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
        if (headerRead < header.Length
            || Encoding.ASCII.GetString(header, 0, 4) != "RIFF"
            || Encoding.ASCII.GetString(header, 8, 4) != "WEBP")
        {
            return false;
        }

        var buffer = new byte[BufferSize + 3];
        var carry = 0;
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(carry, BufferSize), cancellationToken);
            if (read == 0)
            {
                return false;
            }

            var scanLength = carry + read;
            if (ContainsAscii(buffer.AsSpan(0, scanLength), "ANIM"))
            {
                return true;
            }

            carry = Math.Min(3, scanLength);
            buffer.AsSpan(scanLength - carry, carry).CopyTo(buffer);
        }
    }

    private static bool ContainsAscii(ReadOnlySpan<byte> buffer, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        for (var index = 0; index <= buffer.Length - bytes.Length; index += 1)
        {
            if (buffer[index..(index + bytes.Length)].SequenceEqual(bytes))
            {
                return true;
            }
        }

        return false;
    }

    private static (uint Width, uint Height) FitWithin(uint sourceWidth, uint sourceHeight, uint maxSize)
    {
        if (sourceWidth == 0 || sourceHeight == 0 || maxSize == 0)
        {
            return (0, 0);
        }

        if (sourceWidth <= maxSize && sourceHeight <= maxSize)
        {
            return (sourceWidth, sourceHeight);
        }

        var scale = Math.Min(maxSize / (double)sourceWidth, maxSize / (double)sourceHeight);
        return (
            Math.Max(1u, (uint)Math.Round(sourceWidth * scale)),
            Math.Max(1u, (uint)Math.Round(sourceHeight * scale)));
    }

    private static string NormalizePathForKey(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return OperatingSystem.IsWindows() ? fullPath.ToUpperInvariant() : fullPath;
    }

    private static bool PathEquals(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static string DefaultCacheRoot()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = Path.GetTempPath();
        }

        return Path.Combine(localAppData, "ImageViewerWin", "Thumbnails");
    }

    private static bool IsExpectedFailure(Exception ex) =>
        ex is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException
            or COMException;
}
