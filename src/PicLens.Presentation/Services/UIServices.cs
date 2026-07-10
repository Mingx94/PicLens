using PicLens.Core.Models;
using PicLens.Core.Domain;

namespace PicLens.Services;

public interface IDialogService
{
    Task<string?> ChooseFolderAsync();
    Task<bool> ConfirmAsync(string message, string title, string confirmButtonText);
    Task<bool> ConfirmDropRenameAsync(DropTargetBatchRenamePlan plan);
    Task<string?> RequestRenameAsync(ImageListItem item);
}
