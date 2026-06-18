using PicLens.Core.Models;
using System.Threading.Tasks;

namespace PicLens.Services;

public interface IDialogService
{
    Task<string?> ChooseFolderAsync();
    Task<bool> ConfirmAsync(string message, string title, string confirmButtonText);
    Task<bool> ConfirmDropRenameAsync(DropRenamePreview preview) => Task.FromResult(true);
    Task<string?> RequestRenameAsync(ImageListItem item);
}

public sealed record DropRenamePreview(
    int Total,
    int RenameCount,
    int SkippedCount,
    IReadOnlyList<DropRenamePreviewItem> Items);

public sealed record DropRenamePreviewItem(
    string SourcePath,
    string SourceName,
    string TargetPath,
    string TargetName,
    bool WillRename,
    string? Reason);
