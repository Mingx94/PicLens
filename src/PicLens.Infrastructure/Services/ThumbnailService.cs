using PicLens.Application.Services;
using PicLens.Core.Domain;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace PicLens.Infrastructure.Services;

public sealed class ThumbnailService : IThumbnailService
{
    private const int BufferSize = 64 * 1024;
    private const long DefaultMaxCacheBytes = 512L * 1024L * 1024L;
    private const string CacheAlgorithmVersion = "v1";

    private readonly string cacheRoot;
    private readonly long maxCacheBytes;
    private int generatedSinceLastPrune = 0;
    private int isPruning = 0;

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
        if (cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        if (requestedSize <= 0 || ImageFormatRules.GetSupportedImageExtension(imagePath) is not { } extension)
        {
            return null;
        }

        FileInfo sourceInfo;
        try
        {
            sourceInfo = new FileInfo(imagePath);
        }
        catch (OperationCanceledException)
        {
            return null;
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
            TryTriggerPrune(cachePath);
            return cachePath;
        }
        catch (OperationCanceledException)
        {
            return null;
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

        cancellationToken.ThrowIfCancellationRequested();
        var sourceFile = await StorageFile.GetFileFromPathAsync(sourcePath).AsTask();
        cancellationToken.ThrowIfCancellationRequested();
        using var inputStream = await sourceFile.OpenReadAsync().AsTask();
        cancellationToken.ThrowIfCancellationRequested();
        var decoder = await BitmapDecoder.CreateAsync(inputStream).AsTask();
        var (width, height) = FitWithin(decoder.PixelWidth, decoder.PixelHeight, (uint)requestedSize);

        if (width == 0 || height == 0)
        {
            throw new NotSupportedException("Image dimensions must be greater than zero.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var cacheFolder = await StorageFolder.GetFolderFromPathAsync(cacheRoot).AsTask();
        cancellationToken.ThrowIfCancellationRequested();
        var tempFile = await cacheFolder
            .CreateFileAsync(tempFileName, CreationCollisionOption.FailIfExists)
            .AsTask();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using IRandomAccessStream outputStream = await tempFile.OpenAsync(FileAccessMode.ReadWrite).AsTask();
            cancellationToken.ThrowIfCancellationRequested();
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
                .AsTask();
            cancellationToken.ThrowIfCancellationRequested();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outputStream).AsTask();
            cancellationToken.ThrowIfCancellationRequested();
            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                width,
                height,
                decoder.DpiX,
                decoder.DpiY,
                pixelData.DetachPixelData());
            await encoder.FlushAsync().AsTask();
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch
        {
            await TryDeleteStorageFileAsync(tempFile);
            throw;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            await TryDeleteStorageFileAsync(tempFile);
            cancellationToken.ThrowIfCancellationRequested();
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

    private static async Task TryDeleteStorageFileAsync(StorageFile file)
    {
        try
        {
            await file.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask();
        }
        catch
        {
            // Best-effort cleanup; preserve the original thumbnail failure.
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

    private void TryTriggerPrune(string pathToKeep)
    {
        if (maxCacheBytes <= 1024 * 1024)
        {
            PruneCache(pathToKeep);
            return;
        }

        var count = Interlocked.Increment(ref generatedSinceLastPrune);
        if (count >= 50)
        {
            if (Interlocked.CompareExchange(ref isPruning, 1, 0) == 0)
            {
                Interlocked.Exchange(ref generatedSinceLastPrune, 0);
                _ = Task.Run(() =>
                {
                    try
                    {
                        PruneCache(pathToKeep);
                    }
                    finally
                    {
                        Interlocked.Exchange(ref isPruning, 0);
                    }
                });
            }
        }
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
            .OrderByDescending(file => PathRules.PathEquals(file.FullName, pathToKeep))
            .ThenByDescending(file => file.LastWriteTimeUtc)
            .ToList();

        var totalBytes = files.Sum(file => file.Length);
        foreach (var file in files.AsEnumerable().Reverse())
        {
            if (totalBytes <= maxCacheBytes || PathRules.PathEquals(file.FullName, pathToKeep))
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
        return await Task.Run(() =>
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, BufferSize);
                return ImageFormatRules.IsAnimatedGif(stream);
            }
            catch
            {
                return false;
            }
        }, cancellationToken);
    }

    private static async Task<bool> IsAnimatedWebpAsync(string path, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, BufferSize);
                return ImageFormatRules.IsAnimatedWebp(stream);
            }
            catch
            {
                return false;
            }
        }, cancellationToken);
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

    private static string DefaultCacheRoot()
        => AppDataPaths.ThumbnailCacheRoot();

    private static bool IsExpectedFailure(Exception ex) =>
        ex is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException
            or COMException;
}
