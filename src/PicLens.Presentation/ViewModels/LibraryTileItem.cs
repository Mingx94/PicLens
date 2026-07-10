using CommunityToolkit.Mvvm.ComponentModel;
using PicLens.Core.Models;
using PicLens.Core.Domain;

namespace PicLens.ViewModels;

public sealed class LibraryTileItem : ObservableObject
{
    private string? thumbnailPath;
    private int? thumbnailSize;
    private bool isSelected;
    private bool isDropRenameTarget;

    public LibraryTileItem(ListItem sourceItem) => SourceItem = sourceItem;

    public string Name => SourceItem.Name;
    public string Path => SourceItem.Path;
    public string Detail => SourceItem is FolderListItem
        ? "開啟資料夾"
        : SourceItem is ImageListItem image
            ? $"{image.Extension.ToUpperInvariant()} - {image.SizeBytes / 1024} KB"
            : string.Empty;
    public bool IsFolder => SourceItem is FolderListItem;
    public bool IsAnimated => SourceItem is ImageListItem { IsAnimated: true };
    public bool IsStillImage => SourceItem is ImageListItem { IsAnimated: false };
    public ListItem SourceItem { get; }

    public string KindLabel => IsFolder ? "資料夾" : IsAnimated ? "不支援動畫圖片" : "圖片";

    public string AutomationName => $"{Name}，{KindLabel}，{Detail}";

    public string AutomationId => $"{(IsFolder ? "LibraryFolderTile" : "LibraryImageTile")}_{SanitizeAutomationIdSegment(Name)}";

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

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    public bool IsDropRenameTarget
    {
        get => isDropRenameTarget;
        set => SetProperty(ref isDropRenameTarget, value);
    }

    public void ApplyThumbnailSize(int thumbnailSize)
    {
        var normalizedSize = SettingsRules.NormalizeThumbnailSize(thumbnailSize);
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

    private static string SanitizeAutomationIdSegment(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        return builder.ToString();
    }
}
