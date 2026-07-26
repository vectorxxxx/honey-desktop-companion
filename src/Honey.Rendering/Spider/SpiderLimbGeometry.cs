using SkiaSharp;

namespace Honey.Rendering.Spider;

public readonly record struct SpiderLimbSegment(
    SKPoint Start,
    SKPoint End,
    SKPoint StartSideA,
    SKPoint StartSideB,
    SKPoint EndSideA,
    SKPoint EndSideB,
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

        var x = (double)end.X - start.X;
        var y = (double)end.Y - start.Y;
        var length = Math.Sqrt(x * x + y * y);
        if (!double.IsFinite(length) || length <= MinimumLength)
        {
            return default;
        }

        var normalX = y / length;
        var normalY = -x / length;
        var startHalfWidth = (double)startWidth / 2;
        var endHalfWidth = (double)endWidth / 2;
        var segment = new SpiderLimbSegment(
            start,
            end,
            new SKPoint(
                (float)(start.X + normalX * startHalfWidth),
                (float)(start.Y + normalY * startHalfWidth)),
            new SKPoint(
                (float)(start.X - normalX * startHalfWidth),
                (float)(start.Y - normalY * startHalfWidth)),
            new SKPoint(
                (float)(end.X + normalX * endHalfWidth),
                (float)(end.Y + normalY * endHalfWidth)),
            new SKPoint(
                (float)(end.X - normalX * endHalfWidth),
                (float)(end.Y - normalY * endHalfWidth)),
            startWidth,
            endWidth,
            (float)Math.Atan2(y, x),
            true);
        return IsFinite(segment) ? segment : default;
    }

    private static bool IsFinite(SKPoint point) => float.IsFinite(point.X) && float.IsFinite(point.Y);

    private static bool IsFinite(SpiderLimbSegment segment) =>
        IsFinite(segment.Start)
        && IsFinite(segment.End)
        && IsFinite(segment.StartSideA)
        && IsFinite(segment.StartSideB)
        && IsFinite(segment.EndSideA)
        && IsFinite(segment.EndSideB)
        && float.IsFinite(segment.StartWidth)
        && float.IsFinite(segment.EndWidth)
        && float.IsFinite(segment.AngleRadians);
}
