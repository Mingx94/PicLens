using PicLens;
using Windows.Foundation;

namespace PicLens.ViewModels.Tests;

public sealed class MainPageDragCoordinateTests
{
    [Fact]
    public void Drag_hit_test_point_uses_root_coordinates()
    {
        var point = MainPage.TranslateLibraryDragPointToRoot(
            new Point(24, 32),
            new Point(280, 96));

        Assert.Equal(304, point.X);
        Assert.Equal(128, point.Y);
    }
}
