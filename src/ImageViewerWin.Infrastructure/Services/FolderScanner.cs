using ImageViewerWin.Application.Services;
using ImageViewerWin.Core.Domain;
using ImageViewerWin.Core.Models;

namespace ImageViewerWin.Infrastructure.Services;

public sealed class FolderScanner : IFolderScanner
{
    public Task<IReadOnlyList<ListItem>> ScanAsync(ListQuery query, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(query.FolderPath))
        {
            throw new DirectoryNotFoundException(query.FolderPath);
        }

        var items = query.IncludeSubfolders
            ? EnumerateRecursiveImages(query.FolderPath, cancellationToken).Cast<ListItem>().ToList()
            : EnumerateDirectItems(query.FolderPath, cancellationToken).ToList();

        var sorted = ListItemSorter.Sort(items, query.Sort, new SortOptions(KeepFoldersFirst: !query.IncludeSubfolders));
        return Task.FromResult<IReadOnlyList<ListItem>>(sorted);
    }

    public Task<IReadOnlyList<FolderListItem>> ScanChildFoldersAsync(
        string folderPath,
        SortState sort,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException(folderPath);
        }

        var folders = EnumerateDirectFolders(folderPath, cancellationToken).Cast<ListItem>().ToList();
        var sorted = ListItemSorter.Sort(folders, sort, new SortOptions(KeepFoldersFirst: false))
            .OfType<FolderListItem>()
            .ToList();
        return Task.FromResult<IReadOnlyList<FolderListItem>>(sorted);
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
                Id: $"folder:{directory}",
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
                && ImageFormatRules.IsPotentiallyAnimatedImage(extension, File.ReadAllBytes(file));

            return new ImageListItem(
                Id: $"image:{file}",
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

    private static IEnumerable<string> SafeEnumerateDirectories(string folderPath)
    {
        try
        {
            return Directory.EnumerateDirectories(folderPath).ToArray();
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
    }

    private static IEnumerable<string> SafeEnumerateFiles(string folderPath)
    {
        try
        {
            return Directory.EnumerateFiles(folderPath).ToArray();
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
    }

    private static long ToUnixMs(DateTime value) =>
        new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

    private static StringComparer PathKeyComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

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
