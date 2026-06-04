using ImageViewerWin.Core.Models;

namespace ImageViewerWin.Application.Services;

public interface IFolderScanner
{
    Task<IReadOnlyList<ListItem>> ScanAsync(ListQuery query, CancellationToken cancellationToken = default);
}
