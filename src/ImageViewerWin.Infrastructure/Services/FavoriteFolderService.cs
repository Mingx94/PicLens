using ImageViewerWin.Application.Services;
using ImageViewerWin.Core.Domain;
using ImageViewerWin.Core.Models;

namespace ImageViewerWin.Infrastructure.Services;

public sealed class FavoriteFolderService : IFavoriteFolderService
{
    private readonly ISettingsStore settingsStore;

    public FavoriteFolderService(ISettingsStore settingsStore)
    {
        this.settingsStore = settingsStore;
    }

    public async Task<IReadOnlyList<FavoriteFolder>> GetFavoriteFoldersAsync(CancellationToken cancellationToken = default)
    {
        var settings = await settingsStore.LoadAsync(cancellationToken);
        var systemFavorites = GetSystemFavorites();
        var userFavorites = SettingsRules.NormalizeUserFavoriteFolders(settings.FavoriteFolders);

        return systemFavorites
            .Concat(userFavorites)
            .Select(folder => folder with { IsAvailable = Directory.Exists(folder.Path) })
            .ToList();
    }

    public async Task<IReadOnlyList<FavoriteFolder>> SaveUserFavoritesAsync(
        IEnumerable<FavoriteFolder> favorites,
        CancellationToken cancellationToken = default)
    {
        var normalized = SettingsRules.NormalizeUserFavoriteFolders(favorites);
        await settingsStore.UpdateAsync(new AppSettingsPatch { FavoriteFolders = normalized }, cancellationToken);
        return normalized;
    }

    private static IReadOnlyList<FavoriteFolder> GetSystemFavorites()
    {
        var folders = new List<(string Id, Environment.SpecialFolder Folder, string Name)>
        {
            ("system:pictures", Environment.SpecialFolder.MyPictures, "Pictures"),
            ("system:downloads", Environment.SpecialFolder.UserProfile, "Downloads"),
            ("system:desktop", Environment.SpecialFolder.DesktopDirectory, "Desktop")
        };

        return folders
            .Select((folder, index) =>
            {
                var path = folder.Folder == Environment.SpecialFolder.UserProfile
                    ? Path.Combine(Environment.GetFolderPath(folder.Folder), "Downloads")
                    : Environment.GetFolderPath(folder.Folder);

                return new FavoriteFolder(folder.Id, path, FavoriteSource.System, index, folder.Name);
            })
            .Where(folder => !string.IsNullOrWhiteSpace(folder.Path))
            .ToList();
    }
}
