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
            center.X - unit * 0.76f,
            center.Y - unit * 0.43f,
            unit * 1.52f,
            unit * 0.92f);
        var head = SKRect.Create(
            center.X - unit * 0.45f,
            center.Y - unit * 1.02f,
            unit * 0.90f,
            unit * 0.66f);
        var legs = new SpiderLeg[8];
        for (var side = -1; side <= 1; side += 2)
        {
            for (var index = 0; index < 4; index++)
            {
                var y = center.Y - unit * 0.48f + index * unit * 0.32f;
                var root = new SKPoint(center.X + side * unit * 0.54f, y);
                var knee = new SKPoint(
                    center.X + side * unit * (1.02f + index * 0.08f),
                    y + (index - 1.5f) * unit * 0.22f);
                var tip = new SKPoint(
                    center.X + side * unit * (1.56f + index * 0.09f),
                    y + (index - 1.5f) * unit * 0.39f);
                legs[(side < 0 ? 0 : 4) + index] =
                    new SpiderLeg(
                        root,
                        knee,
                        tip,
                        Math.Max(5 * safeDeviceScale, unit * 0.19f));
            }
        }

        return new SpiderLayout(center, abdomen, head, Array.AsReadOnly(legs));
    }
}
