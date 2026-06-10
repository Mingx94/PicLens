namespace PicLens.Application.Services;

public interface IThumbnailService
{
    Task<string?> GetOrCreateThumbnailAsync(
        string imagePath,
        int requestedSize,
        CancellationToken cancellationToken = default);
}
