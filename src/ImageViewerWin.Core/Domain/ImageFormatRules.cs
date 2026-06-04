using System.Text;

namespace ImageViewerWin.Core.Domain;

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
        var normalizedPath = filePath.Replace('\\', '/');
        var lastSlash = normalizedPath.LastIndexOf('/');
        var basename = lastSlash >= 0 ? normalizedPath[(lastSlash + 1)..] : normalizedPath;
        var lastDot = basename.LastIndexOf('.');

        if (lastDot < 0 || lastDot == basename.Length - 1)
        {
            return null;
        }

        var extension = basename[(lastDot + 1)..].ToLowerInvariant();
        return SupportedExtensions.Contains(extension) ? extension : null;
    }

    public static bool IsAnimatedGif(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < 10 || Ascii(buffer[..3]) != "GIF")
        {
            return false;
        }

        var imageDescriptorCount = 0;
        foreach (var value in buffer)
        {
            if (value != 0x2c)
            {
                continue;
            }

            imageDescriptorCount += 1;
            if (imageDescriptorCount > 1)
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsAnimatedWebp(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < 12)
        {
            return false;
        }

        var header = Ascii(buffer[..12]);
        return header.StartsWith("RIFF", StringComparison.Ordinal)
            && header.EndsWith("WEBP", StringComparison.Ordinal)
            && IncludesAscii(buffer, "ANIM");
    }

    public static bool IsPotentiallyAnimatedImage(string extension, ReadOnlySpan<byte> buffer) =>
        extension.ToLowerInvariant() switch
        {
            "gif" => IsAnimatedGif(buffer),
            "webp" => IsAnimatedWebp(buffer),
            _ => false
        };

    private static string Ascii(ReadOnlySpan<byte> buffer) => Encoding.ASCII.GetString(buffer);

    private static bool IncludesAscii(ReadOnlySpan<byte> buffer, string needle)
    {
        if (needle.Length == 0)
        {
            return true;
        }

        var needleBytes = Encoding.ASCII.GetBytes(needle);
        for (var index = 0; index <= buffer.Length - needleBytes.Length; index += 1)
        {
            if (buffer.Slice(index, needleBytes.Length).SequenceEqual(needleBytes))
            {
                return true;
            }
        }

        return false;
    }
}
