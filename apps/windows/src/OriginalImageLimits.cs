namespace PicLens;

internal static class OriginalImageLimits
{
    internal const long MaxBytes = 512L * 1024 * 1024;
    internal static bool FitsDimensions(int width, int height) =>
        width > 0 && height > 0 && (long)width * height <= MaxBytes / 4;
}
