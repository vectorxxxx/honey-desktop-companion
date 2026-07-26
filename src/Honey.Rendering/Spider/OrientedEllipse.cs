using SkiaSharp;

namespace Honey.Rendering.Spider;

public readonly record struct OrientedEllipse(
    SKPoint Center,
    float RadiusX,
    float RadiusY,
    float RotationRadians)
{
    public float Width => RadiusX * 2;
    public float Height => RadiusY * 2;
    public float MidX => Center.X;
    public float MidY => Center.Y;
    public float Left => Bounds.Left;
    public float Top => Bounds.Top;
    public float Right => Bounds.Right;
    public float Bottom => Bounds.Bottom;

    public SKRect Bounds
    {
        get
        {
            var cosine = MathF.Cos(RotationRadians);
            var sine = MathF.Sin(RotationRadians);
            var extentX = MathF.Sqrt(
                RadiusX * RadiusX * cosine * cosine
                + RadiusY * RadiusY * sine * sine);
            var extentY = MathF.Sqrt(
                RadiusX * RadiusX * sine * sine
                + RadiusY * RadiusY * cosine * cosine);
            return SKRect.Create(
                Center.X - extentX,
                Center.Y - extentY,
                extentX * 2,
                extentY * 2);
        }
    }

    public static OrientedEllipse Create(
        SKPoint center,
        float radiusX,
        float radiusY,
        float rotationRadians)
    {
        if (!float.IsFinite(radiusX) || radiusX <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radiusX));
        }

        if (!float.IsFinite(radiusY) || radiusY <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radiusY));
        }

        if (!float.IsFinite(center.X)
            || !float.IsFinite(center.Y)
            || !float.IsFinite(rotationRadians))
        {
            throw new ArgumentOutOfRangeException(nameof(center));
        }

        return new OrientedEllipse(center, radiusX, radiusY, rotationRadians);
    }

    public bool Contains(SKPoint point, float padding = 0)
    {
        if (!float.IsFinite(point.X)
            || !float.IsFinite(point.Y)
            || RadiusX <= 0
            || RadiusY <= 0)
        {
            return false;
        }

        var cosine = MathF.Cos(-RotationRadians);
        var sine = MathF.Sin(-RotationRadians);
        var x = point.X - Center.X;
        var y = point.Y - Center.Y;
        var localX = x * cosine - y * sine;
        var localY = x * sine + y * cosine;
        var safePadding = float.IsFinite(padding) ? Math.Max(0, padding) : 0;
        var radiusX = RadiusX + safePadding;
        var radiusY = RadiusY + safePadding;
        return localX * localX / (radiusX * radiusX)
            + localY * localY / (radiusY * radiusY) <= 1;
    }

    public static implicit operator SKRect(OrientedEllipse ellipse) => ellipse.Bounds;
}
