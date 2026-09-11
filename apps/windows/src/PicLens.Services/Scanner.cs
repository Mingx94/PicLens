using PicLens.Core;
namespace PicLens.Services;

public sealed class Scanner(Profile profile)
{
    public Task<List<LibraryEntry>> ScanAsync(string folder, bool recursive, CancellationToken cancellation) => Task.Run(() =>
    {
        List<LibraryEntry> entries = []; Stack<string> pending = new(); pending.Push(Path.GetFullPath(folder));
        while (pending.TryPop(out string? current))
        {
            cancellation.ThrowIfCancellationRequested();
            try
            {
                foreach (var path in Directory.EnumerateFileSystemEntries(current))
                {
                    cancellation.ThrowIfCancellationRequested();
                    try
                    {
                        var attributes = File.GetAttributes(path);
                        if ((attributes & FileAttributes.Directory) != 0)
                        {
                            if (recursive) { if ((attributes & FileAttributes.ReparsePoint) == 0) pending.Push(path); }
                            else entries.Add(new(path, Path.GetFileName(path), true, Directory.GetLastWriteTimeUtc(path).Ticks, 0));
                        }
                        else if (Formats.Supported(path))
                        {
                            var info = new FileInfo(path);
                            bool animated = AnimationProbe.IsAnimated(path);
                            entries.Add(new(path, info.Name, false, info.LastWriteTimeUtc.Ticks, info.Length, animated));
                        }
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException) { profile.Log($"無法讀取 {path}", e); }
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                profile.Log($"無法掃描 {current}", e); if (current == folder) throw;
            }
        }
        profile.Log($"掃描完成 folder={folder} count={entries.Count}");
        return entries;
    }, cancellation);
    public Task<List<string>> ChildrenAsync(string folder, CancellationToken ct) => Task.Run(() =>
    {
        ct.ThrowIfCancellationRequested();
        return Directory.EnumerateDirectories(folder).OrderBy(p => Path.GetFileName(p), NaturalComparer.Instance).ToList();
    }, ct);
}
public static class AnimationProbe
{
    public static bool IsAnimated(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is not (".gif" or ".webp")) return false;
        using var stream = File.OpenRead(path); using var reader = new BinaryReader(stream);
        if (ext == ".webp")
        {
            if (stream.Length < 12 || new string(reader.ReadChars(4)) != "RIFF") return false;
            stream.Position = 8; if (new string(reader.ReadChars(4)) != "WEBP") return false;
            while (stream.Position + 8 <= stream.Length)
            {
                string type = new(reader.ReadChars(4)); uint length = reader.ReadUInt32();
                if (type == "ANIM" || type == "ANMF") return true;
                if (length > stream.Length - stream.Position) return false;
                stream.Seek(length + (length & 1), SeekOrigin.Current);
            }
            return false;
        }
        if (stream.Length < 13) return false;
        string magic = new(reader.ReadChars(6)); if (magic is not ("GIF87a" or "GIF89a")) return false;
        stream.Position = 10; byte packed = reader.ReadByte(); stream.Position = 13;
        if ((packed & 128) != 0) stream.Seek(3 * (1 << ((packed & 7) + 1)), SeekOrigin.Current);
        int frames = 0;
        while (stream.Position < stream.Length)
        {
            int block = stream.ReadByte();
            if (block == 0x3B) break;
            if (block == 0x21) { if (stream.ReadByte() < 0) break; SkipBlocks(stream); }
            else if (block == 0x2C)
            {
                if (++frames > 1) return true;
                if (stream.Position + 9 > stream.Length) break;
                stream.Seek(8, SeekOrigin.Current); int flags = stream.ReadByte();
                if ((flags & 128) != 0) stream.Seek(3 * (1 << ((flags & 7) + 1)), SeekOrigin.Current);
                if (stream.ReadByte() < 0) break; SkipBlocks(stream);
            }
            else break;
        }
        return false;
    }
    static void SkipBlocks(Stream stream)
    {
        int n;
        while ((n = stream.ReadByte()) > 0)
        {
            if (stream.Position + n > stream.Length) { stream.Position = stream.Length; return; }
            stream.Seek(n, SeekOrigin.Current);
        }
    }
}
