using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using PicLens.Imaging;
using SkiaSharp;
namespace PicLens.Tests;

public sealed class Fixture : IDisposable
{
    public static string Repo
    {
        get { var p = new DirectoryInfo(AppContext.BaseDirectory); while (p is not null && !System.IO.File.Exists(Path.Combine(p.FullName, "apps", "windows", "PicLens.Windows.slnx"))) p = p.Parent; return p?.FullName ?? throw new IOException("找不到 repo"); }
    }
    public static string Worker => Path.Combine(Repo, "apps", "windows", "src", "PicLens.Worker", "bin",
#if DEBUG
        "Debug",
#else
        "Release",
#endif
        "net10.0-windows", "PicLens.Worker.exe");
    public string Root { get; } = Path.Combine(Repo, "artifacts", "wpf-tests", Guid.NewGuid().ToString("N"));
    public Fixture() => Directory.CreateDirectory(Root);
    public string File(string name, string text = "fixture") { string path = Path.Combine(Root, name); Directory.CreateDirectory(Path.GetDirectoryName(path)!); System.IO.File.WriteAllText(path, text); return path; }
    public string Image(string name, int width = 80, int height = 50)
    {
        string path = Path.GetFullPath(Path.Combine(Root, name)); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(35, 103, 80));
            using var paint = new SKPaint { Color = new SKColor(245, 190, 75) };
            canvas.DrawCircle(width * .35f, height * .5f, Math.Min(width, height) * .25f, paint);
        }
        string ext = Path.GetExtension(name);
        if (ext is ".gif" or ".bmp")
        {
            var frame = BitmapFrame.Create(BitmapSource.Create(width, height, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, bitmap.Bytes, bitmap.RowBytes));
            BitmapEncoder encoder = ext == ".gif" ? new GifBitmapEncoder() : new BmpBitmapEncoder();
            encoder.Frames.Add(frame); using var stream = System.IO.File.Create(path); encoder.Save(stream);
        }
        else Codec.Encode(bitmap, path, ext == ".webp" ? "webp" : ext is ".jpg" or ".jpeg" ? "jpg" : "png");
        return path;
    }
    public void Dispose()
    {
        string expected = Path.GetFullPath(Path.Combine(Repo, "artifacts", "wpf-tests")) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(Root).StartsWith(expected, StringComparison.OrdinalIgnoreCase)) throw new IOException("測試清理路徑錯誤");
        for (int attempt = 0; Directory.Exists(Root); attempt++)
        {
            try { Directory.Delete(Root, true); }
            catch (IOException) when (attempt < 5) { Thread.Sleep(100); }
        }
    }
}
