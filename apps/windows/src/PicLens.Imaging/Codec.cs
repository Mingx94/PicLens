using SkiaSharp;
namespace PicLens.Imaging;

public static class Codec
{
    public const long MaxBytes = OriginalImageLimits.MaxBytes;
    public static bool FitsOriginalDimensions(int width, int height) =>
        OriginalImageLimits.FitsDimensions(width, height);
    public static SKBitmap Decode(string path, int edge)
    {
        using var codec = SKCodec.Create(path) ?? throw new IOException("無法辨識圖片格式。");
        if (codec.FrameCount > 1) throw new IOException("動畫圖片目前不支援預覽。");
        var size = codec.Info;
        if (!FitsOriginalDimensions(size.Width, size.Height))
            throw new IOException("原圖超過 512 MiB 像素上限。");
        var bitmap = new SKBitmap(new SKImageInfo(size.Width, size.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        var result = codec.GetPixels(bitmap.Info, bitmap.GetPixels());
        if (result != SKCodecResult.Success) { bitmap.Dispose(); throw new IOException($"圖片解碼失敗：{result}"); }
        if (edge <= 0 || Math.Max(size.Width, size.Height) <= edge) return bitmap;
        double ratio = edge / (double)Math.Max(size.Width, size.Height);
        var scaled = bitmap.Resize(new SKImageInfo(Math.Max(1, (int)(size.Width * ratio)), Math.Max(1, (int)(size.Height * ratio)), SKColorType.Bgra8888, SKAlphaType.Unpremul),
            new SKSamplingOptions(SKFilterMode.Linear)) ?? throw new IOException("無法產生預覽。");
        bitmap.Dispose(); return scaled;
    }
    public static void WritePixels(SKBitmap image, string output)
    {
        using var stream = new FileStream(output, FileMode.CreateNew);
        using var writer = new BinaryWriter(stream);
        writer.Write(0x504C5058); writer.Write(image.Width); writer.Write(image.Height); writer.Write(image.RowBytes);
        writer.Write(image.Bytes);
    }
    public static void Encode(SKBitmap image, string output, string format)
    {
        using var pixmap = image.PeekPixels();
        using var data = format switch
        {
            "webp" => pixmap.Encode(new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossless, 100)),
            "jpg" => pixmap.Encode(new SKJpegEncoderOptions(100, SKJpegEncoderDownsample.Downsample444, SKJpegEncoderAlphaOption.BlendOnBlack)),
            _ => pixmap.Encode(SKEncodedImageFormat.Png, 100)
        } ?? throw new IOException("圖片編碼失敗。");
        using var target = new FileStream(output, FileMode.CreateNew); data.SaveTo(target);
    }
}
