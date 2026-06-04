using ImageViewerWin.Core.Models;

namespace ImageViewerWin.ViewModels;

public sealed record LibraryTileItem(
    string Name,
    string Path,
    string Detail,
    bool IsFolder,
    bool IsSelected,
    bool IsAnimated,
    string IconGlyph,
    ListItem SourceItem,
    string? ThumbnailPath = null)
{
    public string KindLabel => IsFolder ? "Folder" : IsAnimated ? "Animated image unsupported" : "Image";

    public bool CanShowThumbnail => !string.IsNullOrWhiteSpace(ThumbnailPath) && !IsAnimated;

    public bool ShouldShowIcon => !CanShowThumbnail;
}
