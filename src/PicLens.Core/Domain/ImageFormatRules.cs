namespace PicLens.Core.Domain;

public static class ImageFormatRules
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "jpg",
        "jpeg",
        "png",
        "bmp",
        "webp",
        "gif"
    };

    public static string? GetSupportedImageExtension(string filePath)
    {
        var extension = Path.GetExtension(filePath.Replace('\\', '/')).TrimStart('.').ToLowerInvariant();
        return SupportedExtensions.Contains(extension) ? extension : null;
    }
}
