using PicLens.Core.Models;

namespace PicLens.ViewModels;

public static class DragInteractionRules
{
    private const double DefaultAutoScrollEdgeSize = 72;
    private const double DefaultAutoScrollMaxStep = 48;

    public static double CalculateLibraryDragAutoScrollDelta(
        double pointerY,
        double viewportHeight,
        double edgeSize = DefaultAutoScrollEdgeSize,
        double maxStep = DefaultAutoScrollMaxStep)
    {
        if (viewportHeight <= 0 || edgeSize <= 0 || maxStep <= 0)
        {
            return 0;
        }

        var effectiveEdgeSize = Math.Min(edgeSize, viewportHeight / 2);
        if (pointerY < effectiveEdgeSize)
        {
            var strength = (effectiveEdgeSize - pointerY) / effectiveEdgeSize;
            return -Math.Clamp(strength * maxStep, 0, maxStep);
        }

        if (pointerY > viewportHeight - effectiveEdgeSize)
        {
            var strength = (pointerY - (viewportHeight - effectiveEdgeSize)) / effectiveEdgeSize;
            return Math.Clamp(strength * maxStep, 0, maxStep);
        }

        return 0;
    }
}
