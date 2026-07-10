using PicLens.Core.Domain;
using PicLens.Core.Models;

namespace PicLens.Core.Tests;

public sealed class DomainParityTests
{
    [Theory]
    [InlineData(@"C:\Images\photo.JPG", "jpg")]
    [InlineData(@"C:\Images\photo.jpeg", "jpeg")]
    [InlineData(@"C:\Images\photo.png", "png")]
    [InlineData(@"C:\Images\photo.bmp", "bmp")]
    [InlineData(@"C:\Images\photo.webp", "webp")]
    [InlineData(@"C:\Images\photo.gif", "gif")]
    [InlineData(@"C:\Images\photo.avif", null)]
    [InlineData(@"C:\Images\README", null)]
    public void Supported_image_extension_matches_electron_contract(string path, string? expected)
    {
        Assert.Equal(expected, ImageFormatRules.GetSupportedImageExtension(path));
    }

    [Fact]
    public void Sort_keeps_folders_first_and_uses_numeric_name_order()
    {
        List<ListItem> items =
        [
            Image("b10.jpg", 10),
            Folder("z-folder", 5),
            Image("b2.jpg", 20),
            Image("b1.jpg", 30)
        ];

        var sorted = ListItemSorter.Sort(
            items,
            new SortState(SortKey.Name, SortDirection.Asc),
            keepFoldersFirst: true);

        Assert.Equal(["z-folder", "b1.jpg", "b2.jpg", "b10.jpg"], sorted.Select(item => item.Name));
    }

    [Fact]
    public void Sort_by_name_matches_windows_explorer_logical_order_for_leading_zero_numbers()
    {
        List<ListItem> items =
        [
            Image("img2.jpg", 20),
            Image("img02.jpg", 30),
            Image("img002.jpg", 40),
            Image("img10.jpg", 50),
            Image("img1.jpg", 10)
        ];

        var sorted = ListItemSorter.Sort(
            items,
            new SortState(SortKey.Name, SortDirection.Asc),
            keepFoldersFirst: false);

        Assert.Equal(["img1.jpg", "img002.jpg", "img02.jpg", "img2.jpg", "img10.jpg"], sorted.Select(item => item.Name));
    }

    [Fact]
    public void Path_rules_detect_same_basename_target_conflicts()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var otherRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string[] existingPaths =
        [
            Path.Combine(root, "target-01.png"),
            Path.Combine(root, "source.jpg"),
            Path.Combine(otherRoot, "target-01.webp")
        ];

        Assert.True(PathRules.TargetNameExists(existingPaths, Path.Combine(root, "target-01.jpg"), Path.Combine(root, "source.jpg")));
        Assert.False(PathRules.TargetNameExists(existingPaths, Path.Combine(root, "source.webp"), Path.Combine(root, "source.jpg")));
        Assert.False(PathRules.TargetNameExists(existingPaths, Path.Combine(root, "target-02.jpg"), Path.Combine(root, "source.jpg")));
    }

    [Fact]
    public void Path_rules_follow_current_os_case_sensitivity()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var lower = Path.Combine(root, "photo.jpg");
        var upper = Path.Combine(root, "PHOTO.jpg");

        Assert.Equal(OperatingSystem.IsWindows(), PathRules.PathEquals(lower, upper));
    }

    [Fact]
    public void Settings_patch_merges_last_folder_sort_and_recursive_mode()
    {
        var current = AppSettings.CreateDefault();

        var merged = SettingsRules.MergeSettingsPatch(
            current,
            new AppSettingsPatch
            {
                LastFolderPath = @"D:\Manual",
                HasLastFolderPath = true,
                Sort = new SortState(SortKey.ModifiedAt, SortDirection.Desc),
                IncludeSubfolders = true
            });

        Assert.Equal(@"D:\Manual", merged.LastFolderPath);
        Assert.Equal(new SortState(SortKey.ModifiedAt, SortDirection.Desc), merged.Sort);
        Assert.True(merged.IncludeSubfolders);
        Assert.Equal(SettingsRules.DefaultThumbnailSize, merged.ThumbnailSize);
    }

    [Theory]
    [InlineData(64, 120)]
    [InlineData(188, 180)]
    [InlineData(226, 220)]
    [InlineData(625, 240)]
    public void Settings_patch_normalizes_thumbnail_size(int requestedSize, int expectedSize)
    {
        var merged = SettingsRules.MergeSettingsPatch(
            AppSettings.CreateDefault(),
            new AppSettingsPatch
            {
                ThumbnailSize = requestedSize
            });

        Assert.Equal(expectedSize, merged.ThumbnailSize);
    }

    [Fact]
    public void Zoom_math_clamps_and_keeps_pointer_anchor()
    {
        Assert.Equal(0.1, ZoomMath.ClampZoom(0.01));
        Assert.Equal(8, ZoomMath.ClampZoom(80));

        var next = ZoomMath.ZoomAtPoint(
            zoom: 1,
            offset: new Point(0, 0),
            viewportCenter: new Point(100, 100),
            pointer: new Point(120, 100),
            delta: 1);

        Assert.Equal(1.2, next.Zoom, precision: 10);
        Assert.Equal(-4, next.Offset.X, precision: 10);
        Assert.Equal(0, next.Offset.Y, precision: 10);
    }

    private static FolderListItem Folder(string name, long modifiedAtMs) =>
        new($@"C:\Images\{name}", name, modifiedAtMs);

    private static ImageListItem Image(string name, long modifiedAtMs) =>
        new(
            Path: $@"C:\Images\{name}",
            Name: name,
            Extension: name[(name.LastIndexOf('.') + 1)..],
            ModifiedAtMs: modifiedAtMs,
            SizeBytes: 100);
}
