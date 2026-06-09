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
        if (buffer.Length < 13 || buffer[0] != 'G' || buffer[1] != 'I' || buffer[2] != 'F')
        {
            return false;
        }

        byte packed = buffer[10];
        bool hasGct = (packed & 0x80) != 0;
        int offset = 13;
        if (hasGct)
        {
            int gctSize = 3 * (1 << ((packed & 0x07) + 1));
            offset += gctSize;
        }

        int imageDescriptorCount = 0;

        while (offset < buffer.Length)
        {
            byte blockType = buffer[offset];
            offset++;

            if (blockType == 0x2C) // Image Descriptor
            {
                imageDescriptorCount++;
                if (imageDescriptorCount > 1) return true;

                if (offset + 9 > buffer.Length) break;
                byte localPacked = buffer[offset + 8];
                offset += 9;

                if ((localPacked & 0x80) != 0)
                {
                    int lctSize = 3 * (1 << ((localPacked & 0x07) + 1));
                    offset += lctSize;
                }

                if (offset >= buffer.Length) break;
                // LZW Minimum Code Size
                offset++;

                while (offset < buffer.Length)
                {
                    byte subBlockSize = buffer[offset];
                    offset++;
                    if (subBlockSize == 0) break;
                    offset += subBlockSize;
                }
            }
            else if (blockType == 0x21) // Extension Block
            {
                if (offset >= buffer.Length) break;
                // Extension Label
                offset++;

                while (offset < buffer.Length)
                {
                    if (offset >= buffer.Length) break;
                    byte subBlockSize = buffer[offset];
                    offset++;
                    if (subBlockSize == 0) break;
                    offset += subBlockSize;
                }
            }
            else if (blockType == 0x3B) // Trailer
            {
                break;
            }
            else
            {
                break;
            }
        }
        return false;
    }

    public static bool IsAnimatedWebp(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < 21) return false;
        if (buffer[0] != 'R' || buffer[1] != 'I' || buffer[2] != 'F' || buffer[3] != 'F') return false;
        if (buffer[8] != 'W' || buffer[9] != 'E' || buffer[10] != 'B' || buffer[11] != 'P') return false;

        if (buffer[12] == 'V' && buffer[13] == 'P' && buffer[14] == '8' && buffer[15] == 'X')
        {
            byte flags = buffer[20];
            return (flags & 0x02) != 0;
        }

        return false;
    }

    public static bool IsAnimatedGif(Stream stream)
    {
        var header = new byte[13];
        if (stream.Read(header, 0, 13) < 13) return false;
        if (header[0] != 'G' || header[1] != 'I' || header[2] != 'F') return false;

        byte packed = header[10];
        bool hasGct = (packed & 0x80) != 0;
        if (hasGct)
        {
            int gctSize = 3 * (1 << ((packed & 0x07) + 1));
            stream.Seek(gctSize, SeekOrigin.Current);
        }

        int imageDescriptorCount = 0;
        var blockHeader = new byte[9];

        while (true)
        {
            int blockType = stream.ReadByte();
            if (blockType == -1) break;

            if (blockType == 0x2C) // Image Descriptor
            {
                imageDescriptorCount++;
                if (imageDescriptorCount > 1) return true;

                if (stream.Read(blockHeader, 0, 9) < 9) break;
                byte localPacked = blockHeader[8];
                if ((localPacked & 0x80) != 0)
                {
                    int lctSize = 3 * (1 << ((localPacked & 0x07) + 1));
                    stream.Seek(lctSize, SeekOrigin.Current);
                }

                int lzwSize = stream.ReadByte();
                if (lzwSize == -1) break;

                while (true)
                {
                    int subBlockSize = stream.ReadByte();
                    if (subBlockSize <= 0) break;
                    stream.Seek(subBlockSize, SeekOrigin.Current);
                }
            }
            else if (blockType == 0x21) // Extension Block
            {
                int extLabel = stream.ReadByte();
                if (extLabel == -1) break;

                while (true)
                {
                    int subBlockSize = stream.ReadByte();
                    if (subBlockSize <= 0) break;
                    stream.Seek(subBlockSize, SeekOrigin.Current);
                }
            }
            else if (blockType == 0x3B) // Trailer
            {
                break;
            }
            else
            {
                break;
            }
        }
        return false;
    }

    public static bool IsAnimatedWebp(Stream stream)
    {
        var header = new byte[21];
        if (stream.Read(header, 0, 21) < 21) return false;

        if (header[0] != 'R' || header[1] != 'I' || header[2] != 'F' || header[3] != 'F') return false;
        if (header[8] != 'W' || header[9] != 'E' || header[10] != 'B' || header[11] != 'P') return false;

        if (header[12] == 'V' && header[13] == 'P' && header[14] == '8' && header[15] == 'X')
        {
            byte flags = header[20];
            return (flags & 0x02) != 0;
        }

        return false;
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
