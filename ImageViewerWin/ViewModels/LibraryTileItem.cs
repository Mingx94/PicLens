using ImageViewerWin.Core.Models;
using ImageViewerWin.Core.Domain;
using System.ComponentModel;
using System.Runtime.CompilerServices;

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
    : INotifyPropertyChanged
{
    private int tileWidth = SettingsRules.DefaultThumbnailSize;
    private int tileHeight = SettingsRules.DefaultThumbnailSize - 4;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string KindLabel => IsFolder ? "資料夾" : IsAnimated ? "不支援動畫圖片" : "圖片";

    public bool CanShowThumbnail => !string.IsNullOrWhiteSpace(ThumbnailPath) && !IsAnimated;

    public bool ShouldShowIcon => !CanShowThumbnail;

    public int TileWidth
    {
        get => tileWidth;
        private set => SetProperty(ref tileWidth, value);
    }

    public int TileHeight
    {
        get => tileHeight;
        private set => SetProperty(ref tileHeight, value);
    }

    public void ApplyThumbnailSize(int thumbnailSize)
    {
        var normalizedSize = SettingsRules.NormalizeThumbnailSize(thumbnailSize);
        TileWidth = normalizedSize;
        TileHeight = normalizedSize - 4;
    }

    private void SetProperty(ref int field, int value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
