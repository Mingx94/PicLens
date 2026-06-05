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
    public async Task UpdateAsync_merges_patch_and_persists_settings()
    {
        using var temp = TempWorkspace.Create();
        var store = new JsonSettingsStore(Path.Combine(temp.Root, "settings.json"));

        var updated = await store.UpdateAsync(new AppSettingsPatch
        {
            LastFolderPath = temp.Root,
            HasLastFolderPath = true,
            IncludeSubfolders = true,
            Sort = new SortState(SortKey.ModifiedAt, SortDirection.Desc)
        });

        var loaded = await store.LoadAsync();

        Assert.True(updated.IncludeSubfolders);
        Assert.Equal(updated.Version, loaded.Version);
        Assert.Equal(updated.LastFolderPath, loaded.LastFolderPath);
        Assert.Equal(updated.Sort, loaded.Sort);
        Assert.Equal(updated.IncludeSubfolders, loaded.IncludeSubfolders);
    }

    [Fact]
    public async Task LoadAsync_ignores_legacy_favorite_folders()
    {
        using var temp = TempWorkspace.Create();
        var settingsPath = Path.Combine(temp.Root, "settings.json");
        await File.WriteAllTextAsync(
            settingsPath,
            """
            {
              "version": 1,
              "lastFolderPath": "C:\\Images",
              "sort": { "key": 0, "direction": 0 },
              "includeSubfolders": false,
              "favoriteFolders": [
                { "id": "user:old", "path": "C:\\Old", "source": "User", "order": 0 }
              ]
            }
            """);
        var store = new JsonSettingsStore(settingsPath);

        var loaded = await store.LoadAsync();

        Assert.Equal(@"C:\Images", loaded.LastFolderPath);
        Assert.Equal(new SortState(SortKey.Name, SortDirection.Asc), loaded.Sort);
        Assert.False(loaded.IncludeSubfolders);
    }
}
