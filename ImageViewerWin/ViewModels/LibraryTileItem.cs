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
    public string KindLabel => IsFolder ? "資料夾" : IsAnimated ? "不支援動畫圖片" : "圖片";

    public bool CanShowThumbnail => !string.IsNullOrWhiteSpace(ThumbnailPath) && !IsAnimated;

    public bool ShouldShowIcon => !CanShowThumbnail;
}
