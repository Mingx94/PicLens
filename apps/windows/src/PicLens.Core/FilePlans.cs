namespace PicLens.Core;

public enum OperationKind { Jpeg, Webp, Rename, Trash }
public enum ResultStatus { Succeeded, Skipped, Canceled, Failed, Unknown }
public sealed record FileStamp(long Length, long Modified)
{
    public static FileStamp Read(string path) { var f = new FileInfo(path); return new(f.Length, f.LastWriteTimeUtc.Ticks); }
    public static FileStamp Capture(string path)
    {
        try { return Read(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return new(-1, -1); }
    }
}
public sealed record FilePlan(string Source, string? Target, OperationKind Kind, FileStamp Stamp, string? Skip = null, bool CheckStem = false);
public sealed record FileResult(string Source, string? Target, ResultStatus Status, string Message);
public sealed record BatchResult(IReadOnlyList<FileResult> Items)
{
    public int Succeeded => Items.Count(x => x.Status == ResultStatus.Succeeded);
    public int Skipped => Items.Count(x => x.Status == ResultStatus.Skipped);
    public int Canceled => Items.Count(x => x.Status == ResultStatus.Canceled);
    public int Failed => Items.Count(x => x.Status is ResultStatus.Failed or ResultStatus.Unknown);
    public string Summary => $"共 {Items.Count} 張 · 成功 {Succeeded} · 略過 {Skipped} · 取消 {Canceled} · 失敗 {Failed}";
}
public static class FilePlans
{
    public static bool RequiresConversionConfirmation(int count) => count >= 50;
    public static string ValidateRename(string source, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.EndsWith('.') || name.EndsWith(' ') ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Length > 240)
            throw new ArgumentException("檔名不可為空、包含特殊字元，或以空白和句點結尾。");
        string first = name.Split('.')[0].ToUpperInvariant();
        if (new[] { "CON", "PRN", "AUX", "NUL" }.Contains(first) ||
            (first.Length == 4 && (first.StartsWith("COM") || first.StartsWith("LPT")) && first[3] is >= '1' and <= '9'))
            throw new ArgumentException("這是 Windows 保留的檔名。");
        return Path.Combine(Path.GetDirectoryName(source)!, name + Path.GetExtension(source));
    }
    public static List<FilePlan> Convert(IEnumerable<LibraryEntry> entries, OperationKind kind) =>
        entries.Where(e => !e.IsFolder).Select(e => new FilePlan(e.Path, Path.ChangeExtension(e.Path, kind == OperationKind.Jpeg ? ".jpg" : ".webp"), kind,
            FileStamp.Capture(e.Path), e.Animated ? "動畫圖片不支援轉換" :
            kind == OperationKind.Webp && e.Extension is ".jpg" or ".jpeg" or ".webp" ? "保留既有 JPG／WebP" :
            kind == OperationKind.Jpeg && e.Extension is ".jpg" or ".jpeg" ? "已是 JPG" : null)).ToList();
    public static List<FilePlan> Cleanup(IEnumerable<LibraryEntry> entries)
    {
        var images = entries.Where(e => !e.IsFolder).ToList();
        var keep = images.Where(e => e.Extension is ".jpg" or ".jpeg" or ".webp")
            .Select(e => Path.Combine(Path.GetDirectoryName(e.Path)!, Path.GetFileNameWithoutExtension(e.Path))).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return images.Where(e => e.Extension is not (".jpg" or ".jpeg" or ".webp") &&
            keep.Contains(Path.Combine(Path.GetDirectoryName(e.Path)!, Path.GetFileNameWithoutExtension(e.Path))))
            .Select(e => new FilePlan(e.Path, null, OperationKind.Trash, FileStamp.Capture(e.Path))).ToList();
    }
    public static List<FilePlan> DropRename(IReadOnlyList<string> sources, string target, IEnumerable<string> existing)
    {
        var occupied = existing.ToList(); string dir = Path.GetDirectoryName(target)!, stem = Path.GetFileNameWithoutExtension(target);
        int sequence = 1; List<FilePlan> plans = [];
        foreach (string source in sources.Where(s => !StringComparer.OrdinalIgnoreCase.Equals(s, target)))
        {
            string sourceStem = Path.GetFileNameWithoutExtension(source);
            if (sourceStem.StartsWith(stem + "-", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(sourceStem[(stem.Length + 1)..], out int sourceSequence) && sourceSequence < sequence &&
                !occupied.Any(p => !StringComparer.OrdinalIgnoreCase.Equals(p, source) &&
                    StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(p), Path.GetDirectoryName(source)) &&
                    StringComparer.OrdinalIgnoreCase.Equals(Path.GetFileNameWithoutExtension(p), sourceStem)))
            {
                plans.Add(new(source, source, OperationKind.Rename, FileStamp.Capture(source), "已是目標序號", true));
                continue;
            }
            string candidate;
            while (true)
            {
                candidate = Path.Combine(dir, $"{stem}-{sequence:00}{Path.GetExtension(source)}");
                bool conflict = occupied.Any(p => !StringComparer.OrdinalIgnoreCase.Equals(p, source) &&
                    StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(p), dir) &&
                    StringComparer.OrdinalIgnoreCase.Equals(Path.GetFileNameWithoutExtension(p), Path.GetFileNameWithoutExtension(candidate)));
                if (!conflict) break;
                sequence++;
            }
            string? skip = StringComparer.OrdinalIgnoreCase.Equals(source, candidate) ? "已是目標名稱" : null;
            plans.Add(new(source, candidate, OperationKind.Rename, FileStamp.Capture(source), skip, true));
            occupied.Add(candidate); sequence++;
        }
        return plans;
    }
}
