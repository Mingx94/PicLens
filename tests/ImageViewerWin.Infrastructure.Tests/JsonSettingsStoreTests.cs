using ImageViewerWin.Core.Models;
using ImageViewerWin.Infrastructure.Services;

namespace ImageViewerWin.Infrastructure.Tests;

public sealed class JsonSettingsStoreTests
{
    [Fact]
    public async Task LoadAsync_returns_default_settings_when_file_is_missing()
    {
        using var temp = TempWorkspace.Create();
        var store = new JsonSettingsStore(Path.Combine(temp.Root, "settings.json"));

        var settings = await store.LoadAsync();

        Assert.Equal(AppSettings.CreateDefault(), settings);
    }

    [Fact]
    public async Task UpdateAsync_merges_patch_and_persists_normalized_user_favorites()
    {
        using var temp = TempWorkspace.Create();
        var store = new JsonSettingsStore(Path.Combine(temp.Root, "settings.json"));

        var updated = await store.UpdateAsync(new AppSettingsPatch
        {
            IncludeSubfolders = true,
            FavoriteFolders =
            [
                new FavoriteFolder("system:pictures", temp.Root, FavoriteSource.System, 0),
                new FavoriteFolder("user:b", Path.Combine(temp.Root, "B"), FavoriteSource.User, 99),
                new FavoriteFolder("user:a", Path.Combine(temp.Root, "A"), FavoriteSource.User, 42)
            ]
        });

        var loaded = await store.LoadAsync();

        Assert.True(updated.IncludeSubfolders);
        Assert.Equal(updated.Version, loaded.Version);
        Assert.Equal(updated.LastFolderPath, loaded.LastFolderPath);
        Assert.Equal(updated.Sort, loaded.Sort);
        Assert.Equal(updated.IncludeSubfolders, loaded.IncludeSubfolders);
        Assert.Equal(["user:b", "user:a"], loaded.FavoriteFolders.Select(favorite => favorite.Id));
        Assert.Equal([0, 1], loaded.FavoriteFolders.Select(favorite => favorite.Order));
    }
}
