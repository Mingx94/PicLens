using ImageViewerWin.Infrastructure.Services;

namespace ImageViewerWin.Infrastructure.Tests;

public sealed class ImageDataServiceTests
{
    [Fact]
    public async Task GetImageDataUriAsync_uses_supported_extension_mime_type_and_base64_file_contents()
    {
        using var temp = TempWorkspace.Create();
        var path = Path.Combine(temp.Root, "sample.png");
        await File.WriteAllBytesAsync(path, [1, 2, 3]);
        var service = new ImageDataService();

        var dataUri = await service.GetImageDataUriAsync(path);

        Assert.Equal("data:image/png;base64,AQID", dataUri);
    }

    [Fact]
    public async Task GetImageDataUriAsync_rejects_unsupported_images()
    {
        using var temp = TempWorkspace.Create();
        var path = Path.Combine(temp.Root, "sample.txt");
        await File.WriteAllTextAsync(path, "hello");
        var service = new ImageDataService();

        await Assert.ThrowsAsync<NotSupportedException>(() => service.GetImageDataUriAsync(path));
    }
}
