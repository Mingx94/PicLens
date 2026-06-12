using PicLens.Core.Models;
using PicLens.Core.Domain;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PicLens.ViewModels;

public sealed record LibraryTileItem(
    string Name,
    string Path,
    string Detail,
    bool IsFolder,
    bool IsSelected,
    bool IsAnimated,
    string IconGlyph,
    ListItem SourceItem,
    string? InitialThumbnailPath = null)
    : INotifyPropertyChanged
{
    private string? thumbnailPath = InitialThumbnailPath;
    private int? thumbnailSize;
    private int tileWidth = SettingsRules.DefaultThumbnailSize;
    private int tileHeight = SettingsRules.DefaultThumbnailSize - 4;
    private bool isDropRenameTarget;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string KindLabel => IsFolder ? "資料夾" : IsAnimated ? "不支援動畫圖片" : "圖片";

    public string AutomationName => $"{Name}，{KindLabel}，{Detail}";

    public string? ThumbnailPath
    {
        get => thumbnailPath;
        private set
        {
            if (thumbnailPath == value)
            {
                return;
            }

            thumbnailPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanShowThumbnail));
            OnPropertyChanged(nameof(ShouldShowIcon));
        }
    }

    public bool CanShowThumbnail => !string.IsNullOrWhiteSpace(ThumbnailPath) && !IsAnimated;

    public bool ShouldShowIcon => !CanShowThumbnail;

    public bool IsDropRenameTarget
    {
        get => isDropRenameTarget;
        set => SetProperty(ref isDropRenameTarget, value);
    }

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
        if (this.thumbnailSize.HasValue && this.thumbnailSize.Value != normalizedSize)
        {
            ClearThumbnail();
        }
    }

    public bool HasThumbnailFor(int thumbnailSize)
    {
        var normalizedSize = SettingsRules.NormalizeThumbnailSize(thumbnailSize);
        return this.thumbnailSize == normalizedSize && !string.IsNullOrWhiteSpace(ThumbnailPath);
    }

    public void ApplyThumbnailPath(string? thumbnailPath, int thumbnailSize)
    {
        var normalizedSize = SettingsRules.NormalizeThumbnailSize(thumbnailSize);
        this.thumbnailSize = string.IsNullOrWhiteSpace(thumbnailPath) ? null : normalizedSize;
        ThumbnailPath = thumbnailPath;
    }

    public void ClearThumbnail()
    {
        thumbnailSize = null;
        ThumbnailPath = null;
    }

    private void SetProperty(ref int field, int value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void SetProperty(ref bool field, bool value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
