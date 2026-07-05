using PicLens.Core.Models;

namespace PicLens.Core.Domain;

public static class SettingsRules
{
    public const int SettingsVersion = 2;
    public const int MaxRecentFolderCount = 5;
    public const int DefaultThumbnailSize = 160;
    public const int MinThumbnailSize = 140;
    public const int MaxThumbnailSize = 200;
    public const int ThumbnailSizeStep = 20;

    public static AppSettings NormalizeSettings(AppSettings settings)
    {
        var thumbnailSize = settings.ThumbnailSize == 0
            ? DefaultThumbnailSize
            : NormalizeThumbnailSize(settings.ThumbnailSize);

        return settings with
        {
            Version = SettingsVersion,
            ThumbnailSize = thumbnailSize,
            RecentFolderPaths = NormalizeRecentFolderPaths(settings.RecentFolderPaths)
        };
    }

    public static int NormalizeThumbnailSize(double thumbnailSize)
    {
        if (double.IsNaN(thumbnailSize) || double.IsInfinity(thumbnailSize))
        {
            return DefaultThumbnailSize;
        }

        var stepped = (int)(Math.Round(thumbnailSize / ThumbnailSizeStep, MidpointRounding.AwayFromZero) * ThumbnailSizeStep);
        return Math.Clamp(stepped, MinThumbnailSize, MaxThumbnailSize);
    }

    public static AppSettings MergeSettingsPatch(AppSettings current, AppSettingsPatch patch)
    {
        var normalizedCurrent = NormalizeSettings(current);

        return normalizedCurrent with
        {
            Version = SettingsVersion,
            LastFolderPath = patch.HasLastFolderPath ? patch.LastFolderPath : normalizedCurrent.LastFolderPath,
            Sort = patch.Sort ?? normalizedCurrent.Sort,
            IncludeSubfolders = patch.IncludeSubfolders ?? normalizedCurrent.IncludeSubfolders,
            ThumbnailSize = patch.ThumbnailSize.HasValue
                ? NormalizeThumbnailSize(patch.ThumbnailSize.Value)
                : normalizedCurrent.ThumbnailSize,
            RecentFolderPaths = patch.RecentFolderPaths is null
                ? normalizedCurrent.RecentFolderPaths
                : NormalizeRecentFolderPaths(patch.RecentFolderPaths)
        };
    }

    public static IReadOnlyList<string> NormalizeRecentFolderPaths(IEnumerable<string>? paths)
    {
        if (paths is null)
        {
            return [];
        }

        var normalized = new List<string>();
        var seen = new HashSet<string>(PathRules.PathComparer);
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                continue;
            }

            if (seen.Add(PathRules.PathKey(fullPath)))
            {
                normalized.Add(fullPath);
            }

            if (normalized.Count == MaxRecentFolderCount)
            {
                break;
            }
        }

        return normalized;
    }
}
