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
    int Version,
    string? LastFolderPath,
    SortState Sort,
    bool IncludeSubfolders,
    int ThumbnailSize,
    IReadOnlyList<string> RecentFolderPaths)
{
    public static AppSettings CreateDefault() =>
        new(
            Version: 2,
            LastFolderPath: null,
            Sort: new SortState(SortKey.Name, SortDirection.Asc),
            IncludeSubfolders: false,
            ThumbnailSize: 200,
            RecentFolderPaths: []);
}

public sealed record AppSettingsPatch
{
    public string? LastFolderPath { get; init; }
    public bool HasLastFolderPath { get; init; }
    public SortState? Sort { get; init; }
    public bool? IncludeSubfolders { get; init; }
    public int? ThumbnailSize { get; init; }
    public IReadOnlyList<string>? RecentFolderPaths { get; init; }
}

public abstract record ListItem
{
    protected ListItem(string id, string path, string name, long? modifiedAtMs)
    {
        Id = id;
        Path = path;
        Name = name;
        ModifiedAtMs = modifiedAtMs;
    }

    public string Id { get; init; }
    public string Path { get; init; }
    public string Name { get; init; }
    public long? ModifiedAtMs { get; init; }
}

public sealed record FolderListItem(
    string Id,
    string Path,
    string Name,
    long? ModifiedAtMs)
    : ListItem(Id, Path, Name, ModifiedAtMs);

public sealed record ImageListItem(
    string Id,
    string Path,
    string Name,
    string Extension,
    long? ModifiedAtMs,
    long SizeBytes,
    bool IsAnimated = false)
    : ListItem(Id, Path, Name, ModifiedAtMs);

public sealed record ListQuery(
    string FolderPath,
    bool IncludeSubfolders,
    SortState Sort);

public sealed record ImageSequenceSnapshot(
    string Id,
    long CreatedAtMs,
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
    int Total,
    int Succeeded,
    int Skipped,
    int Failed,
    IReadOnlyList<FileOperationResult> Items);

public readonly record struct Point(double X, double Y);

public sealed record ZoomState(double Zoom, Point Offset);
