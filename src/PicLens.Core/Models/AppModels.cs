namespace PicLens.Core.Models;

public enum SortKey
{
    Name,
    ModifiedAt
}

public enum SortDirection
{
    Asc,
    Desc
}

public enum FileOperationStatus
{
    Converted,
    Trashed,
    Renamed,
    Skipped,
    Failed
}

public sealed record SortState(SortKey Key, SortDirection Direction);

public sealed record AppSettings(
    string? LastFolderPath,
    SortState Sort,
    bool IncludeSubfolders,
    int ThumbnailSize)
{
    public static AppSettings CreateDefault() =>
        new(
            LastFolderPath: null,
            Sort: new SortState(SortKey.Name, SortDirection.Asc),
            IncludeSubfolders: false,
            ThumbnailSize: 200);
}

public sealed record AppSettingsPatch
{
    public string? LastFolderPath { get; init; }
    public bool HasLastFolderPath { get; init; }
    public SortState? Sort { get; init; }
    public bool? IncludeSubfolders { get; init; }
    public int? ThumbnailSize { get; init; }
}

public abstract record ListItem(string Path, string Name, long? ModifiedAtMs);

public sealed record FolderListItem(
    string Path,
    string Name,
    long? ModifiedAtMs)
    : ListItem(Path, Name, ModifiedAtMs);

public sealed record ImageListItem(
    string Path,
    string Name,
    string Extension,
    long? ModifiedAtMs,
    long SizeBytes,
    bool IsAnimated = false)
    : ListItem(Path, Name, ModifiedAtMs);

public sealed record ListQuery(
    string FolderPath,
    bool IncludeSubfolders,
    SortState Sort);

public sealed record ImageSequenceSnapshot(
    string SourceFolderPath,
    bool IncludeSubfolders,
    SortState Sort,
    IReadOnlyList<ImageListItem> Images,
    int CurrentIndex);

public sealed record FileOperationResult(
    string Path,
    FileOperationStatus Status,
    string? TargetPath = null,
    string? Reason = null,
    string? Message = null);

public sealed record FileOperationBatchResult(
    IReadOnlyList<FileOperationResult> Items)
{
    public int Total => Items.Count;
    public int Succeeded => Items.Count(item => item.Status is FileOperationStatus.Converted or FileOperationStatus.Trashed or FileOperationStatus.Renamed);
    public int Skipped => Items.Count(item => item.Status == FileOperationStatus.Skipped);
    public int Failed => Items.Count(item => item.Status == FileOperationStatus.Failed);
}

public readonly record struct Point(double X, double Y);

public sealed record ZoomState(double Zoom, Point Offset);
