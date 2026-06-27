using PicLens.Infrastructure.Services;

namespace PicLens.Infrastructure.Tests;

public sealed class AppDataPathsTests
{
    [Fact]
    public void Default_paths_use_piclens_data_root_when_set()
    {
        using var environment = EnvironmentVariableScope.Set(
            AppDataPaths.DataRootEnvironmentVariable,
            Path.Combine(Path.GetTempPath(), "PicLens.Infrastructure.Tests", Guid.NewGuid().ToString("N")));

        var root = AppDataPaths.AppRoot();

        Assert.Equal(Path.GetFullPath(environment.Value!), root);
        Assert.Equal(Path.Combine(root, "piclens-settings.json"), AppDataPaths.SettingsPath());
        Assert.Equal(Path.Combine(root, "Logs", "PicLens.log"), AppDataPaths.LogPath());
        Assert.Equal(Path.Combine(root, "Thumbnails"), AppDataPaths.ThumbnailCacheRoot());
    }

    [Fact]
    public void Default_paths_use_local_app_data_when_piclens_data_root_is_not_set()
    {
        using var environment = EnvironmentVariableScope.Set(AppDataPaths.DataRootEnvironmentVariable, null);

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = Path.GetTempPath();
        }

        var expectedRoot = Path.Combine(localAppData, "PicLens");

        Assert.Equal(expectedRoot, AppDataPaths.AppRoot());
        Assert.Equal(Path.Combine(expectedRoot, "piclens-settings.json"), AppDataPaths.SettingsPath());
        Assert.Equal(Path.Combine(expectedRoot, "Logs", "PicLens.log"), AppDataPaths.LogPath());
        Assert.Equal(Path.Combine(expectedRoot, "Thumbnails"), AppDataPaths.ThumbnailCacheRoot());
    }

}
