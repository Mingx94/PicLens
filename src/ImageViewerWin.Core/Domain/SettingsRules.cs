using ImageViewerWin.Core.Models;

namespace ImageViewerWin.Core.Domain;

public static class SettingsRules
{
    public const int SettingsVersion = 1;

    public static IReadOnlyList<FavoriteFolder> NormalizeUserFavoriteFolders(IEnumerable<FavoriteFolder> folders) =>
        folders
            .Where(folder => folder.Source == FavoriteSource.User)
            .Select((folder, index) => folder with { Source = FavoriteSource.User, Order = index })
            .ToList();

    public static AppSettings MergeSettingsPatch(AppSettings current, AppSettingsPatch patch) =>
        current with
        {
            Version = SettingsVersion,
            LastFolderPath = patch.HasLastFolderPath ? patch.LastFolderPath : current.LastFolderPath,
            Sort = patch.Sort ?? current.Sort,
            IncludeSubfolders = patch.IncludeSubfolders ?? current.IncludeSubfolders,
            FavoriteFolders = patch.FavoriteFolders is null
                ? current.FavoriteFolders
                : NormalizeUserFavoriteFolders(patch.FavoriteFolders)
        };
}
