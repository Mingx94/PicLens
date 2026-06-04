namespace ImageViewerWin.ViewModels;

public sealed record LibraryTileItem(
    string Name,
    string Path,
    string Detail,
    bool IsFolder,
    bool IsSelected,
    bool IsAnimated,
    string IconGlyph)
{
    public string KindLabel => IsFolder ? "Folder" : IsAnimated ? "Animated image unsupported" : "Image";
}
