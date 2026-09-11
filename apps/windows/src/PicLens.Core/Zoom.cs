namespace PicLens.Core;

public readonly record struct Zoom(double Scale, double X, double Y)
{
    public static Zoom Fit => new(1, 0, 0);
    public Zoom At(double px, double py, int direction)
    {
        double next = Math.Clamp(direction > 0 ? Scale * 1.2 : Scale / 1.2, .1, 8);
        return new(next, px - (px - X) / Scale * next, py - (py - Y) / Scale * next);
    }
}
