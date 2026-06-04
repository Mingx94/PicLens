using ImageViewerWin.Core.Models;

namespace ImageViewerWin.Application.Services;

public interface ISettingsStore
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);

    Task<AppSettings> UpdateAsync(AppSettingsPatch patch, CancellationToken cancellationToken = default);
}
