namespace PicLens.Infrastructure.Services;

public static class AppDataPaths
{
    public const string DataRootEnvironmentVariable = "PICLENS_DATA_ROOT";

    private const string AppFolderName = "PicLens";
    private const string SettingsFileName = "piclens-settings.json";
    private const string LogsFolderName = "Logs";
    private const string LogFileName = "PicLens.log";
    private const string ThumbnailFolderName = "Thumbnails";

    public static string AppRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable(DataRootEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(configuredRoot));
        }

        return Path.Combine(LocalAppDataRoot(), AppFolderName);
    }

    public static string SettingsPath() => Path.Combine(AppRoot(), SettingsFileName);

    public static string LogPath() => Path.Combine(AppRoot(), LogsFolderName, LogFileName);

    public static string ThumbnailCacheRoot() => Path.Combine(AppRoot(), ThumbnailFolderName);

    private static string LocalAppDataRoot()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return string.IsNullOrWhiteSpace(localAppData) ? Path.GetTempPath() : localAppData;
    }
}
