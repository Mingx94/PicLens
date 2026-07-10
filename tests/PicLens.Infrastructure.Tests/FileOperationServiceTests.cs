using PicLens.Core.Models;
using PicLens.Infrastructure.Services;

namespace PicLens.Infrastructure.Tests;

public sealed class FileOperationServiceTests
{
    [Fact]
    public async Task ConvertVisibleToJpgAsync_preserves_originals_and_skips_existing_targets()
    {
        using var temp = TempWorkspace.Create();
        var png = await temp.WriteFileAsync("a.png", [1, 2, 3]);
        var webp = await temp.WriteFileAsync("b.webp", [4, 5, 6]);
        var jpg = await temp.WriteFileAsync("c.jpg", [7, 8, 9]);
        await temp.WriteFileAsync("b.jpg", [0]);
        var convertedSources = new List<string>();
        var service = CreateService(convertedSources: convertedSources);

        var result = await service.ConvertVisibleToJpgAsync(
        [
            Image(png),
            Image(webp),
            Image(jpg)
        ], TestContext.Current.CancellationToken);

        Assert.Equal(3, result.Total);
        Assert.Equal(1, result.Succeeded);
        Assert.Equal(2, result.Skipped);
        Assert.True(File.Exists(png));
        Assert.True(File.Exists(Path.Combine(temp.Root, "a.jpg")));
        Assert.Equal([png], convertedSources);
        Assert.Contains(result.Items, item => item.Path == webp && item.Status == FileOperationStatus.Skipped && item.Reason == "target_exists");
        Assert.Contains(result.Items, item => item.Path == jpg && item.Status == FileOperationStatus.Skipped && item.Reason == "already_jpg");
    }

    [Fact]
    public async Task ConvertVisibleToJpgAsync_skips_animated_images()
    {
        using var temp = TempWorkspace.Create();
        var gif = await temp.WriteFileAsync("loop.gif", [1, 2, 3]);
        var convertedSources = new List<string>();
        var service = CreateService(convertedSources: convertedSources);

        var result = await service.ConvertVisibleToJpgAsync([Image(gif) with { IsAnimated = true }], TestContext.Current.CancellationToken);

        var item = Assert.Single(result.Items);
        Assert.Equal(FileOperationStatus.Skipped, item.Status);
        Assert.Equal("animated_unsupported", item.Reason);
        Assert.Empty(convertedSources);
    }

    [Fact]
    public async Task TrashSameBasenameNonJpgAsync_trashes_only_non_jpg_matches()
    {
        using var temp = TempWorkspace.Create();
        var jpg = await temp.WriteFileAsync("a.jpg", [1]);
        var matchingPng = await temp.WriteFileAsync("a.png", [2]);
        var unrelatedWebp = await temp.WriteFileAsync("b.webp", [3]);
        var trashedPaths = new List<string>();
        var service = CreateService(trashedPaths: trashedPaths);

        var result = await service.TrashSameBasenameNonJpgAsync([Image(jpg), Image(matchingPng), Image(unrelatedWebp)], TestContext.Current.CancellationToken);

        Assert.Equal(3, result.Total);
        Assert.Equal(1, result.Succeeded);
        Assert.Equal(2, result.Skipped);
        Assert.Equal([matchingPng], trashedPaths);
        Assert.Contains(result.Items, item => item.Path == jpg && item.Reason == "already_jpg");
        Assert.Contains(result.Items, item => item.Path == unrelatedWebp && item.Reason == "no_matching_jpg");
    }

    [Fact]
    public async Task RenameAsync_accepts_leaf_supported_name_and_reports_collisions_as_invalid_requests()
    {
        using var temp = TempWorkspace.Create();
        var source = await temp.WriteFileAsync("old.png", [1]);
        await temp.WriteFileAsync("taken.png", [2]);
        var service = CreateService();

        var collision = await service.RenameAsync(source, "taken.png", TestContext.Current.CancellationToken);
        var renamed = await service.RenameAsync(source, "new.png", TestContext.Current.CancellationToken);

        Assert.Equal(FileOperationStatus.Failed, collision.Status);
        Assert.Equal("invalid_request", collision.Reason);
        Assert.Equal("已有相同名稱的檔案。", collision.Message);
        Assert.Equal(FileOperationStatus.Renamed, renamed.Status);
        Assert.Equal(Path.Combine(temp.Root, "new.png"), renamed.TargetPath);
        Assert.False(File.Exists(source));
    }

