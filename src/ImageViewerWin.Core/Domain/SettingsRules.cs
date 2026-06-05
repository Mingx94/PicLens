using ImageViewerWin.Core.Models;

namespace ImageViewerWin.Core.Domain;

public static class SettingsRules
{
    public const int SettingsVersion = 1;

    public static AppSettings MergeSettingsPatch(AppSettings current, AppSettingsPatch patch) =>
        current with
        {
            Version = SettingsVersion,
            LastFolderPath = patch.HasLastFolderPath ? patch.LastFolderPath : current.LastFolderPath,
            Sort = patch.Sort ?? current.Sort,
            IncludeSubfolders = patch.IncludeSubfolders ?? current.IncludeSubfolders
        };
}
