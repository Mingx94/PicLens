using ImageViewerWin.Core.Models;

namespace ImageViewerWin.Core.Domain;

public sealed record CreateImageSequenceSnapshotInput(
    string SourceFolderPath,
    bool IncludeSubfolders,
    SortState Sort,
    IReadOnlyList<ImageListItem> Images,
    string CurrentImagePath,
    long? NowMs = null);

public static class ImageSequenceFactory
{
    public static ImageSequenceSnapshot Create(CreateImageSequenceSnapshotInput input)
    {
        var currentIndex = -1;
        for (var index = 0; index < input.Images.Count; index += 1)
        {
            if (input.Images[index].Path == input.CurrentImagePath)
            {
                currentIndex = index;
                break;
            }
        }

        if (currentIndex < 0)
        {
            throw new InvalidOperationException("Current image must exist in the image sequence.");
        }

        var createdAtMs = input.NowMs ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var id = CreateSnapshotId(input.SourceFolderPath, createdAtMs, input.CurrentImagePath);

        return new ImageSequenceSnapshot(
            Id: id,
            CreatedAtMs: createdAtMs,
            SourceFolderPath: input.SourceFolderPath,
            IncludeSubfolders: input.IncludeSubfolders,
            Sort: input.Sort,
            Images: input.Images.Select(image => image with { }).ToList(),
            CurrentIndex: currentIndex);
    }

    private static string CreateSnapshotId(string sourceFolderPath, long createdAtMs, string currentImagePath)
    {
        var raw = $"{sourceFolderPath}:{createdAtMs}:{currentImagePath}";
        return $"sequence:{Uri.EscapeDataString(raw).Replace("%", "_", StringComparison.Ordinal)}";
    }
}