    [Fact]
    public async Task RenameAsync_uses_electron_parity_reasons_for_same_name_and_collisions()
    {
        using var temp = TempWorkspace.Create();
        var source = await temp.WriteFileAsync("old.png", [1]);
        await temp.WriteFileAsync("taken.png", [2]);
        var service = CreateService();

        var sameName = await service.RenameAsync(source, "old.png", TestContext.Current.CancellationToken);
        var collision = await service.RenameAsync(source, "taken.png", TestContext.Current.CancellationToken);

        Assert.Equal(FileOperationStatus.Skipped, sameName.Status);
        Assert.Equal("same_name", sameName.Reason);
        Assert.Equal(FileOperationStatus.Failed, collision.Status);
        Assert.Equal("invalid_request", collision.Reason);
        Assert.Equal("已有相同名稱的檔案。", collision.Message);
    }

    [Fact]
    public async Task RenameByDropTargetAsync_uses_selection_order_and_advances_past_existing_sequence_names()
    {
        using var temp = TempWorkspace.Create();
        var target = await temp.WriteFileAsync("Album.jpg", [1]);
        var first = await temp.WriteFileAsync("first.png", [2]);
        var already = await temp.WriteFileAsync("Album-03.gif", [3]);
        var second = await temp.WriteFileAsync("second.webp", [4]);
        await temp.WriteFileAsync("Album-01.jpg", [5]);
        var service = CreateService();

        var result = await service.RenameByDropTargetAsync([first, target, already, second], target, TestContext.Current.CancellationToken);

        Assert.Equal(3, result.Total);
        Assert.Equal(2, result.Succeeded);
        Assert.Equal(1, result.Skipped);
        Assert.True(File.Exists(Path.Combine(temp.Root, "Album-02.png")));
        Assert.True(File.Exists(Path.Combine(temp.Root, "Album-04.webp")));
        Assert.True(File.Exists(already));
        Assert.Contains(result.Items, item => item.Path == already && item.Reason == "already_target_sequence");
    }

    [Fact]
    public async Task RenameByDropTargetAsync_compacts_existing_sequence_source_into_first_gap()
    {
        using var temp = TempWorkspace.Create();
        var target = await temp.WriteFileAsync("Album.jpg", [1]);
        var source = await temp.WriteFileAsync("Album-03.jpg", [2]);
        var service = CreateService();

        var result = await service.RenameByDropTargetAsync([source], target, TestContext.Current.CancellationToken);

        var item = Assert.Single(result.Items);
        Assert.Equal(FileOperationStatus.Renamed, item.Status);
        Assert.Equal(Path.Combine(temp.Root, "Album-01.jpg"), item.TargetPath);
        Assert.True(File.Exists(item.TargetPath));
        Assert.False(File.Exists(source));
    }

    [Fact]
    public async Task TrashAsync_delegates_existing_path_to_recycle_bin()
    {
        using var temp = TempWorkspace.Create();
        var file = await temp.WriteFileAsync("delete-me.jpg", [1]);
        var trashedPaths = new List<string>();
        var service = CreateService(trashedPaths: trashedPaths);

        var result = await service.TrashAsync(file, TestContext.Current.CancellationToken);

        Assert.Equal(FileOperationStatus.Trashed, result.Status);
        Assert.Equal([file], trashedPaths);
    }

    [Fact]
    public async Task ConvertVisibleToJpgAsync_default_encoder_outputs_jpeg_bytes()
    {
        using var temp = TempWorkspace.Create();
        var source = await temp.WriteFileAsync("source.bmp", OnePixelBmp());
        var target = Path.Combine(temp.Root, "source.jpg");
        var service = new FileOperationService();

        var result = await service.ConvertVisibleToJpgAsync([Image(source)], TestContext.Current.CancellationToken);

        Assert.Equal(FileOperationStatus.Converted, Assert.Single(result.Items).Status);
        var bytes = await File.ReadAllBytesAsync(target, TestContext.Current.CancellationToken);
        Assert.True(bytes.Length > 2);
        Assert.Equal(0xFF, bytes[0]);
        Assert.Equal(0xD8, bytes[1]);
    }

    private static ImageListItem Image(string path) =>
        new(
            Path: path,
            Name: Path.GetFileName(path),
            Extension: Path.GetExtension(path).TrimStart('.').ToLowerInvariant(),
            ModifiedAtMs: null,
            SizeBytes: 1);

    private static byte[] OnePixelBmp() =>
    [
        0x42, 0x4D, 0x3A, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x36, 0x00, 0x00, 0x00, 0x28, 0x00,
        0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00,
        0x00, 0x00, 0x01, 0x00, 0x18, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x13, 0x0B,
        0x00, 0x00, 0x13, 0x0B, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0xFF, 0x00,
    ];

    private static FileOperationService CreateService(
        List<string>? convertedSources = null,
        List<string>? trashedPaths = null) =>
        new(
            async (sourcePath, targetPath, cancellationToken) =>
            {
                convertedSources?.Add(sourcePath);
                await File.WriteAllBytesAsync(targetPath, [9, 9, 9], cancellationToken);
            },
            (path, _) =>
            {
                trashedPaths?.Add(path);
                return Task.CompletedTask;
            });
}
