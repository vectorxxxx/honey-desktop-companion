using SkiaSharp;

namespace Honey.Rendering.Spider;

public readonly record struct SpiderLimbSegment(
    SKPoint Start,
    SKPoint End,
    SKPoint StartTop,
    SKPoint StartBottom,
    SKPoint EndTop,
    SKPoint EndBottom,
    float StartWidth,
    float EndWidth,
    float AngleRadians,
    bool IsValid);

public static class SpiderLimbGeometry
{
    private const float MinimumLength = 0.001f;

    public static SpiderLimbSegment Create(
        SKPoint start,
        SKPoint end,
        float startWidth,
        float endWidth)
    {
        if (!IsFinite(start)
            || !IsFinite(end)
            || !float.IsFinite(startWidth)
            || !float.IsFinite(endWidth)
            || startWidth <= 0
            || endWidth <= 0)
        {
            return default;
        }

        var x = end.X - start.X;
        var y = end.Y - start.Y;
        var length = MathF.Sqrt(x * x + y * y);
        if (!float.IsFinite(length) || length <= MinimumLength)
        {
            return default;
        }

        var normalX = y / length;
        var normalY = -x / length;
        var startHalfWidth = startWidth / 2;
        var endHalfWidth = endWidth / 2;
        return new SpiderLimbSegment(
            start,
            end,
            new SKPoint(start.X + normalX * startHalfWidth, start.Y + normalY * startHalfWidth),
            new SKPoint(start.X - normalX * startHalfWidth, start.Y - normalY * startHalfWidth),
            new SKPoint(end.X + normalX * endHalfWidth, end.Y + normalY * endHalfWidth),
            new SKPoint(end.X - normalX * endHalfWidth, end.Y - normalY * endHalfWidth),
            startWidth,
            endWidth,
            MathF.Atan2(y, x),
            true);
    }

    private static bool IsFinite(SKPoint point) => float.IsFinite(point.X) && float.IsFinite(point.Y);
}
