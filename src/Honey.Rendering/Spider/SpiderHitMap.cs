namespace Honey.Rendering.Spider;

public sealed class SpiderHitMap
{
    private readonly SpiderLayout? _layout;

    private SpiderHitMap(SpiderLayout? layout)
    {
        _layout = layout;
    }

    public static SpiderHitMap CreateDefault(float width, float height, float scale)
    {
        if (!float.IsFinite(width) || !float.IsFinite(height) || width <= 0 || height <= 0)
        {
            return new SpiderHitMap(null);
        }

        return new SpiderHitMap(SpiderLayout.Create(width, height, scale));
    }

    public bool Contains(float x, float y)
    {
        if (_layout is null || !float.IsFinite(x) || !float.IsFinite(y))
        {
            return false;
        }

        if (ContainsEllipse(_layout.Abdomen, x, y) || ContainsEllipse(_layout.Head, x, y))
        {
            return true;
        }

        return _layout.Legs.Any(leg =>
            DistanceToSegment(x, y, leg.Root.X, leg.Root.Y, leg.Knee.X, leg.Knee.Y) <= leg.Width / 2
            || DistanceToSegment(x, y, leg.Knee.X, leg.Knee.Y, leg.Tip.X, leg.Tip.Y) <= leg.Width / 2);
    }

    private static bool ContainsEllipse(SkiaSharp.SKRect rectangle, float x, float y)
    {
        var radiusX = rectangle.Width / 2;
        var radiusY = rectangle.Height / 2;
        var normalizedX = (x - rectangle.MidX) / radiusX;
        var normalizedY = (y - rectangle.MidY) / radiusY;
        return normalizedX * normalizedX + normalizedY * normalizedY <= 1;
    }

    private static float DistanceToSegment(
        float x,
        float y,
        float startX,
        float startY,
        float endX,
        float endY)
    {
        var deltaX = endX - startX;
        var deltaY = endY - startY;
        var lengthSquared = deltaX * deltaX + deltaY * deltaY;
        if (lengthSquared <= float.Epsilon)
        {
            return MathF.Sqrt((x - startX) * (x - startX) + (y - startY) * (y - startY));
        }

        var t = Math.Clamp(((x - startX) * deltaX + (y - startY) * deltaY) / lengthSquared, 0, 1);
        var closestX = startX + t * deltaX;
        var closestY = startY + t * deltaY;
        return MathF.Sqrt((x - closestX) * (x - closestX) + (y - closestY) * (y - closestY));
    }
}
