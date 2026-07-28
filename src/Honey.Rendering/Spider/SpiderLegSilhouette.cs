using SkiaSharp;

namespace Honey.Rendering.Spider;

internal readonly record struct SpiderLegControlPoints(
    SKPoint Root,
    SKPoint Hip,
    SKPoint Knee,
    SKPoint Tip);

internal static class SpiderLegSilhouette
{
    private static readonly SpiderLegControlPoints[] Base =
    [
        new(new SKPoint(.34f, -.78f), new SKPoint(.66f, -.96f), new SKPoint(1.35f, -1.15f), new SKPoint(1.48f, -1.28f)),
        new(new SKPoint(.42f, -.64f), new SKPoint(.78f, -.82f), new SKPoint(1.22f, -.64f), new SKPoint(1.62f, -.75f)),
        new(new SKPoint(.45f, -.50f), new SKPoint(.84f, -.08f), new SKPoint(1.28f, -.10f), new SKPoint(1.62f, .20f)),
        new(new SKPoint(.43f, -.38f), new SKPoint(.78f, .15f), new SKPoint(1.35f, .52f), new SKPoint(1.48f, .92f))
    ];

    private static readonly SpiderLegControlPoints[] Compact =
    [
        new(new SKPoint(.34f, -.78f), new SKPoint(.66f, -1.17f), new SKPoint(1.02f, -1.15f), new SKPoint(1.42f, -1.28f)),
        new(new SKPoint(.42f, -.64f), new SKPoint(.78f, -.84f), new SKPoint(1.18f, -.60f), new SKPoint(1.54f, -.72f)),
        new(new SKPoint(.45f, -.50f), new SKPoint(.82f, -.05f), new SKPoint(1.24f, -.06f), new SKPoint(1.54f, .22f)),
        new(new SKPoint(.43f, -.38f), new SKPoint(.76f, .18f), new SKPoint(1.32f, .54f), new SKPoint(1.42f, .90f))
    ];

    public static IReadOnlyList<SpiderLegControlPoints> Create(float petScale)
    {
        var safeScale = float.IsFinite(petScale) && petScale > 0
            ? Math.Clamp(petScale, 0.4f, 2f)
            : 1;
        var displayPixels = SpiderDetailLevelSelector.ReferencePetPixels * safeScale;
        var amount = Math.Clamp((displayPixels - 60f) / 30f, 0f, 1f);
        var smooth = amount * amount * (3f - 2f * amount);
        var compactBlend = 1f - smooth;
        var points = new SpiderLegControlPoints[Base.Length];
        for (var index = 0; index < points.Length; index++)
        {
            points[index] = new SpiderLegControlPoints(
                Base[index].Root,
                Lerp(Base[index].Hip, Compact[index].Hip, compactBlend),
                Lerp(Base[index].Knee, Compact[index].Knee, compactBlend),
                Lerp(Base[index].Tip, Compact[index].Tip, compactBlend));
        }

        return Array.AsReadOnly(points);
    }

    private static SKPoint Lerp(SKPoint start, SKPoint end, float amount) =>
        new(
            start.X + (end.X - start.X) * amount,
            start.Y + (end.Y - start.Y) * amount);
}
