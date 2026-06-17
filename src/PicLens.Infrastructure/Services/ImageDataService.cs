using PicLens.Core.Domain;

namespace PicLens.Infrastructure.Services;

public sealed class ImageDataService
{
    public async Task<byte[]> ReadImageBytesAsync(string path, CancellationToken cancellationToken = default)
    {
        EnsureSupported(path);
        return await File.ReadAllBytesAsync(path, cancellationToken);
    }

    public async Task<string> GetImageDataUriAsync(string path, CancellationToken cancellationToken = default)
    {
        var extension = EnsureSupported(path);
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        return $"data:{GetMimeType(extension)};base64,{Convert.ToBase64String(bytes)}";
    }

    private static string EnsureSupported(string path) =>
        ImageFormatRules.GetSupportedImageExtension(path)
        ?? throw new NotSupportedException($"Unsupported image extension: {Path.GetExtension(path)}");

    private static string GetMimeType(string extension) =>
        extension.ToLowerInvariant() switch
        {
            "jpg" or "jpeg" => "image/jpeg",
            "png" => "image/png",
            "bmp" => "image/bmp",
            "webp" => "image/webp",
            "gif" => "image/gif",
            _ => throw new NotSupportedException($"Unsupported image extension: {extension}")
        };
}
