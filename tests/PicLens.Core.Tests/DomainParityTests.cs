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
    public void Animated_gif_detection_counts_multiple_image_descriptors()
    {
        var staticGif = new byte[] { (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a', 0, 0, 0, 0, 0, 0, 0, 0x2c, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0x3b };
        var animatedGif = new byte[] { (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a', 0, 0, 0, 0, 0, 0, 0, 0x2c, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0x2c, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0x3b };

        Assert.False(ImageFormatRules.IsAnimatedGif(staticGif));
        Assert.True(ImageFormatRules.IsAnimatedGif(animatedGif));
    }

    [Fact]
    public void Animated_webp_detection_uses_riff_webp_header_and_anim_marker()
    {
        var staticWebp = new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P', (byte)'V', (byte)'P', (byte)'8', (byte)'X', 0, 0, 0, 0, 0 };
        var animatedWebp = new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P', (byte)'V', (byte)'P', (byte)'8', (byte)'X', 0, 0, 0, 0, 0x02 };

        Assert.False(ImageFormatRules.IsAnimatedWebp(staticWebp));
        Assert.True(ImageFormatRules.IsAnimatedWebp(animatedWebp));
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
            new SortOptions(KeepFoldersFirst: true));

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
            new SortOptions(KeepFoldersFirst: false));

        Assert.Equal(["img1.jpg", "img002.jpg", "img02.jpg", "img2.jpg", "img10.jpg"], sorted.Select(item => item.Name));
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

        Assert.Equal(1, merged.Version);
        Assert.Equal(@"D:\Manual", merged.LastFolderPath);
        Assert.Equal(new SortState(SortKey.ModifiedAt, SortDirection.Desc), merged.Sort);
        Assert.True(merged.IncludeSubfolders);
        Assert.Equal(SettingsRules.DefaultThumbnailSize, merged.ThumbnailSize);
    }

    [Theory]
    [InlineData(64, 140)]
    [InlineData(188, 180)]
    [InlineData(226, 200)]
    [InlineData(625, 200)]
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
    public void Image_sequence_snapshot_is_immutable_and_requires_current_image()
    {
        var images = new List<ImageListItem>
        {
            Image("a.jpg", 10),
            Image("b.jpg", 20)
        };

        var snapshot = ImageSequenceFactory.Create(
            new CreateImageSequenceSnapshotInput(
                SourceFolderPath: @"C:\Images",
                IncludeSubfolders: true,
                Sort: new SortState(SortKey.Name, SortDirection.Asc),
                Images: images,
                CurrentImagePath: @"C:\Images\b.jpg",
                NowMs: 1234));

        Assert.Equal("sequence:C_3A_5CImages_3A1234_3AC_3A_5CImages_5Cb.jpg", snapshot.Id);
        Assert.Equal(1, snapshot.CurrentIndex);
        Assert.NotSame(images, snapshot.Images);

        Assert.Throws<InvalidOperationException>(() =>
            ImageSequenceFactory.Create(new CreateImageSequenceSnapshotInput(
                SourceFolderPath: @"C:\Images",
                IncludeSubfolders: false,
                Sort: new SortState(SortKey.Name, SortDirection.Asc),
                Images: images,
                CurrentImagePath: @"C:\Images\missing.jpg",
                NowMs: 1234)));
    }

    [Fact]
    public void Zoom_math_clamps_and_keeps_pointer_anchor()
    {
        Assert.Equal(0.1, ZoomMath.ClampZoom(0.01));
        Assert.Equal(8, ZoomMath.ClampZoom(80));

        var next = ZoomMath.ZoomAtPoint(new ZoomAtPointInput(
            Zoom: 1,
            Offset: new Point(0, 0),
            ViewportCenter: new Point(100, 100),
            Pointer: new Point(120, 100),
            Delta: 1));

        Assert.Equal(1.2, next.Zoom, precision: 10);
        Assert.Equal(-4, next.Offset.X, precision: 10);
        Assert.Equal(0, next.Offset.Y, precision: 10);
    }

    private static FolderListItem Folder(string name, long modifiedAtMs) =>
        new($"folder:{name}", $@"C:\Images\{name}", name, modifiedAtMs);

    private static ImageListItem Image(string name, long modifiedAtMs) =>
        new(
            Id: $"image:{name}",
            Path: $@"C:\Images\{name}",
            Name: name,
            Extension: name[(name.LastIndexOf('.') + 1)..],
            ModifiedAtMs: modifiedAtMs,
            SizeBytes: 100);
}
