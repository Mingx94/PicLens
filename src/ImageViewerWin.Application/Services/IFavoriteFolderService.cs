using ImageViewerWin.Core.Models;

namespace ImageViewerWin.Application.Services;

public interface IFavoriteFolderService
{
    Task<IReadOnlyList<FavoriteFolder>> GetFavoriteFoldersAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FavoriteFolder>> SaveUserFavoritesAsync(
        IEnumerable<FavoriteFolder> favorites,
        CancellationToken cancellationToken = default);
}
