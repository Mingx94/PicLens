using PicLens.Core.Services;
using PicLens.Core.Domain;
using PicLens.Core.Models;
using SkiaSharp;

namespace PicLens.Infrastructure.Services;

public sealed class FolderScanner : IFolderScanner
{
    private static readonly EnumerationOptions SafeEnumerationOptions = new()
    {
        AttributesToSkip = 0,
        IgnoreInaccessible = true
    };

    public Task<IReadOnlyList<ListItem>> ScanAsync(ListQuery query, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Scan(query, cancellationToken), cancellationToken);
    }

    public Task<IReadOnlyList<FolderListItem>> ScanChildFoldersAsync(
        string folderPath,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => ScanChildFolders(folderPath, cancellationToken), cancellationToken);
    }

    private static IReadOnlyList<ListItem> Scan(ListQuery query, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(query.FolderPath))
        {
            throw new DirectoryNotFoundException(query.FolderPath);
        }

        var items = query.IncludeSubfolders
            ? EnumerateRecursiveImages(query.FolderPath, cancellationToken).Cast<ListItem>().ToList()
            : EnumerateDirectItems(query.FolderPath, cancellationToken).ToList();

        var sorted = ListItemSorter.Sort(items, query.Sort, keepFoldersFirst: !query.IncludeSubfolders);
        return sorted;
    }

    private static IReadOnlyList<FolderListItem> ScanChildFolders(
        string folderPath,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException(folderPath);
        }

        var folders = EnumerateDirectFolders(folderPath, cancellationToken).Cast<ListItem>().ToList();
        var sorted = ListItemSorter.Sort(
                folders,
                new SortState(SortKey.Name, SortDirection.Asc),
                keepFoldersFirst: false)
            .OfType<FolderListItem>()
            .ToList();
        return sorted;
    }

    private static IEnumerable<ListItem> EnumerateDirectItems(string folderPath, CancellationToken cancellationToken)
    {
        foreach (var folder in EnumerateDirectFolders(folderPath, cancellationToken))
        {
            yield return folder;
        }

        foreach (var file in SafeEnumerateFiles(folderPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var image = CreateImageItem(file);
            if (image is not null)
            {
                yield return image;
            }
        }
    }

    private static IEnumerable<FolderListItem> EnumerateDirectFolders(string folderPath, CancellationToken cancellationToken)
    {
        foreach (var directory in SafeEnumerateDirectories(folderPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new FolderListItem(
                Path: directory,
                Name: Path.GetFileName(directory),
                ModifiedAtMs: ToUnixMs(Directory.GetLastWriteTimeUtc(directory)));
        }
    }

    private static IEnumerable<ImageListItem> EnumerateRecursiveImages(string root, CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        var visited = new HashSet<string>(PathKeyComparer);
        pending.Push(root);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = pending.Pop();
            var canonical = CanonicalDirectoryKey(current);
            if (canonical is null || !visited.Add(canonical))
            {
                continue;
            }

            foreach (var directory in SafeEnumerateDirectories(current))
            {
                pending.Push(directory);
            }

            foreach (var file in SafeEnumerateFiles(current))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var image = CreateImageItem(file);
                if (image is not null)
                {
                    yield return image;
                }
            }
        }
    }

    private static ImageListItem? CreateImageItem(string file)
    {
        var extension = ImageFormatRules.GetSupportedImageExtension(file);
        if (extension is null)
        {
            return null;
        }

        try
        {
            var info = new FileInfo(file);
            var isAnimated = RequiresAnimationDetection(extension)
                && IsKnownAnimatedImage(file);

            return new ImageListItem(
                Path: file,
                Name: info.Name,
                Extension: extension,
                ModifiedAtMs: ToUnixMs(info.LastWriteTimeUtc),
                SizeBytes: info.Length,
                IsAnimated: isAnimated);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool RequiresAnimationDetection(string extension) =>
        extension.Equals("gif", StringComparison.OrdinalIgnoreCase)
        || extension.Equals("webp", StringComparison.OrdinalIgnoreCase);

    private static bool IsKnownAnimatedImage(string path)
    {
        try
        {
            using var stream = OpenProbeStream(path);
            using var codec = SKCodec.Create(stream);
            return codec?.FrameCount > 1;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static FileStream OpenProbeStream(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    private static IEnumerable<string> SafeEnumerateDirectories(string folderPath) =>
        Directory.EnumerateDirectories(folderPath, "*", SafeEnumerationOptions);

    private static IEnumerable<string> SafeEnumerateFiles(string folderPath) =>
        Directory.EnumerateFiles(folderPath, "*", SafeEnumerationOptions);

    private static long ToUnixMs(DateTime value) =>
        new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

    private static StringComparer PathKeyComparer => PathRules.PathComparer;

    private static string? CanonicalDirectoryKey(string folderPath)
    {
        try
        {
            var directory = new DirectoryInfo(folderPath);
            var resolved = directory.ResolveLinkTarget(returnFinalTarget: true);
            return Path.GetFullPath(resolved?.FullName ?? directory.FullName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
