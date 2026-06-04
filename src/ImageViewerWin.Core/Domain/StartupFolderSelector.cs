using ImageViewerWin.Core.Models;

namespace ImageViewerWin.Core.Domain;

public static class StartupFolderSelector
{
    public static string? FirstAvailableFavoritePath(IEnumerable<FavoriteFolder> favorites) =>
        favorites.FirstOrDefault(favorite => favorite.IsAvailable != false)?.Path;

    public static string? SelectInitialFolder(string? lastFolderPath, IEnumerable<FavoriteFolder> favorites)
    {
        var favoriteList = favorites.ToList();
        var fallback = FirstAvailableFavoritePath(favoriteList);

        if (lastFolderPath is null)
        {
            return fallback;
        }

        var matchingFavorite = favoriteList.FirstOrDefault(favorite => favorite.Path == lastFolderPath);
        return matchingFavorite is { IsAvailable: false } ? fallback : lastFolderPath;
    }
}
