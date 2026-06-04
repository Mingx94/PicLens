using ImageViewerWin.Core.Models;
using ImageViewerWin.Infrastructure.Services;

namespace ImageViewerWin.Infrastructure.Tests;

public sealed class FavoriteFolderServiceTests
{
    [Fact]
    public async Task GetFavoriteFoldersAsync_combines_system_folders_with_normalized_user_favorites()
    {
        using var temp = TempWorkspace.Create();
        var userFavoritePath = Directory.CreateDirectory(Path.Combine(temp.Root, "User Favorite")).FullName;
        var settingsStore = new JsonSettingsStore(Path.Combine(temp.Root, "settings.json"));
        await settingsStore.SaveAsync(AppSettings.CreateDefault() with
        {
            FavoriteFolders =
            [
                new FavoriteFolder("user:custom", userFavoritePath, FavoriteSource.User, 10, "Custom")
            ]
        });

        var service = new FavoriteFolderService(settingsStore);

        var favorites = await service.GetFavoriteFoldersAsync();

        Assert.Contains(favorites, favorite => favorite.Source == FavoriteSource.System && favorite.Id == "system:pictures");
        Assert.Contains(favorites, favorite => favorite.Source == FavoriteSource.System && favorite.Id == "system:downloads");
        Assert.Contains(favorites, favorite => favorite.Source == FavoriteSource.System && favorite.Id == "system:desktop");
        Assert.DoesNotContain(favorites, favorite => favorite.Source == FavoriteSource.System && favorite.Id == "system:documents");
        var userFavorite = Assert.Single(favorites, favorite => favorite.Source == FavoriteSource.User);
        Assert.Equal("user:custom", userFavorite.Id);
        Assert.Equal(0, userFavorite.Order);
        Assert.True(userFavorite.IsAvailable);
        Assert.Equal("Custom", userFavorite.Name);
    }
}
