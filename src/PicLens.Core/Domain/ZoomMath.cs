using PicLens.Core.Models;

namespace PicLens.Core.Domain;

public static class ZoomMath
{
    public const double MinZoom = 0.1;
    public const double MaxZoom = 8;
    public const double ZoomStep = 1.2;

    public static double ClampZoom(double zoom) => Math.Min(MaxZoom, Math.Max(MinZoom, zoom));

    public static ZoomState ResetZoomState() => new(1, new Point(0, 0));

    public static ZoomState ZoomAtPoint(
        double zoom,
        Point offset,
        Point viewportCenter,
        Point pointer,
        int delta)
    {
        var nextZoom = ClampZoom(delta > 0 ? zoom * ZoomStep : zoom / ZoomStep);
        var imagePoint = new Point(
            X: (pointer.X - viewportCenter.X - offset.X) / zoom,
            Y: (pointer.Y - viewportCenter.Y - offset.Y) / zoom);

        return new ZoomState(
            Zoom: nextZoom,
            Offset: new Point(
                X: pointer.X - viewportCenter.X - imagePoint.X * nextZoom,
                Y: pointer.Y - viewportCenter.Y - imagePoint.Y * nextZoom));
    }
}
