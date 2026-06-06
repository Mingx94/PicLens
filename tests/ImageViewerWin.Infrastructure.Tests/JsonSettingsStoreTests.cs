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
    public async Task LoadAsync_returns_default_and_quarantines_invalid_json()
    {
        using var temp = TempWorkspace.Create();
        var settingsPath = Path.Combine(temp.Root, "settings.json");
        const string invalidJson = "{ invalid json";
        await File.WriteAllTextAsync(settingsPath, invalidJson);
        var store = new JsonSettingsStore(settingsPath);

        var settings = await store.LoadAsync();

        Assert.Equal(AppSettings.CreateDefault(), settings);
        Assert.False(File.Exists(settingsPath));
        var quarantinedFiles = Directory.GetFiles(temp.Root, "settings.json.corrupt.*");
        var quarantinedPath = Assert.Single(quarantinedFiles);
        Assert.Equal(invalidJson, await File.ReadAllTextAsync(quarantinedPath));

        var loadedAgain = await store.LoadAsync();

        Assert.Equal(AppSettings.CreateDefault(), loadedAgain);
        Assert.False(File.Exists(settingsPath));
        Assert.Equal(quarantinedPath, Assert.Single(Directory.GetFiles(temp.Root, "settings.json.corrupt.*")));
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
    public async Task SaveAsync_preserves_existing_settings_and_removes_temp_file_when_write_is_canceled()
    {
        using var temp = TempWorkspace.Create();
        var settingsPath = Path.Combine(temp.Root, "settings.json");
        var store = new JsonSettingsStore(settingsPath);
        var original = AppSettings.CreateDefault() with
        {
            LastFolderPath = temp.Root,
            ThumbnailSize = 300
        };
        await store.SaveAsync(original);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.SaveAsync(original with { IncludeSubfolders = true }, cancellation.Token));

        Assert.Equal(original, await store.LoadAsync());
        Assert.Empty(Directory.GetFiles(temp.Root, "*.tmp"));
    }

    [Fact]
    public async Task UpdateAsync_does_not_overwrite_existing_settings_when_read_failure_cannot_be_quarantined()
    {
        using var temp = TempWorkspace.Create();
        var settingsPath = Path.Combine(temp.Root, "settings.json");
        var store = new JsonSettingsStore(settingsPath);
        var original = AppSettings.CreateDefault() with
        {
            LastFolderPath = temp.Root,
            ThumbnailSize = 300
        };
        await store.SaveAsync(original);
        await using var locked = new FileStream(settingsPath, FileMode.Open, FileAccess.Read, FileShare.None);

        var exception = await Assert.ThrowsAsync<IOException>(() => store.UpdateAsync(new AppSettingsPatch
        {
            IncludeSubfolders = true
        }));

        Assert.Contains("update skipped", exception.Message);
        Assert.Empty(Directory.GetFiles(temp.Root, "*.tmp"));
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
