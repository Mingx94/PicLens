using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PicLens.Core;
using PicLens.Services;
namespace PicLens.App.Controls;

public sealed record ImageTile(BitmapSource Image, Rect SourceBounds, Rect ClipBounds);
public sealed record PreparedImage(int Width, int Height, IReadOnlyList<ImageTile> Tiles)
{
    public static PreparedImage Create(Pixels pixels)
    {
        List<ImageTile> tiles = [];
        const int edge = 2048;
        for (int y = 0; y < pixels.Height; y += edge) for (int x = 0; x < pixels.Width; x += edge)
        {
            int left = Math.Max(0, x - 1), top = Math.Max(0, y - 1);
            int right = Math.Min(pixels.Width, x + edge + 1), bottom = Math.Min(pixels.Height, y + edge + 1);
            int width = right - left, height = bottom - top, stride = width * 4;
            byte[] bytes = new byte[stride * height];
            for (int row = 0; row < height; row++)
                Buffer.BlockCopy(pixels.Bytes, (top + row) * pixels.Stride + left * 4, bytes, row * stride, stride);
            var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, bytes, stride);
            image.Freeze(); tiles.Add(new(image, new(left, top, width, height), new(x, y, Math.Min(edge, pixels.Width - x), Math.Min(edge, pixels.Height - y))));
        }
        return new(pixels.Width, pixels.Height, tiles);
    }
}
public sealed class ViewerCanvas : FrameworkElement
{
    PreparedImage? image; Point? drag; Zoom zoom = Zoom.Fit;
    public event Action? Painted;
    public event Action<double>? ZoomChanged;
    public Zoom CurrentZoom => zoom;
    public bool Original { get; private set; }
    public void SetImage(PreparedImage? value, bool original, bool reset = false)
    {
        image = value; Original = original;
        if (reset) ResetZoom();
        InvalidateVisual();
    }
    public void ResetZoom() { zoom = Zoom.Fit; ZoomChanged?.Invoke(zoom.Scale); InvalidateVisual(); }
    public void ChangeZoom(int direction) { zoom = zoom.At(0, 0, direction); ZoomChanged?.Invoke(zoom.Scale); InvalidateVisual(); }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc); dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(20, 22, 23)), null, new(RenderSize));
        if (image is null) return;
        double fit = Math.Min(Math.Max(1, ActualWidth - 48) / image.Width, Math.Max(1, ActualHeight - 48) / image.Height);
        double scale = fit * zoom.Scale;
        dc.PushClip(new RectangleGeometry(new(RenderSize)));
        dc.PushTransform(new MatrixTransform(scale, 0, 0, scale, (ActualWidth - image.Width * scale) / 2 + zoom.X, (ActualHeight - image.Height * scale) / 2 + zoom.Y));
        foreach (var tile in image.Tiles) { dc.PushClip(new RectangleGeometry(tile.ClipBounds)); dc.DrawImage(tile.Image, tile.SourceBounds); dc.Pop(); }
        dc.Pop(); dc.Pop();
        if (Original) Painted?.Invoke();
    }
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        Point p = e.GetPosition(this); zoom = zoom.At(p.X - ActualWidth / 2, p.Y - ActualHeight / 2, e.Delta);
        ZoomChanged?.Invoke(zoom.Scale); InvalidateVisual(); e.Handled = true;
    }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) { Focus(); drag = e.GetPosition(this); CaptureMouse(); e.Handled = true; }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (drag is { } previous && IsMouseCaptured)
        {
            Point next = e.GetPosition(this); zoom = zoom with { X = zoom.X + next.X - previous.X, Y = zoom.Y + next.Y - previous.Y };
            drag = next; InvalidateVisual(); e.Handled = true;
        }
    }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { drag = null; ReleaseMouseCapture(); e.Handled = true; }
    protected override void OnLostMouseCapture(MouseEventArgs e) { drag = null; base.OnLostMouseCapture(e); }
}
