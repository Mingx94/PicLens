namespace ImageViewerWin.Application.Services;

public interface IImageDataService
{
    Task<byte[]> ReadImageBytesAsync(string path, CancellationToken cancellationToken = default);

    Task<string> GetImageDataUriAsync(string path, CancellationToken cancellationToken = default);
}
