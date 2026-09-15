using PicLens.Core;
using PicLens.Imaging;
using PicLens.Services;
using SkiaSharp;
namespace PicLens.Tests;

public sealed class ServiceTests
{
    [Theory]
    [InlineData("png")]
    [InlineData("jpg")]
    [InlineData("jpeg")]
    [InlineData("webp")]
    [InlineData("bmp")]
    [InlineData("gif")]
    public void DecodesRequiredFormats(string extension)
    {
        using var f = new Fixture(); var path = f.Image("test." + extension);
        using var image = Codec.Decode(path, 0);
        Assert.Equal(80, image.Width); Assert.Equal(50, image.Height); Assert.False(AnimationProbe.IsAnimated(path));
    }
    [Theory]
    [InlineData("png")]
    [InlineData("webp")]
    public void TransparencyAndOriginalDimensionsSurviveDecoding(string extension)
    {
        using var f = new Fixture(); string path = Path.Combine(f.Root, "alpha." + extension);
        using var bitmap = new SKBitmap(new SKImageInfo(3, 2, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        bitmap.Erase(SKColors.Transparent); bitmap.SetPixel(1, 0, new SKColor(24, 80, 160, 128));
        bitmap.SetPixel(2, 1, SKColors.Red); Codec.Encode(bitmap, path, extension);
        using var decoded = Codec.Decode(path, 0);
        Assert.Equal(3, decoded.Width); Assert.Equal(2, decoded.Height);
        Assert.Equal(0, decoded.GetPixel(0, 0).Alpha);
        Assert.Equal(new SKColor(24, 80, 160, 128), decoded.GetPixel(1, 0));
        Assert.Equal(SKColors.Red, decoded.GetPixel(2, 1));
    }

    [Theory]
    [InlineData("jpg")]
    [InlineData("jpeg")]
    [InlineData("png")]
    [InlineData("bmp")]
    [InlineData("webp")]
    [InlineData("gif")]
    public void CorruptImagesFailWithoutChangingSource(string extension)
    {
        using var f = new Fixture(); string path = f.Image("bad." + extension);
        byte[] truncated = File.ReadAllBytes(path)[..12]; File.WriteAllBytes(path, truncated);
        Assert.Throws<IOException>(() => Codec.Decode(path, 0));
        Assert.False(AnimationProbe.IsAnimated(path)); Assert.Equal(truncated, File.ReadAllBytes(path));
    }

    [Fact]
    public void AnimatedWebpIsDetectedAndRejectedByDecoder()
    {
        using var f = new Fixture(); string still = f.Image("still.webp");
        byte[] imageChunks = File.ReadAllBytes(still)[12..];
        string path = Path.Combine(f.Root, "animated.webp");
        using (var data = new MemoryStream())
        {
            using var writer = new BinaryWriter(data, System.Text.Encoding.ASCII, true);
            void Chunk(string name, byte[] bytes)
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes(name)); writer.Write(bytes.Length); writer.Write(bytes);
                if ((bytes.Length & 1) != 0) writer.Write((byte)0);
            }
            // VP8X canvas 80x50, animation flag; two opaque lossless frames.
            Chunk("VP8X", [2, 0, 0, 0, 79, 0, 0, 49, 0, 0]);
            Chunk("ANIM", new byte[6]);
            byte[] frameHeader = [0, 0, 0, 0, 0, 0, 79, 0, 0, 49, 0, 0, 100, 0, 0, 2];
            Chunk("ANMF", frameHeader.Concat(imageChunks).ToArray());
            Chunk("ANMF", frameHeader.Concat(imageChunks).ToArray());
            using var output = new BinaryWriter(File.Create(path));
            output.Write("RIFF"u8); output.Write((int)data.Length + 4); output.Write("WEBP"u8); output.Write(data.ToArray());
        }
        using var codec = SKCodec.Create(path); Assert.NotNull(codec); Assert.Equal(2, codec.FrameCount);
        Assert.True(AnimationProbe.IsAnimated(path)); Assert.Throws<IOException>(() => Codec.Decode(path, 0));
    }

    [Fact]
    public void OriginalPixelBudgetIs512MiBAndOversizeIsRejectedBeforeAllocation()
    {
        using var f = new Fixture(); string path = Path.Combine(f.Root, "oversized.bmp");
        Assert.Equal(512L * 1024 * 1024, Codec.MaxBytes);
        Assert.True(10000L * 10000 * 4 <= Codec.MaxBytes);
        Assert.True(16385L * 8193 * 4 > Codec.MaxBytes);
        using (var writer = new BinaryWriter(File.Create(path)))
        {
            writer.Write((ushort)0x4d42); writer.Write(54); writer.Write(0); writer.Write(54);
            writer.Write(40); writer.Write(16385); writer.Write(8193); writer.Write((ushort)1); writer.Write((ushort)24);
            for (int i = 0; i < 6; i++) writer.Write(0);
        }
        Assert.Contains("512 MiB", Assert.Throws<IOException>(() => Codec.Decode(path, 0)).Message);
    }

    [Fact]
    public void PixelTransportAllowsAbove256MiBAndRejectsAbove512MiBBeforePayloadAllocation()
    {
        using var f = new Fixture();
        string Header(string name, int width, int height)
        {
            string path = Path.Combine(f.Root, name);
            using var writer = new BinaryWriter(File.Create(path));
            writer.Write(0x504C5058); writer.Write(width); writer.Write(height); writer.Write(checked(width * 4));
            return path;
        }

        var incomplete = Assert.Throws<IOException>(() => ImageService.ReadPixels(Header("above-old-limit.pixels", 8193, 8193)));
        Assert.Contains("不完整", incomplete.Message);
        var oversized = Assert.Throws<IOException>(() => ImageService.ReadPixels(Header("above-new-limit.pixels", 16385, 8193)));
        Assert.Contains("允許範圍", oversized.Message);
    }

