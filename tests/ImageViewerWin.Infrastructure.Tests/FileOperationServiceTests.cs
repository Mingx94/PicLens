using ImageViewerWin.Core.Models;
using ImageViewerWin.Infrastructure.Services;

namespace ImageViewerWin.Infrastructure.Tests;

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
        var encoder = new RecordingJpegEncoder();
        var service = new FileOperationService(encoder, new RecordingRecycleBin());

        var result = await service.ConvertVisibleToJpgAsync(
        [
            Image(png),
            Image(webp),
            Image(jpg)
        ]);

        Assert.Equal(3, result.Total);
        Assert.Equal(1, result.Succeeded);
        Assert.Equal(2, result.Skipped);
        Assert.True(File.Exists(png));
        Assert.True(File.Exists(Path.Combine(temp.Root, "a.jpg")));
        Assert.Equal([png], encoder.ConvertedSources);
        Assert.Contains(result.Items, item => item.Path == webp && item.Status == FileOperationStatus.Skipped && item.Reason == "target_exists");
        Assert.Contains(result.Items, item => item.Path == jpg && item.Status == FileOperationStatus.Skipped && item.Reason == "already_jpg");
    }

    [Fact]
    public async Task ConvertVisibleToJpgAsync_skips_animated_images()
    {
        using var temp = TempWorkspace.Create();
        var gif = await temp.WriteFileAsync("loop.gif", [1, 2, 3]);
        var encoder = new RecordingJpegEncoder();
        var service = new FileOperationService(encoder, new RecordingRecycleBin());

        var result = await service.ConvertVisibleToJpgAsync([Image(gif) with { IsAnimated = true }]);

        var item = Assert.Single(result.Items);
        Assert.Equal(FileOperationStatus.Skipped, item.Status);
        Assert.Equal("animated_unsupported", item.Reason);
        Assert.Empty(encoder.ConvertedSources);
    }

    [Fact]
    public async Task TrashSameBasenameNonJpgAsync_trashes_only_non_jpg_matches()
    {
        using var temp = TempWorkspace.Create();
        var jpg = await temp.WriteFileAsync("a.jpg", [1]);
        var matchingPng = await temp.WriteFileAsync("a.png", [2]);
        var unrelatedWebp = await temp.WriteFileAsync("b.webp", [3]);
        var recycleBin = new RecordingRecycleBin();
        var service = new FileOperationService(new RecordingJpegEncoder(), recycleBin);

        var result = await service.TrashSameBasenameNonJpgAsync([Image(jpg), Image(matchingPng), Image(unrelatedWebp)]);

        Assert.Equal(3, result.Total);
        Assert.Equal(1, result.Succeeded);
        Assert.Equal(2, result.Skipped);
        Assert.Equal([matchingPng], recycleBin.TrashedPaths);
        Assert.Contains(result.Items, item => item.Path == jpg && item.Reason == "already_jpg");
        Assert.Contains(result.Items, item => item.Path == unrelatedWebp && item.Reason == "no_matching_jpg");
    }

    [Fact]
    public async Task RenameAsync_accepts_leaf_supported_name_and_reports_collisions_as_invalid_requests()
    {
        using var temp = TempWorkspace.Create();
        var source = await temp.WriteFileAsync("old.png", [1]);
        await temp.WriteFileAsync("taken.png", [2]);
        var service = new FileOperationService(new RecordingJpegEncoder(), new RecordingRecycleBin());

        var collision = await service.RenameAsync(source, "taken.png");
        var renamed = await service.RenameAsync(source, "new.png");

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
        var service = new FileOperationService(new RecordingJpegEncoder(), new RecordingRecycleBin());

        var sameName = await service.RenameAsync(source, "old.png");
        var collision = await service.RenameAsync(source, "taken.png");

        Assert.Equal(FileOperationStatus.Skipped, sameName.Status);
        Assert.Equal("same_name", sameName.Reason);
        Assert.Equal(FileOperationStatus.Failed, collision.Status);
        Assert.Equal("invalid_request", collision.Reason);
        Assert.Equal("已有相同名稱的檔案。", collision.Message);
    }

    [Fact]
    public async Task RenameByDropTargetAsync_uses_selection_order_and_advances_past_existing_targets()
    {
        using var temp = TempWorkspace.Create();
        var target = await temp.WriteFileAsync("Album.jpg", [1]);
        var first = await temp.WriteFileAsync("first.png", [2]);
        var already = await temp.WriteFileAsync("Album-99.gif", [3]);
        var second = await temp.WriteFileAsync("second.webp", [4]);
        await temp.WriteFileAsync("Album-01.png", [5]);
        await temp.WriteFileAsync("Album-03.webp", [5]);
        var service = new FileOperationService(new RecordingJpegEncoder(), new RecordingRecycleBin());

        var result = await service.RenameByDropTargetAsync([first, target, already, second], target);

        Assert.Equal(3, result.Total);
        Assert.Equal(2, result.Succeeded);
        Assert.Equal(1, result.Skipped);
        Assert.True(File.Exists(Path.Combine(temp.Root, "Album-02.png")));
        Assert.True(File.Exists(Path.Combine(temp.Root, "Album-04.webp")));
        Assert.True(File.Exists(already));
        Assert.Contains(result.Items, item => item.Path == already && item.Reason == "already_target_sequence");
    }

    [Fact]
    public async Task TrashAsync_delegates_existing_path_to_recycle_bin()
    {
        using var temp = TempWorkspace.Create();
        var file = await temp.WriteFileAsync("delete-me.jpg", [1]);
        var recycleBin = new RecordingRecycleBin();
        var service = new FileOperationService(new RecordingJpegEncoder(), recycleBin);

        var result = await service.TrashAsync(file);

        Assert.Equal(FileOperationStatus.Trashed, result.Status);
        Assert.Equal([file], recycleBin.TrashedPaths);
    }

    [Fact]
    public async Task WinRTJpegEncoder_outputs_jpeg_bytes()
    {
        using var temp = TempWorkspace.Create();
        var source = await temp.WriteFileAsync("source.bmp", OnePixelBmp());
        var target = Path.Combine(temp.Root, "source.jpg");

        await new WinRTJpegEncoder().EncodeAsJpegAsync(source, target);

        var bytes = await File.ReadAllBytesAsync(target);
        Assert.True(bytes.Length > 2);
        Assert.Equal(0xFF, bytes[0]);
        Assert.Equal(0xD8, bytes[1]);
    }

    private static ImageListItem Image(string path) =>
        new(
            Id: $"image:{Path.GetFileName(path)}",
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

    private sealed class RecordingJpegEncoder : IJpegEncoder
    {
        public List<string> ConvertedSources { get; } = [];

        public async Task EncodeAsJpegAsync(string sourcePath, string targetPath, CancellationToken cancellationToken = default)
        {
            ConvertedSources.Add(sourcePath);
            await File.WriteAllBytesAsync(targetPath, [9, 9, 9], cancellationToken);
        }
    }

    private sealed class RecordingRecycleBin : IRecycleBin
    {
        public List<string> TrashedPaths { get; } = [];

        public Task TrashAsync(string path, CancellationToken cancellationToken = default)
        {
            TrashedPaths.Add(path);
            return Task.CompletedTask;
        }
    }
}
