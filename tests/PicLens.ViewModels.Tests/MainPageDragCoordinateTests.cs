using PicLens.Core.Models;
using PicLens.ViewModels;

namespace PicLens.ViewModels.Tests;

public sealed class MainPageDragCoordinateTests
{
    [Theory]
    [InlineData(4, -45.333333333333336)]
    [InlineData(36, -24)]
    [InlineData(200, 0)]
    [InlineData(364, 24)]
    [InlineData(396, 45.333333333333336)]
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

        Assert.Equal(48, delta);
    }
}
