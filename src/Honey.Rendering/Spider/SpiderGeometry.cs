using Honey.Domain.Model;
using SkiaSharp;

namespace Honey.Rendering.Spider;

public sealed record SpiderPose(
    float ViewportWidth,
    float ViewportHeight,
    float DeviceScale,
    SKPoint Center,
    SKRect Abdomen,
    SKRect Head,
    IReadOnlyList<SpiderLeg> Legs,
    SKRect ContentBounds);

public static class SpiderGeometry
{
    public static SpiderPose CreatePose(
        float width,
        float height,
        RenderSnapshot snapshot,
        float deviceScale = 1)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var safe = snapshot.Normalize();
        var safeDeviceScale = SpiderViewportMetrics.NormalizeDeviceScale(deviceScale);
        var layout = SpiderLayout.Create(width, height, safe.Scale, safeDeviceScale);
        var cadence = safe.Mode == PetMode.Berserk ? 5.4 : 3.1;
        var moodAmplitude = safe.Mood switch
        {
            PetMood.Sleepy => 0.25f,
            PetMood.Alert or PetMood.Angry => 1.25f,
            PetMood.Happy => 0.8f,
            _ => 0.6f
        };
        var legs = new SpiderLeg[layout.Legs.Count];
        for (var index = 0; index < layout.Legs.Count; index++)
        {
            var leg = layout.Legs[index];
            var phase = safe.AnimationTime * cadence + index * Math.PI / 2;
            var lift = MathF.Sin((float)phase) * leg.Width * moodAmplitude;
            legs[index] = leg with
            {
                Knee = new SKPoint(leg.Knee.X, leg.Knee.Y + lift),
                Tip = new SKPoint(leg.Tip.X, leg.Tip.Y - lift * 0.35f)
            };
        }

        return new SpiderPose(
            width,
            height,
            safeDeviceScale,
            layout.Center,
            layout.Abdomen,
            layout.Head,
            legs,
            CalculateContentBounds(layout.Abdomen, layout.Head, legs));
    }

    private static SKRect CalculateContentBounds(
        SKRect abdomen,
        SKRect head,
        IReadOnlyList<SpiderLeg> legs)
    {
        var left = Math.Min(abdomen.Left, head.Left);
        var top = Math.Min(abdomen.Top, head.Top);
        var right = Math.Max(abdomen.Right, head.Right);
        var bottom = Math.Max(abdomen.Bottom, head.Bottom);
        foreach (var leg in legs)
        {
            var radius = leg.Width * 1.18f / 2;
            Include(leg.Root, radius, ref left, ref top, ref right, ref bottom);
            Include(leg.Knee, radius, ref left, ref top, ref right, ref bottom);
            Include(leg.Tip, radius, ref left, ref top, ref right, ref bottom);
        }

        return new SKRect(left, top, right, bottom);
    }

    private static void Include(
        SKPoint point,
        float radius,
        ref float left,
        ref float top,
        ref float right,
        ref float bottom)
    {
        left = Math.Min(left, point.X - radius);
        top = Math.Min(top, point.Y - radius);
        right = Math.Max(right, point.X + radius);
        bottom = Math.Max(bottom, point.Y + radius);
    }
}
