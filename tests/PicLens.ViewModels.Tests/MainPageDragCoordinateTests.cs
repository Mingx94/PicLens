using PicLens.Core.Models;
using PicLens.ViewModels;

namespace PicLens.ViewModels.Tests;

public sealed class MainPageDragCoordinateTests
{
    [Fact]
    public void Drag_hit_test_point_uses_root_coordinates()
    {
        var point = DragInteractionRules.TranslateLibraryDragPointToRoot(
            new Point(24, 32),
            new Point(280, 96));

        Assert.Equal(304, point.X);
        Assert.Equal(128, point.Y);
    }

    [Theory]
    [InlineData(4, -15.11111111111111)]
    [InlineData(36, -8)]
    [InlineData(200, 0)]
    [InlineData(364, 8)]
    [InlineData(396, 15.11111111111111)]
    public void Drag_auto_scroll_delta_scales_near_vertical_edges(double pointerY, double expected)
    {
        var delta = DragInteractionRules.CalculateLibraryDragAutoScrollDelta(pointerY, viewportHeight: 400);

        Assert.Equal(expected, delta, precision: 12);
    }

    [Theory]
    [InlineData(-40)]
    [InlineData(440)]
    public void Drag_auto_scroll_delta_is_capped(double pointerY)
    {
        var delta = Math.Abs(DragInteractionRules.CalculateLibraryDragAutoScrollDelta(pointerY, viewportHeight: 400));

        Assert.Equal(16, delta);
    }
}
