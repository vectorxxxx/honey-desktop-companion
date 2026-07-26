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
        float[] rootY = [-0.58f, -0.31f, 0.02f, 0.28f];
        float[] rootX = [0.34f, 0.48f, 0.57f, 0.55f];
        float[] hipX = [0.72f, 0.82f, 0.84f, 0.78f];
        float[] hipY = [-0.76f, -0.39f, 0.13f, 0.49f];
        float[] kneeX = [1.18f, 1.31f, 1.34f, 1.20f];
        float[] kneeY = [-1.01f, -0.52f, 0.35f, 0.79f];
        float[] tipX = [1.58f, 1.70f, 1.72f, 1.53f];
        float[] tipY = [-1.28f, -0.65f, 0.55f, 1.06f];
        var legs = new SpiderLeg[8];
        for (var side = -1; side <= 1; side += 2)
        {
            for (var index = 0; index < 4; index++)
            {
                var root = new SKPoint(
                    center.X + side * unit * rootX[index],
                    center.Y + unit * rootY[index]);
                var hip = new SKPoint(
                    center.X + side * unit * hipX[index],
                    center.Y + unit * hipY[index]);
                var knee = new SKPoint(
                    center.X + side * unit * kneeX[index],
                    center.Y + unit * kneeY[index]);
                var tip = new SKPoint(
                    center.X + side * unit * tipX[index],
                    center.Y + unit * tipY[index]);
                legs[(side < 0 ? 0 : 4) + index] =
                    new SpiderLeg(
                        root,
                        hip,
                        knee,
                        tip,
                        Math.Max(4.5f * safeDeviceScale, unit * 0.17f),
                        index < 2 ? SpiderLegLayer.AboveBody : SpiderLegLayer.BehindBody);
            }
        }

        return new SpiderLayout(center, abdomen, head, Array.AsReadOnly(legs));
    }
}
