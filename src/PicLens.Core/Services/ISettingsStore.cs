using PicLens.Core.Models;

namespace PicLens.Core.Services;

public interface ISettingsStore
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task<AppSettings> UpdateAsync(AppSettingsPatch patch, CancellationToken cancellationToken = default);
}
