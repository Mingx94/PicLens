using ImageViewerWin.Core.Domain;
using ImageViewerWin.Core.Models;

namespace ImageViewerWin.Core.Tests;

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
        var staticGif = new byte[] { (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a', 0, 0, 0, 0, 0x2c };
        var animatedGif = new byte[] { (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a', 0, 0, 0, 0, 0x2c, 1, 0x2c };

        Assert.False(ImageFormatRules.IsAnimatedGif(staticGif));
        Assert.True(ImageFormatRules.IsAnimatedGif(animatedGif));
    }

    [Fact]
    public void Animated_webp_detection_uses_riff_webp_header_and_anim_marker()
    {
        var staticWebp = "RIFF----WEBPVP8 "u8.ToArray();
        var animatedWebp = "RIFF----WEBPVP8X....ANIM"u8.ToArray();

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
    public void Settings_patch_merges_sort_and_normalizes_user_favorites()
    {
        var current = AppSettings.CreateDefault() with
        {
            FavoriteFolders =
            [
                new FavoriteFolder("system:pictures", @"C:\Users\Me\Pictures", FavoriteSource.System, 0),
                new FavoriteFolder("user:old", @"C:\Old", FavoriteSource.User, 7)
            ]
        };

        var merged = SettingsRules.MergeSettingsPatch(
            current,
            new AppSettingsPatch
            {
                Sort = new SortState(SortKey.ModifiedAt, SortDirection.Desc),
                FavoriteFolders =
                [
                    new FavoriteFolder("system:desktop", @"C:\Users\Me\Desktop", FavoriteSource.System, 0),
                    new FavoriteFolder("user:a", @"C:\A", FavoriteSource.User, 99),
                    new FavoriteFolder("user:b", @"C:\B", FavoriteSource.User, 42)
                ]
            });

        Assert.Equal(1, merged.Version);
        Assert.Equal(new SortState(SortKey.ModifiedAt, SortDirection.Desc), merged.Sort);
        Assert.Equal(["user:a", "user:b"], merged.FavoriteFolders.Select(folder => folder.Id));
        Assert.Equal([0, 1], merged.FavoriteFolders.Select(folder => folder.Order));
        Assert.All(merged.FavoriteFolders, folder => Assert.Equal(FavoriteSource.User, folder.Source));
    }

    [Fact]
    public void Startup_folder_uses_first_available_favorite_when_last_folder_is_missing_or_unavailable()
    {
        var favorites = new[]
        {
            new FavoriteFolder("unavailable", @"C:\Missing", FavoriteSource.System, 0, IsAvailable: false),
            new FavoriteFolder("available", @"C:\Pictures", FavoriteSource.System, 1, IsAvailable: true)
        };

        Assert.Equal(@"C:\Pictures", StartupFolderSelector.SelectInitialFolder(null, favorites));
        Assert.Equal(@"C:\Pictures", StartupFolderSelector.SelectInitialFolder(@"C:\Missing", favorites));
        Assert.Equal(@"D:\Manual", StartupFolderSelector.SelectInitialFolder(@"D:\Manual", favorites));
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
