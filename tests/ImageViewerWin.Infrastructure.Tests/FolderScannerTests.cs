using ImageViewerWin.Core.Models;
using ImageViewerWin.Infrastructure.Services;

namespace ImageViewerWin.Infrastructure.Tests;

public sealed class FolderScannerTests
{
    [Fact]
    public async Task ScanAsync_direct_mode_returns_child_folders_and_supported_images_only()
    {
        using var temp = TempWorkspace.Create();
        Directory.CreateDirectory(Path.Combine(temp.Root, "Nested"));
        await File.WriteAllBytesAsync(Path.Combine(temp.Root, "b10.jpg"), [1, 2, 3]);
        await File.WriteAllBytesAsync(Path.Combine(temp.Root, "b2.txt"), [1, 2, 3]);
        await File.WriteAllBytesAsync(Path.Combine(temp.Root, "loop.gif"), StaticGifBytes());
        await File.WriteAllBytesAsync(Path.Combine(temp.Root, "Nested", "deep.png"), [1, 2, 3]);
        var scanner = new FolderScanner();

        var items = await scanner.ScanAsync(new ListQuery(
            temp.Root,
            IncludeSubfolders: false,
            Sort: new SortState(SortKey.Name, SortDirection.Asc)));

        Assert.Equal(["Nested", "b10.jpg", "loop.gif"], items.Select(item => item.Name));
        Assert.Contains(items.OfType<ImageListItem>(), image => image.Name == "loop.gif" && image.IsAnimated == false);
        Assert.DoesNotContain(items, item => item.Name == "deep.png");
    }

    [Fact]
    public async Task ScanAsync_recursive_mode_returns_supported_images_from_descendants_and_marks_animated_images()
    {
        using var temp = TempWorkspace.Create();
        var nested = Directory.CreateDirectory(Path.Combine(temp.Root, "Nested")).FullName;
        await File.WriteAllBytesAsync(Path.Combine(temp.Root, "cover.webp"), AnimatedWebpBytes());
        await File.WriteAllBytesAsync(Path.Combine(nested, "z.png"), [1, 2, 3]);
        await File.WriteAllBytesAsync(Path.Combine(nested, "ignored.txt"), [1, 2, 3]);
        var scanner = new FolderScanner();

        var items = await scanner.ScanAsync(new ListQuery(
            temp.Root,
            IncludeSubfolders: true,
            Sort: new SortState(SortKey.Name, SortDirection.Asc)));

        Assert.Equal(["cover.webp", "z.png"], items.Select(item => item.Name));
        Assert.All(items, item => Assert.IsType<ImageListItem>(item));
        Assert.Contains(items.OfType<ImageListItem>(), image => image.Name == "cover.webp" && image.IsAnimated);
    }

    [Fact]
    public async Task ScanAsync_recursive_mode_visits_canonical_directories_once()
    {
        using var temp = TempWorkspace.Create();
        var realFolder = Directory.CreateDirectory(Path.Combine(temp.Root, "Real")).FullName;
        var aliasFolder = Path.Combine(temp.Root, "Alias");
        await File.WriteAllBytesAsync(Path.Combine(realFolder, "photo.jpg"), [1, 2, 3]);
        CreateDirectoryAlias(aliasFolder, realFolder);
        var scanner = new FolderScanner();

        var items = await scanner.ScanAsync(new ListQuery(
            temp.Root,
            IncludeSubfolders: true,
            Sort: new SortState(SortKey.Name, SortDirection.Asc)));

        Assert.Equal(["photo.jpg"], items.Select(item => item.Name));
    }

    private static byte[] StaticGifBytes() =>
        [(byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a', 0, 0, 0, 0, 0x2c];

    private static byte[] AnimatedWebpBytes() =>
        "RIFF----WEBPVP8X....ANIM"u8.ToArray();

    private static void CreateDirectoryAlias(string aliasPath, string targetPath)
    {
        Directory.CreateSymbolicLink(aliasPath, targetPath);
    }
}
