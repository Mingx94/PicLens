using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;

namespace PicLens.App.Controls;

public enum IconKind
{
    ArrowLeft, ArrowRight, ChevronLeft, ChevronRight, Folder, FolderOpen,
    Image, Images, Minus, PanelLeft, Pencil, Plus, RefreshCw, Search, Trash2,
    X, CircleAlert, Play
}

/// <summary>Renders the bundled Lucide SVG subset in WPF using the inherited foreground.</summary>
public sealed class LucideIcon : FrameworkElement
{
    static readonly Dictionary<IconKind, (Geometry Shape, bool Filled)[]> shapes = [];
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(IconKind), typeof(LucideIcon),
        new FrameworkPropertyMetadata(IconKind.Image, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ForegroundProperty = Control.ForegroundProperty.AddOwner(
        typeof(LucideIcon), new FrameworkPropertyMetadata(Brushes.Black,
            FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));

    public IconKind Kind { get => (IconKind)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    static LucideIcon()
    {
        WidthProperty.OverrideMetadata(typeof(LucideIcon), new FrameworkPropertyMetadata(18d));
        HeightProperty.OverrideMetadata(typeof(LucideIcon), new FrameworkPropertyMetadata(18d));
    }

    public LucideIcon()
    {
        IsHitTestVisible = false;
        Focusable = false;
        VerticalAlignment = VerticalAlignment.Center;
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        double scale = Math.Min(ActualWidth, ActualHeight) / 24;
        if (scale <= 0) return;
        var pen = new Pen(Foreground, 2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        drawingContext.PushTransform(new MatrixTransform(scale, 0, 0, scale,
            (ActualWidth - 24 * scale) / 2, (ActualHeight - 24 * scale) / 2));
        foreach (var (shape, filled) in GetShapes(Kind))
            drawingContext.DrawGeometry(filled ? Foreground : null, pen, shape);
        drawingContext.Pop();
    }

    // Only trusted embedded assets are parsed. Geometry is frozen and shared by all tiles.
    static (Geometry Shape, bool Filled)[] GetShapes(IconKind kind)
    {
        if (shapes.TryGetValue(kind, out var cached)) return cached;
        string name = kind switch
        {
            IconKind.ArrowLeft => "arrow-left", IconKind.ArrowRight => "arrow-right",
            IconKind.ChevronLeft => "chevron-left", IconKind.ChevronRight => "chevron-right",
            IconKind.FolderOpen => "folder-open", IconKind.PanelLeft => "panel-left",
            IconKind.RefreshCw => "refresh-cw", IconKind.Trash2 => "trash-2",
            IconKind.CircleAlert => "circle-alert", _ => kind.ToString().ToLowerInvariant()
        };
        using var stream = Application.GetResourceStream(new Uri($"/PicLens;component/Icons/Lucide/{name}.svg", UriKind.Relative)).Stream;
        var root = XElement.Load(stream);
        var result = root.Elements().Select(element =>
        {
            double Number(string attribute, double fallback = 0) => element.Attribute(attribute) is { } value
                ? double.Parse(value.Value, CultureInfo.InvariantCulture) : fallback;
            Geometry geometry = element.Name.LocalName switch
            {
                "path" => Geometry.Parse((string?)element.Attribute("d") ?? throw new InvalidOperationException("Missing SVG path")),
                "circle" => new EllipseGeometry(new Point(Number("cx"), Number("cy")), Number("r"), Number("r")),
                "rect" => new RectangleGeometry(new Rect(Number("x"), Number("y"), Number("width"), Number("height")), Number("rx"), Number("ry", Number("rx"))),
                "line" => new LineGeometry(new Point(Number("x1"), Number("y1")), new Point(Number("x2"), Number("y2"))),
                _ => throw new InvalidOperationException($"Unsupported Lucide SVG element: {element.Name}")
            };
            geometry.Freeze();
            return (geometry, element.Attribute("fill") is { Value: not "none" });
        }).ToArray();
        shapes.Add(kind, result);
        return result;
    }
}
