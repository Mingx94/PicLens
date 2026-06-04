using ImageViewerWin.Core.Models;

namespace ImageViewerWin.ViewModels;

public sealed record FavoriteSidebarItem(
    string Name,
    string Path,
    FavoriteSource Source,
    bool IsAvailable)
{
    public string SourceLabel => Source == FavoriteSource.System ? "System" : "User";
    public string AvailabilityLabel => IsAvailable ? "Available" : "Unavailable";
}
