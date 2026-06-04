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

    private static IEnumerable<ListItem> EnumerateDirectItems(string folderPath, CancellationToken cancellationToken)
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

    private static IEnumerable<ImageListItem> EnumerateRecursiveImages(string root, CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = pending.Pop();

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
            var bytes = File.ReadAllBytes(file);
            var isAnimated = ImageFormatRules.IsPotentiallyAnimatedImage(extension, bytes);

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

    private static IEnumerable<string> SafeEnumerateDirectories(string folderPath)
    {
        try
        {
            return Directory.EnumerateDirectories(folderPath);
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
            return Directory.EnumerateFiles(folderPath);
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
}
