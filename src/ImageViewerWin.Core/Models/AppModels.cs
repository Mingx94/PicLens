namespace ImageViewerWin.Core.Models;

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

public enum FavoriteSource
{
    System,
    User
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
    IReadOnlyList<FavoriteFolder> FavoriteFolders)
{
    public static AppSettings CreateDefault() =>
        new(
            Version: 1,
            LastFolderPath: null,
            Sort: new SortState(SortKey.Name, SortDirection.Asc),
            IncludeSubfolders: false,
            FavoriteFolders: []);
}

public sealed record AppSettingsPatch
{
    public string? LastFolderPath { get; init; }
    public bool HasLastFolderPath { get; init; }
    public SortState? Sort { get; init; }
    public bool? IncludeSubfolders { get; init; }
    public IReadOnlyList<FavoriteFolder>? FavoriteFolders { get; init; }
}

public sealed record FavoriteFolder(
    string Id,
    string Path,
    FavoriteSource Source,
    int Order,
    string? Name = null,
    bool? IsAvailable = null);

public sealed record FolderNode(
    string Id,
    string Path,
    string Name,
    bool HasChildren,
    bool IsReadable);

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
    int? Width = null,
    int? Height = null,
    bool IsAnimated = false,
    string? ThumbnailUrl = null,
    string? ImageUrl = null)
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
