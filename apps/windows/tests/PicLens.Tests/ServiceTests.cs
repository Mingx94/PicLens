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