    [Fact]
    public void LosslessWebpPreservesPixels()
    {
        using var f = new Fixture(); var png = f.Image("source.png"); using var original = Codec.Decode(png, 0);
        string webp = Path.Combine(f.Root, "target.webp"); Codec.Encode(original, webp, "webp");
        using var decoded = Codec.Decode(webp, 0);
        Assert.Equal(original.Bytes, decoded.Bytes);
    }
    [Fact]
    public void DetectsMultipleGifFrames()
    {
        using var f = new Fixture(); var path = f.Image("static.gif");
        var original = File.ReadAllBytes(path);
        // Duplicate the actual image descriptor/data segment in a valid static GIF.
        int packed = original[10], start = 13 + ((packed & 128) != 0 ? 3 * (1 << ((packed & 7) + 1)) : 0);
        while (original[start] == 0x21) { start += 2; while (original[start] != 0) start += original[start] + 1; start++; }
        Assert.Equal(0x2C, original[start]);
        var frame = original[start..^1]; File.WriteAllBytes(path, original[..^1].Concat(frame).Append((byte)0x3B).ToArray());
        Assert.True(AnimationProbe.IsAnimated(path)); Assert.Throws<IOException>(() => Codec.Decode(path, 0));
    }
    [Fact]
    public async Task ScannerRecursionAndCancellation()
    {
        using var f = new Fixture(); f.Image("a.jpg"); f.Image("child/b.png"); f.File("notes.txt");
        var scanner = new Scanner(new Profile(Path.Combine(f.Root, "profile")));
        var flat = await scanner.ScanAsync(f.Root, false, default);
        Assert.Single(flat, x => !x.IsFolder);
        var recursive = await scanner.ScanAsync(f.Root, true, default); Assert.Equal(2, recursive.Count); Assert.All(recursive, x => Assert.False(x.IsFolder));
        using var ct = new CancellationTokenSource(); ct.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scanner.ScanAsync(f.Root, true, ct.Token));
    }
    [Fact]
    public async Task WorkerTimeoutReleasesSlot()
    {
        using var f = new Fixture(); var profile = new Profile(Path.Combine(f.Root, "profile"));
        await using var pool = new WorkerPool(profile, Fixture.Worker);
        await Assert.ThrowsAsync<TimeoutException>(() => pool.RunAsync(["--stall-test"], default, TimeSpan.FromMilliseconds(150)));
        Assert.Equal(0, pool.ActiveCount);
        string source = f.Image("image.png"), output = Path.Combine(f.Root, "out.pixels");
        await pool.RunAsync(["decode", source, output, "32"], default);
        Assert.Equal(32, ImageService.ReadPixels(output).Width);
    }
    [Fact]
    public async Task PipelineCacheAndTemporaryCleanup()
    {
        using var f = new Fixture(); var profile = new Profile(Path.Combine(f.Root, "profile"));
        await using var pool = new WorkerPool(profile, Fixture.Worker); await using var images = new ImageService(profile, pool);
        string source = f.Image("image.png", 2048, 1024);
        Assert.Equal(256, (await images.LoadAsync(source, 256, default)).Width);
        Assert.Single(Directory.GetFiles(profile.Cache, "*.png"));
        Assert.Equal(1024, (await images.LoadAsync(source, 1024, default)).Width);
        Assert.Single(Directory.GetFiles(profile.Cache, "*.png")); Assert.Empty(Directory.GetFiles(profile.Temporary));
    }
    [Fact]
    public async Task NeverOverwritesOrMutatesChangedSource()
    {
        using var f = new Fixture(); var source = f.File("a.jpg"); var target = f.File("b.jpg", "keep");
        var profile = new Profile(Path.Combine(f.Root, "profile"));
        await using var pool = new WorkerPool(profile, Fixture.Worker); var operations = new FileOperations(profile, pool);
        var collision = await operations.ExecuteAsync([new(source, target, OperationKind.Rename, FileStamp.Read(source))], null, default);
        Assert.Equal(1, collision.Skipped); Assert.Equal("keep", File.ReadAllText(target)); Assert.True(File.Exists(source));
        var plan = new FilePlan(source, Path.Combine(f.Root, "c.jpg"), OperationKind.Rename, FileStamp.Read(source));
        File.AppendAllText(source, "changed");
        var changed = await operations.ExecuteAsync([plan], null, default); Assert.Equal(1, changed.Failed); Assert.True(File.Exists(source));
    }
    [Fact]
    public async Task ConversionPreservesSourceAndCancelDoesNothing()
    {
        using var f = new Fixture(); var path = f.Image("a.png"); byte[] original = File.ReadAllBytes(path);
        var entry = new LibraryEntry(path, "a.png", false, 0, original.Length);
        var plans = FilePlans.Convert([entry], OperationKind.Webp);
        var profile = new Profile(Path.Combine(f.Root, "profile"));
        await using var pool = new WorkerPool(profile, Fixture.Worker); var operations = new FileOperations(profile, pool);
        using var ct = new CancellationTokenSource(); ct.Cancel();
        Assert.Equal(1, (await operations.ExecuteAsync(plans, null, ct.Token)).Canceled);
        Assert.False(File.Exists(plans[0].Target));
        Assert.Equal(1, (await operations.ExecuteAsync(plans, null, default)).Succeeded);
        Assert.Equal(original, File.ReadAllBytes(path));
    }
}
