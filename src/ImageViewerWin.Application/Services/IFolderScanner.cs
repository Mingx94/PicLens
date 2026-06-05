using ImageViewerWin.Core.Models;

namespace ImageViewerWin.Application.Services;

public interface IFolderScanner
{
    Task<IReadOnlyList<ListItem>> ScanAsync(ListQuery query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FolderListItem>> ScanChildFoldersAsync(
        string folderPath,
        SortState sort,
        CancellationToken cancellationToken = default);
}
