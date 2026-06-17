using PicLens.Core.Models;

namespace PicLens.Core.Domain;

public static class ImageSequenceFactory
{
    public static ImageSequenceSnapshot Create(
        string sourceFolderPath,
        bool includeSubfolders,
        SortState sort,
        IReadOnlyList<ImageListItem> images,
        string currentImagePath,
        long? nowMs = null)
    {
        var currentIndex = -1;
        for (var index = 0; index < images.Count; index += 1)
        {
            if (images[index].Path == currentImagePath)
            {
                currentIndex = index;
                break;
            }
        }

        if (currentIndex < 0)
        {
            throw new InvalidOperationException("Current image must exist in the image sequence.");
        }

        var createdAtMs = nowMs ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var id = CreateSnapshotId(sourceFolderPath, createdAtMs, currentImagePath);

        return new ImageSequenceSnapshot(
            Id: id,
            CreatedAtMs: createdAtMs,
            SourceFolderPath: sourceFolderPath,
            IncludeSubfolders: includeSubfolders,
            Sort: sort,
            Images: images.Select(image => image with { }).ToList(),
            CurrentIndex: currentIndex);
    }

    private static string CreateSnapshotId(string sourceFolderPath, long createdAtMs, string currentImagePath)
    {
        var raw = $"{sourceFolderPath}:{createdAtMs}:{currentImagePath}";
        return $"sequence:{Uri.EscapeDataString(raw).Replace("%", "_", StringComparison.Ordinal)}";
    }
}
