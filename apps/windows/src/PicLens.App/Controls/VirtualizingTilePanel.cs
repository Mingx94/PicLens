using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
namespace PicLens.App.Controls;

public sealed class VirtualizingTilePanel : VirtualizingPanel, IScrollInfo
{
    public static readonly DependencyProperty TileWidthProperty = DependencyProperty.Register(nameof(TileWidth), typeof(double), typeof(VirtualizingTilePanel), new FrameworkPropertyMetadata(184d, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public static readonly DependencyProperty TileHeightProperty = DependencyProperty.Register(nameof(TileHeight), typeof(double), typeof(VirtualizingTilePanel), new FrameworkPropertyMetadata(212d, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public double TileWidth { get => (double)GetValue(TileWidthProperty); set => SetValue(TileWidthProperty, value); }
    public double TileHeight { get => (double)GetValue(TileHeightProperty); set => SetValue(TileHeightProperty, value); }
    int columns = 1; double extent, viewport, width, offset;
    public int RealizedCount => InternalChildren.Count;
    protected override Size MeasureOverride(Size available)
    {
        var owner = ItemsControl.GetItemsOwner(this);
        if (owner is null) return new();
        width = double.IsInfinity(available.Width) ? 800 : available.Width;
        viewport = double.IsInfinity(available.Height) ? 600 : available.Height;
        columns = Math.Max(1, (int)Math.Floor(width / Math.Max(1, TileWidth)));
        extent = Math.Ceiling(owner.Items.Count / (double)columns) * TileHeight;
        offset = Math.Clamp(offset, 0, Math.Max(0, extent - viewport));
        ScrollOwner?.InvalidateScrollInfo();
        int first = Math.Max(0, (int)Math.Floor(offset / TileHeight) * columns);
        int last = Math.Min(owner.Items.Count - 1, ((int)Math.Ceiling((offset + viewport) / TileHeight)) * columns - 1);
        _ = InternalChildren.Count;
        var generator = ItemContainerGenerator;
        if (generator is null || owner.Items.Count == 0) return new(width, viewport);
        for (int i = InternalChildren.Count - 1; i >= 0; i--)
        {
            int index = generator.IndexFromGeneratorPosition(new(i, 0));
            if (index < first || index > last)
            {
                if (generator is IRecyclingItemContainerGenerator recycling) recycling.Recycle(new(i, 0), 1);
                else generator.Remove(new(i, 0), 1);
                RemoveInternalChildRange(i, 1);
            }
        }
        var position = generator.GeneratorPositionFromIndex(first);
        int childIndex = position.Offset == 0 ? position.Index : position.Index + 1;
        using (generator.StartAt(position, GeneratorDirection.Forward, true))
        {
            for (int index = first; index <= last; index++, childIndex++)
            {
                var child = (UIElement)generator.GenerateNext(out bool fresh);
                if (fresh || !InternalChildren.Contains(child))
                {
                    if (childIndex >= InternalChildren.Count) AddInternalChild(child);
                    else InsertInternalChild(childIndex, child);
                    generator.PrepareItemContainer(child);
                }
                child.Measure(new(TileWidth, TileHeight));
            }
        }
        return new(width, viewport);
    }
    protected override Size ArrangeOverride(Size final)
    {
        foreach (UIElement child in InternalChildren)
        {
            int index = ItemContainerGenerator.IndexFromGeneratorPosition(new(InternalChildren.IndexOf(child), 0));
            child.Arrange(new(index % columns * TileWidth, index / columns * TileHeight - offset, TileWidth, TileHeight));
        }
        return final;
    }
    protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args)
    {
        if (args.Action == NotifyCollectionChangedAction.Reset) { RemoveInternalChildRange(0, InternalChildren.Count); offset = 0; }
        InvalidateMeasure();
    }
    public bool CanHorizontallyScroll { get; set; }
    public bool CanVerticallyScroll { get; set; } = true;
    public double ExtentWidth => width;
    public double ExtentHeight => extent;
    public double ViewportWidth => width;
    public double ViewportHeight => viewport;
    public double HorizontalOffset => 0;
    public double VerticalOffset => offset;
    public ScrollViewer? ScrollOwner { get; set; }
    public void SetHorizontalOffset(double value) { }
    public void SetVerticalOffset(double value) { offset = Math.Clamp(value, 0, Math.Max(0, extent - viewport)); InvalidateMeasure(); ScrollOwner?.InvalidateScrollInfo(); }
    public void LineUp() => SetVerticalOffset(offset - 36);
    public void LineDown() => SetVerticalOffset(offset + 36);
    public void LineLeft() { }
    public void LineRight() { }
    public void MouseWheelUp() => SetVerticalOffset(offset - TileHeight * .7);
    public void MouseWheelDown() => SetVerticalOffset(offset + TileHeight * .7);
    public void MouseWheelLeft() { }
    public void MouseWheelRight() { }
    public void PageUp() => SetVerticalOffset(offset - viewport);
    public void PageDown() => SetVerticalOffset(offset + viewport);
    public void PageLeft() { }
    public void PageRight() { }
    public Rect MakeVisible(Visual visual, Rect rectangle) => rectangle;
    public void ScrollToIndex(int index)
    {
        double top = index / columns * TileHeight;
        if (top < offset) SetVerticalOffset(top);
        else if (top + TileHeight > offset + viewport) SetVerticalOffset(top + TileHeight - viewport);
    }
}
