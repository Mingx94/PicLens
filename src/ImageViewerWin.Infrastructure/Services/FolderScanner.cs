using ImageViewerWin.Application.Services;
using ImageViewerWin.Core.Domain;
using ImageViewerWin.Core.Models;
using System.Text;

namespace ImageViewerWin.Infrastructure.Services;

public sealed class FolderScanner : IFolderScanner
{
    private const int AnimationProbeBufferSize = 64 * 1024;

    public Task<IReadOnlyList<ListItem>> ScanAsync(ListQuery query, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Scan(query, cancellationToken), cancellationToken);
    }

    public Task<IReadOnlyList<FolderListItem>> ScanChildFoldersAsync(
        string folderPath,
        SortState sort,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => ScanChildFolders(folderPath, sort, cancellationToken), cancellationToken);
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

        var sorted = ListItemSorter.Sort(items, query.Sort, new SortOptions(KeepFoldersFirst: !query.IncludeSubfolders));
        return sorted;
    }

    private static IReadOnlyList<FolderListItem> ScanChildFolders(
        string folderPath,
        SortState sort,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException(folderPath);
        }

        var folders = EnumerateDirectFolders(folderPath, cancellationToken).Cast<ListItem>().ToList();
        var sorted = ListItemSorter.Sort(folders, sort, new SortOptions(KeepFoldersFirst: false))
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
            var image = CreateImageItem(file, cancellationToken);
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
                var image = CreateImageItem(file, cancellationToken);
                if (image is not null)
                {
                    yield return image;
                }
            }
        }
    }

    private static ImageListItem? CreateImageItem(string file, CancellationToken cancellationToken)
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
                && IsKnownAnimatedImage(file, extension, cancellationToken);

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

    private static bool IsKnownAnimatedImage(
        string path,
        string extension,
        CancellationToken cancellationToken)
    {
        try
        {
            return extension.ToLowerInvariant() switch
            {
                "gif" => IsAnimatedGif(path, cancellationToken),
                "webp" => IsAnimatedWebp(path, cancellationToken),
                _ => false
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsAnimatedGif(string path, CancellationToken cancellationToken)
    {
        using var stream = OpenProbeStream(path);
        return ImageFormatRules.IsAnimatedGif(stream);
    }

    private static bool IsAnimatedWebp(string path, CancellationToken cancellationToken)
    {
        using var stream = OpenProbeStream(path);
        return ImageFormatRules.IsAnimatedWebp(stream);
    }

    private static FileStream OpenProbeStream(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    private static bool ContainsAscii(ReadOnlySpan<byte> buffer, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        for (var index = 0; index <= buffer.Length - bytes.Length; index += 1)
        {
            if (buffer[index..(index + bytes.Length)].SequenceEqual(bytes))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string folderPath)
    {
        return SafeEnumerateFileSystemEntries(() => Directory.EnumerateDirectories(folderPath));
    }

    private static IEnumerable<string> SafeEnumerateFiles(string folderPath)
    {
        return SafeEnumerateFileSystemEntries(() => Directory.EnumerateFiles(folderPath));
    }

    private static IEnumerable<string> SafeEnumerateFileSystemEntries(Func<IEnumerable<string>> enumerate)
    {
        IEnumerator<string> enumerator;
        try
        {
            enumerator = enumerate().GetEnumerator();
        }
        catch (UnauthorizedAccessException)
        {
            yield break;
        }
        catch (IOException)
        {
            yield break;
        }

        using (enumerator)
        {
            while (true)
            {
                string current;
                try
                {
                    if (!enumerator.MoveNext())
                    {
                        yield break;
                    }

                    current = enumerator.Current;
                }
                catch (UnauthorizedAccessException)
                {
                    yield break;
                }
                catch (IOException)
                {
                    yield break;
                }

                yield return current;
            }
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
