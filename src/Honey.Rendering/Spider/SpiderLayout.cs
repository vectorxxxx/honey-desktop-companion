using SkiaSharp;

namespace Honey.Rendering.Spider;

public enum SpiderLegLayer
{
    BehindBody,
    AboveBody
}

public readonly record struct SpiderLeg(
    SKPoint Root,
    SKPoint Hip,
    SKPoint Knee,
    SKPoint Tip,
    float Width,
    SpiderLegLayer Layer);

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
        var silhouette = SpiderLegSilhouette.Create(safeScale);
        var legs = new SpiderLeg[8];
        for (var side = -1; side <= 1; side += 2)
        {
            for (var index = 0; index < 4; index++)
            {
                var local = silhouette[index];
                legs[(side < 0 ? 0 : 4) + index] =
                    new SpiderLeg(
                        ToWorld(local.Root, side, center, unit),
                        ToWorld(local.Hip, side, center, unit),
                        ToWorld(local.Knee, side, center, unit),
                        ToWorld(local.Tip, side, center, unit),
                        Math.Max(4.5f * safeDeviceScale, unit * 0.17f),
                        index < 2 ? SpiderLegLayer.AboveBody : SpiderLegLayer.BehindBody);
            }
        }

        return new SpiderLayout(center, abdomen, head, Array.AsReadOnly(legs));
    }

    private static SKPoint ToWorld(
        SKPoint local,
        int side,
        SKPoint center,
        float unit) =>
        new(center.X + side * unit * local.X, center.Y + unit * local.Y);
}
