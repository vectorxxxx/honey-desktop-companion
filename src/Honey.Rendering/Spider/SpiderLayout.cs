using SkiaSharp;

namespace Honey.Rendering.Spider;

public readonly record struct SpiderLeg(SKPoint Root, SKPoint Knee, SKPoint Tip, float Width);

public sealed record SpiderLayout(
    SKPoint Center,
    SKRect Abdomen,
    SKRect Head,
    IReadOnlyList<SpiderLeg> Legs)
{
    public static SpiderLayout Create(
        float width,
        float height,
        float scale,
        float deviceScale = 1)
    {
        var safeScale = float.IsFinite(scale) && scale > 0 ? Math.Clamp(scale, 0.4f, 2f) : 1;
        var safeDeviceScale = SpiderViewportMetrics.NormalizeDeviceScale(deviceScale);
        var center = new SKPoint(width / 2, height / 2);
        var unit = SpiderViewportMetrics.CanonicalUnit * safeScale * safeDeviceScale;
        var abdomen = SKRect.Create(
            center.X - unit * 0.58f,
            center.Y - unit * 0.72f,
            unit * 1.16f,
            unit * 1.45f);
        var head = SKRect.Create(
            center.X - unit * 0.46f,
            center.Y - unit * 1.18f,
            unit * 0.92f,
            unit * 0.76f);
        var legs = new SpiderLeg[8];
        for (var side = -1; side <= 1; side += 2)
        {
            for (var index = 0; index < 4; index++)
            {
                var y = center.Y - unit * 0.63f + index * unit * 0.42f;
                var root = new SKPoint(center.X + side * unit * 0.42f, y);
                var knee = new SKPoint(
                    center.X + side * unit * (0.92f + index * 0.07f),
                    y + (index - 1.5f) * unit * 0.18f);
                var tip = new SKPoint(
                    center.X + side * unit * (1.48f + index * 0.08f),
                    y + (index - 1.5f) * unit * 0.32f);
                legs[(side < 0 ? 0 : 4) + index] =
                    new SpiderLeg(
                        root,
                        knee,
                        tip,
                        Math.Max(4 * safeDeviceScale, unit * 0.15f));
            }
        }

        return new SpiderLayout(center, abdomen, head, Array.AsReadOnly(legs));
    }
}
