using PicLens.Core.Services;
using PicLens.Core.Domain;
using System.Security.Cryptography;
using System.Text;
using SkiaSharp;

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

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var source = DecodeBitmap(sourcePath);
                var (width, height) = FitWithin((uint)source.Width, (uint)source.Height, (uint)requestedSize);

                if (width == 0 || height == 0)
                {
                    throw new NotSupportedException("Image dimensions must be greater than zero.");
                }

                cancellationToken.ThrowIfCancellationRequested();
                var info = new SKImageInfo((int)width, (int)height, source.ColorType, source.AlphaType);
                SKBitmap? scaled = null;
                try
                {
                    var output = source;
                    if (source.Width != info.Width || source.Height != info.Height)
                    {
                        scaled = source.Resize(info, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
                            ?? throw new NotSupportedException("Image could not be resized.");
                        output = scaled;
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    using var stream = File.Create(tempPath);
                    if (!output.Encode(stream, SKEncodedImageFormat.Png, quality: 90))
                    {
                        throw new NotSupportedException("Thumbnail could not be encoded.");
                    }
                }
                finally
                {
                    scaled?.Dispose();
                }
            }, cancellationToken);
        }
        catch
        {
            DeleteTempFile(tempPath);
            throw;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            DeleteTempFile(tempPath);
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

    private static SKBitmap DecodeBitmap(string path)
    {
        using var input = File.OpenRead(path);
        return SKBitmap.Decode(input)
            ?? throw new NotSupportedException("Image could not be decoded.");
    }

    private static void DeleteTempFile(string tempPath)
    {
        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
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
        if (extension.ToLowerInvariant() is not ("gif" or "webp"))
        {
            return false;
        }

        return await Task.Run(() =>
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, BufferSize);
                using var codec = SKCodec.Create(stream);
                return codec?.FrameCount > 1;
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
            or ArgumentException;
}
