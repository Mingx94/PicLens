using PicLens.Core.Models;

namespace PicLens.Core.Domain;

public sealed record ZoomAtPointInput(
    double Zoom,
    Point Offset,
    Point ViewportCenter,
    Point Pointer,
    int Delta);

public static class ZoomMath
{
    public const double MinZoom = 0.1;
    public const double MaxZoom = 8;
    public const double ZoomStep = 1.2;

    public static double ClampZoom(double zoom) => Math.Min(MaxZoom, Math.Max(MinZoom, zoom));

    public static ZoomState ResetZoomState() => new(1, new Point(0, 0));

    public static ZoomState ZoomAtPoint(ZoomAtPointInput input)
    {
        var nextZoom = ClampZoom(input.Delta > 0 ? input.Zoom * ZoomStep : input.Zoom / ZoomStep);
        var imagePoint = new Point(
            X: (input.Pointer.X - input.ViewportCenter.X - input.Offset.X) / input.Zoom,
            Y: (input.Pointer.Y - input.ViewportCenter.Y - input.Offset.Y) / input.Zoom);

        return new ZoomState(
            Zoom: nextZoom,
            Offset: new Point(
                X: input.Pointer.X - input.ViewportCenter.X - imagePoint.X * nextZoom,
                Y: input.Pointer.Y - input.ViewportCenter.Y - imagePoint.Y * nextZoom));
    }
}
